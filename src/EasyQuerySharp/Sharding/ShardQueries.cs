using System.Data.Common;
using System.Runtime.ExceptionServices;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Sharding;

/// <summary>
/// 分片查询的执行（见 0006 §4、§5）：按 <see cref="ShardPlanner"/> 的单元渲染语句。一个单元直接执行；几个单元并行执行再归并：
/// <list type="bullet">
/// <item>列表：每个单元取前 offset + take 行，有排序时 k 路归并（排序列不在结果里时追加成隐藏列），否则按单元拼接，再统一跳过 / 截取；</item>
/// <item>计数相加，Any 任一为真，Sum 相加、Min / Max 取小 / 取大（忽略 NULL）；</item>
/// <item>分组、去重、聚合投影、窗口函数、Avg、流式读取不能跨单元合并，抛 <see cref="NotSupportedException"/> 写明改法。</item>
/// </list>
/// </summary>
internal static class ShardQueries
{
    public static async Task<List<T>> ListAsync<T>(EasyDb db, SelectQuery query, Func<DbDataReader, T> shaper, CancellationToken ct)
    {
        if (ShardPlanner.Plan(db, query) is not { } plan) return await db.Executor.QueryAsync(Render(db, query, null), shaper, ct).ConfigureAwait(false);
        if (plan.Units.Count == 0) return [];
        AddDefaultOrder(query, plan);
        if (plan.Units.Count == 1) return await db.Executor.QueryAsync(Render(db, query, plan.Units[0]), shaper, ct).ConfigureAwait(false);
        return await MergeAsync(db, query, plan, shaper, ct).ConfigureAwait(false);
    }

    public static async Task<long> CountAsync(EasyDb db, SelectQuery query, CancellationToken ct)
    {
        var counts = await ScalarsAsync(db, query, ct).ConfigureAwait(false);
        return counts.Sum(SqlExecutor.ToLong);
    }

    public static async Task<bool> AnyAsync(EasyDb db, SelectQuery query, CancellationToken ct)
    {
        var statements = Statements(db, query);
        var found = await RunAsync(db, statements, async (s, token) => (await db.Executor.QueryAsync(s, _ => true, token).ConfigureAwait(false)).Count > 0, ct)
            .ConfigureAwait(false);
        return found.Any(f => f);
    }

    public static async Task<object?> AggregateAsync(EasyDb db, SelectQuery query, string function, CancellationToken ct)
    {
        var statements = Statements(db, query);
        if (statements.Count == 0) return null;
        if (statements.Count == 1) return await db.Executor.ScalarAsync(statements[0], ct).ConfigureAwait(false);
        if (function is not (SqlFunctionNames.Sum or SqlFunctionNames.Min or SqlFunctionNames.Max))
            throw new NotSupportedException($"{function} 分到了 {statements.Count} 个单元（{Describe(statements)}），各单元的结果不能合并：平均值请用 SumAsync 与 CountAsync 自己算，或加分片键条件让查询落在一个单元里");
        var values = (await RunAsync(db, statements, (s, token) => db.Executor.ScalarAsync(s, token), ct).ConfigureAwait(false)).OfType<object>().ToList();
        if (values.Count == 0) return null;
        return function switch
        {
            SqlFunctionNames.Sum => ShardValues.Sum(values),
            SqlFunctionNames.Min => values.MinBy(v => v, ShardValues.Comparer),
            _ => values.MaxBy(v => v, ShardValues.Comparer),
        };
    }

    /// <summary>
    /// 只能落在一个单元上的执行（流式读取、自己读结果集）：返回要执行的语句；路由为空时任取一张分表（条件本来就查不到行）。
    /// </summary>
    public static SqlStatement SingleUnit(EasyDb db, SelectQuery query, string method)
    {
        if (ShardPlanner.Plan(db, query) is not { } plan) return Render(db, query, null);
        if (plan.Units.Count == 0) plan = ShardPlanner.Plan(db, query, [plan.Outer!.Rule.Nodes[0]])!;
        if (plan.Units.Count > 1)
            throw new NotSupportedException($"{method} 分到了 {plan.Units.Count} 个单元（{Describe(plan)}），不能跨单元{(method == "ReadAsync" ? "自己读结果集" : "流式读取")}：改用 ToListAsync，或加分片键条件让查询落在一个单元里");
        AddDefaultOrder(query, plan);
        return Render(db, query, plan.Units[0]);
    }

    /// <summary>
    /// 按节点计数（<c>ToShardingPageAsync</c>，对应 Java 的 toShardingPageResult 的 totalLines）：最外层分片表命中的每个节点各计一次数，
    /// <paramref name="known"/> 里不小于 0 的项直接用、不再查。最外层没有分片表时只有一项。
    /// </summary>
    public static async Task<IReadOnlyList<long>> CountByNodeAsync(EasyDb db, SelectQuery query, IReadOnlyList<long>? known, CancellationToken ct)
    {
        var plan = db.Shards.Enabled ? ShardPlanner.Plan(db, query) : null;
        if (plan?.Outer is not { } outer)
        {
            if (known is not null && known.Count != 1) throw new ArgumentException($"totalLines 有 {known.Count} 项，这条查询只计一次数", nameof(known));
            return [known is [>= 0 and var line] ? line : await db.Executor.CountAsync(query, ct).ConfigureAwait(false)];
        }
        var nodes = plan.Units.SelectMany(u => u.Nodes.Select(n => (Unit: u, Node: n))).ToList();
        if (known is not null && known.Count != nodes.Count) throw new ArgumentException($"totalLines 有 {known.Count} 项，这条查询命中 {nodes.Count} 个节点", nameof(known));
        var lines = new long[nodes.Count];
        var pending = new List<int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (known is not null && known[i] >= 0) lines[i] = known[i];
            else pending.Add(i);
        }
        var statements = pending.Select(i =>
        {
            var (unit, node) = nodes[i];
            var routes = new TableRoutes();
            foreach (var (table, tables) in unit.Routes) routes[table] = tables;
            routes[outer.Table] = [node.Table];
            return Render(db, query, unit with { Routes = routes });
        }).ToList();
        var counts = await RunAsync(db, statements, (s, token) => db.Executor.ScalarAsync(s, token), ct).ConfigureAwait(false);
        for (var i = 0; i < pending.Count; i++) lines[pending[i]] = SqlExecutor.ToLong(counts[i]);
        return lines;
    }

    // 各单元的第一行第一列（计数、聚合）；路由为空时没有语句
    private static async Task<object?[]> ScalarsAsync(EasyDb db, SelectQuery query, CancellationToken ct) =>
        await RunAsync(db, Statements(db, query), (s, token) => db.Executor.ScalarAsync(s, token), ct).ConfigureAwait(false);

    private static List<SqlStatement> Statements(EasyDb db, SelectQuery query) =>
        ShardPlanner.Plan(db, query) is { } plan ? [.. plan.Units.Select(u => Render(db, query, u))] : [Render(db, query, null)];

    private static SqlStatement Render(EasyDb db, SelectQuery query, ShardUnit? unit) =>
        unit is null ? SqlRenderer.Render(db.Options.Dialect, query) : SqlRenderer.Render(db.Options.Dialect, query, unit.Routes) with { DataSource = unit.DataSource };

    // 没写排序、带了 Take / Skip、命中多个节点时补规则的默认排序：跨表取前几行的结果才确定
    private static void AddDefaultOrder(SelectQuery query, ShardPlan plan)
    {
        if (plan.Outer is not { Rule.DefaultOrder: { } order } outer || query.OrderBy.Count > 0 || plan.OuterNodeCount < 2) return;
        if ((query.Limit is null && query.Offset is null) || query.GroupBy.Count > 0 || query.Distinct) return;
        query.OrderBy.Add(new OrderByItem(outer.Table.Column(outer.Table.Entity.GetColumn(order.Property)), order.Descending));
    }

    private static async Task<List<T>> MergeAsync<T>(EasyDb db, SelectQuery query, ShardPlan plan, Func<DbDataReader, T> shaper, CancellationToken ct)
    {
        RejectMerge(query, plan);
        var skip = query.Offset ?? 0;
        var take = query.Limit;
        // 每个单元取前 skip + take 行，归并后再统一跳过、截取
        query.Offset = null;
        query.Limit = take + skip;
        var visible = query.Columns.Count;
        var keyOrdinals = OrderKeyOrdinals(query);
        var hidden = query.Columns.Count > visible;
        var statements = plan.Units.Select(u => Render(db, query, u)).ToList();
        var parts = await RunAsync(db, statements, (statement, token) =>
        {
            VisibleColumnsReader? view = null;
            return db.Executor.QueryAsync(statement, r => (Row: shaper(hidden ? view ??= new VisibleColumnsReader(r, visible) : r), Keys: ReadKeys(r, keyOrdinals)), token);
        }, ct).ConfigureAwait(false);
        var merged = query.OrderBy.Count == 0 ? parts.SelectMany(p => p.Select(x => x.Row)) : MergeSorted(parts, new OrderComparer(query.OrderBy, db.Options.Dialect));
        var result = new List<T>();
        long index = 0;
        foreach (var row in merged)
        {
            if (index++ < skip) continue;
            if (take is { } limit && result.Count >= limit) break;
            result.Add(row);
        }
        return result;
    }

    // 分组、去重、聚合投影、窗口函数的结果是按单元算的，拼起来不对；原生排序片段没法在内存里比较
    private static void RejectMerge(SelectQuery query, ShardPlan plan)
    {
        string? shape = query switch
        {
            { GroupBy.Count: > 0 } or { Having.Count: > 0 } => "分组",
            { Distinct: true } => "去重",
            _ when query.Columns.Any(c => HasAggregate(c.Expr)) => "聚合投影",
            _ when query.Columns.Any(c => c.Expr is WindowExpr) || query.OrderBy.Any(o => o.Expr is WindowExpr) => "窗口函数",
            _ when query.OrderBy.Any(o => o.Raw) => "原生排序片段",
            _ => null,
        };
        if (shape is not null)
            throw new NotSupportedException($"这条查询分到了 {plan.Units.Count} 个单元（{Describe(plan)}），{shape}的结果不能跨单元合并：加分片键条件让它落在一个数据源里（同一数据源的分表在库内 UNION ALL），或调大 EasyDbOptions.MaxUnionTables");
    }

    private static bool HasAggregate(SqlExpr expr) => expr switch
    {
        FunctionExpr { Name: SqlFunctionNames.Count or SqlFunctionNames.CountDistinct or SqlFunctionNames.Sum or SqlFunctionNames.SumDistinct or SqlFunctionNames.Max
            or SqlFunctionNames.Min or SqlFunctionNames.Avg or SqlFunctionNames.AvgDistinct or SqlFunctionNames.StringJoin or SqlFunctionNames.StringJoinDistinct } => true,
        OrderedStringJoinExpr => true,
        FunctionExpr f => f.Arguments.Any(HasAggregate),
        FragmentExpr f => f.Arguments.Any(HasAggregate),
        BinaryExpr b => HasAggregate(b.Left) || HasAggregate(b.Right),
        CastExpr c => HasAggregate(c.Operand),
        _ => false,
    };

    // 排序列在结果里的位置：已经选出的列直接用，否则追加成隐藏列
    private static int[] OrderKeyOrdinals(SelectQuery query) =>
    [
        .. query.OrderBy.Select((order, i) =>
        {
            var existing = query.Columns.FindIndex(c => SameColumn(c.Expr, order.Expr));
            if (existing >= 0) return existing;
            query.Columns.Add(new SelectColumn(order.Expr, $"eqs_order_{i}"));
            return query.Columns.Count - 1;
        }),
    ];

    private static bool SameColumn(SqlExpr a, SqlExpr b) =>
        ReferenceEquals(a, b) || (a is ColumnExpr x && b is ColumnExpr y && ReferenceEquals(x.Table, y.Table) && x.Name == y.Name);

    private static object?[] ReadKeys(DbDataReader reader, int[] ordinals)
    {
        var keys = new object?[ordinals.Length];
        for (var i = 0; i < ordinals.Length; i++) keys[i] = reader.IsDBNull(ordinals[i]) ? null : reader.GetValue(ordinals[i]);
        return keys;
    }

    // k 路归并：各单元的结果已按同一排序排好；相等时按单元顺序，保持稳定
    private static IEnumerable<T> MergeSorted<T>(IReadOnlyList<List<(T Row, object?[] Keys)>> parts, OrderComparer comparer)
    {
        var queue = new PriorityQueue<(int Part, int Index), (object?[] Keys, int Part)>(
            Comparer<(object?[] Keys, int Part)>.Create((a, b) => comparer.Compare(a.Keys, b.Keys) is var c && c != 0 ? c : a.Part.CompareTo(b.Part)));
        for (var p = 0; p < parts.Count; p++)
            if (parts[p].Count > 0) queue.Enqueue((p, 0), (parts[p][0].Keys, p));
        while (queue.TryDequeue(out var at, out _))
        {
            var part = parts[at.Part];
            yield return part[at.Index].Row;
            if (at.Index + 1 < part.Count) queue.Enqueue((at.Part, at.Index + 1), (part[at.Index + 1].Keys, at.Part));
        }
    }

    /// <summary>
    /// 并行执行各单元（任何一个失败就取消其余，抛出那个失败）。事务里同一数据源共用一个连接，所以同一数据源的单元依次执行、不同数据源并行；
    /// 不在事务里时每个单元各取一个连接。结果按单元的顺序。
    /// </summary>
    private static async Task<TResult[]> RunAsync<TResult>(EasyDb db, IReadOnlyList<SqlStatement> statements, Func<SqlStatement, CancellationToken, Task<TResult>> run,
        CancellationToken ct)
    {
        var results = new TResult[statements.Count];
        if (statements.Count == 1)
        {
            results[0] = await run(statements[0], ct).ConfigureAwait(false);
            return results;
        }
        using var failed = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var inTransaction = EasyTransaction.Current(db) is not null;
        var lanes = Enumerable.Range(0, statements.Count).GroupBy(i => inTransaction ? statements[i].DataSource : (object)i);
        var tasks = lanes.Select(async lane =>
        {
            foreach (var i in lane)
            {
                try
                {
                    results[i] = await run(statements[i], failed.Token).ConfigureAwait(false);
                }
                catch
                {
                    await failed.CancelAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }).ToList();
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // 被连带取消的单元抛的是取消，真正的失败是另一个
            if (tasks.Where(t => t.IsFaulted).SelectMany(t => t.Exception!.InnerExceptions).FirstOrDefault(e => e is not OperationCanceledException) is { } failure)
                ExceptionDispatchInfo.Throw(failure);
            throw;
        }
        return results;
    }

    private static string Describe(ShardPlan plan) =>
        string.Join("、", plan.Units.GroupBy(u => u.DataSource).Select(g => $"{g.Key} 的 {g.Sum(u => u.Nodes.Count)} 张表"));

    private static string Describe(IReadOnlyList<SqlStatement> statements) => string.Join("、", statements.Select(s => s.DataSource).Distinct());

    /// <summary>按 ORDER BY 比较两行的排序键；NULL 的位置按排序项指定的，没有指定时按方言的默认（<see cref="Dialects.SqlDialect.DefaultNullsOrder"/>）。</summary>
    private sealed class OrderComparer(IReadOnlyList<OrderByItem> orders, Dialects.SqlDialect dialect) : IComparer<object?[]>
    {
        public int Compare(object?[]? x, object?[]? y)
        {
            for (var i = 0; i < orders.Count; i++)
            {
                var (a, b) = (x![i], y![i]);
                var order = orders[i];
                int c;
                if (a is null || b is null)
                    c = a is null && b is null ? 0 : (a is null) == ((order.Nulls ?? dialect.DefaultNullsOrder(order.Descending)) == NullsOrder.First) ? -1 : 1;
                else c = order.Descending ? -ShardValues.Comparer.Compare(a, b) : ShardValues.Comparer.Compare(a, b);
                if (c != 0) return c;
            }
            return 0;
        }
    }
}

/// <summary>跨单元合并时比较、相加数据库返回的值。</summary>
internal static class ShardValues
{
    /// <summary>
    /// 比较两个非 null 的值：字符串按序数（与 Java 的 String.compareTo 一致，不按数据库的排序规则），数值跨类型时按 decimal / double 比较，其余按 IComparable。
    /// </summary>
    public static IComparer<object> Comparer { get; } = Comparer<object>.Create(Compare);

    private static int Compare(object? a, object? b)
    {
        if (a is string s && b is string t) return string.CompareOrdinal(s, t);
        if (a!.GetType() != b!.GetType() && IsNumber(a) && IsNumber(b))
            return a is double or float || b is double or float ? ToDouble(a).CompareTo(ToDouble(b)) : ToDecimal(a).CompareTo(ToDecimal(b));
        return System.Collections.Comparer.DefaultInvariant.Compare(a, b);
    }

    /// <summary>各单元 SUM 的和：整数精确相加（DuckDB 的 SUM 返回 HUGEINT），有浮点时按 double，其余按 decimal（精确）。</summary>
    public static object Sum(IReadOnlyList<object> values)
    {
        if (values.All(IsInteger))
        {
            var sum = values.Aggregate(System.Numerics.BigInteger.Zero, (total, v) => total + (v is System.Numerics.BigInteger big ? big : ToDecimalInteger(v)));
            return sum >= long.MinValue && sum <= long.MaxValue ? (object)(long)sum : sum;
        }
        if (values.Any(v => v is double or float)) return values.Sum(ToDouble);
        return values.Sum(ToDecimal);

        static System.Numerics.BigInteger ToDecimalInteger(object v) => new(Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static double ToDouble(object value) => value is System.Numerics.BigInteger big ? (double)big : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

    private static decimal ToDecimal(object value) => value is System.Numerics.BigInteger big ? (decimal)big : Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsInteger(object value) => value is sbyte or byte or short or ushort or int or uint or long or ulong or System.Numerics.BigInteger;

    private static bool IsNumber(object value) => IsInteger(value) || value is decimal or double or float;
}
