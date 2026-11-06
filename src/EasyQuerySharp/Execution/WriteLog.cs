using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Execution;

/// <summary>
/// 写入语句的标记：<see cref="Rendering.SqlRenderer"/> 渲染 INSERT / UPDATE / DELETE / INSERT … SELECT 时盖上，
/// <see cref="SqlSession"/> 执行成功后记进 <see cref="WriteLog"/>。只引用语句的语法树，主键到通知时才提取（没有写入监听器时不提取）。
/// </summary>
internal sealed class WriteMark
{
    private readonly object _statement;
    private readonly bool _exactConflictTarget;

    private WriteMark(EntityTableSource table, WriteKind kind, object statement, bool exactConflictTarget = false)
    {
        Table = table;
        Kind = kind;
        _statement = statement;
        _exactConflictTarget = exactConflictTarget;
    }

    /// <summary>
    /// 插入；冲突时更新的 upsert 是 Insert | Update。<paramref name="exactConflictTarget"/>：数据库只在写出的约束列上处理冲突
    /// （MySQL 的 ON DUPLICATE KEY 在任何唯一键上冲突都会更新，不是）。
    /// </summary>
    public static WriteMark Insert(InsertStatement insert, bool exactConflictTarget) =>
        new(insert.Table, insert.Conflict is { Ignore: false } ? WriteKind.Insert | WriteKind.Update : WriteKind.Insert, insert, exactConflictTarget);

    public static WriteMark InsertSelect(InsertSelectStatement insert) => new(insert.Table, WriteKind.Insert, insert);

    public static WriteMark Update(UpdateStatement update) => new(update.Table, WriteKind.Update, update);

    public static WriteMark Delete(DeleteStatement delete) => new(delete.Table, WriteKind.Delete, delete);

    /// <summary>写入的目标表（换表名、schema 之后）。</summary>
    public EntityTableSource Table { get; }

    public WriteKind Kind { get; }

    /// <summary>写入的实体类型；按字典写入时为 null（那时的元数据只承载表名，没有列）。</summary>
    public Type? EntityType => Table.Entity.Columns.Count == 0 ? null : Table.Entity.Type;

    /// <summary>受影响行的主键（属性类型）；不能确定时为 null，规则见 <see cref="WriteEvent.Keys"/>。</summary>
    public IReadOnlyList<object>? Keys()
    {
        if (Table.Entity.KeyColumns is not [var key]) return null;
        try
        {
            return _statement switch
            {
                InsertStatement insert => InsertKeys(insert, key),
                UpdateStatement update => update.Sets.Any(s => IsKey(s.Column, key)) ? null : WhereKeys(update.Where, key),
                DeleteStatement delete => WhereKeys(delete.Where, key),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            // 参数值还原不成主键类型（如自定义转换器只认部分值）：当作主键不明，监听器按保守的方式处理
            return null;
        }
    }

    private List<object>? InsertKeys(InsertStatement insert, ColumnMetadata key)
    {
        // 冲突时更新的行由约束列定位：约束列不是主键、或数据库不只看约束列时，更新到的可能是主键以外的行
        if (insert.Conflict is { Ignore: false } conflict && !(_exactConflictTarget && conflict.ConflictColumns is [var on] && IsKey(on, key)))
            return null;
        var index = -1;
        for (var i = 0; i < insert.Columns.Count && index < 0; i++)
            if (IsKey(insert.Columns[i], key)) index = i;
        // 自增主键不写进语句
        if (index < 0) return null;
        var keys = new List<object>(insert.Rows.Count);
        foreach (var row in insert.Rows)
        {
            // 值由 SQL 模板或生成 SQL 给出时不知道
            if (row[index] is not ParameterExpr p) return null;
            if (ModelValue(key, p.Value) is { } value) keys.Add(value);
        }
        return keys;
    }

    // 顶层 AND 里第一个主键条件：主键 = 参数、主键 IN (参数…)、全由这两种组成的 OR（按上限拆开的 IN）
    private List<object>? WhereKeys(IEnumerable<SqlExpr> where, ColumnMetadata key)
    {
        foreach (var conjunct in Conjuncts(where))
            if (KeysOf(conjunct, key) is { } keys) return keys;
        return null;
    }

    private static IEnumerable<SqlExpr> Conjuncts(IEnumerable<SqlExpr> where) =>
        where.SelectMany(e => e is LogicalExpr { IsAnd: true } and ? Conjuncts(and.Operands) : [e]);

    private List<object>? KeysOf(SqlExpr e, ColumnMetadata key)
    {
        switch (e)
        {
            case BinaryExpr { Operator: SqlBinaryOperator.Equal, Left: var left, Right: ParameterExpr p } when IsKey(left, key):
                return Values([p], key);
            case BinaryExpr { Operator: SqlBinaryOperator.Equal, Left: ParameterExpr p, Right: var right } when IsKey(right, key):
                return Values([p], key);
            case InExpr { Negated: false, Values: { } values } i when IsKey(i.Operand, key) && values.All(v => v is ParameterExpr):
                return Values(values.Cast<ParameterExpr>(), key);
            case LogicalExpr { IsAnd: false } or:
                var union = new List<object>();
                foreach (var operand in or.Operands)
                {
                    if (KeysOf(operand, key) is not { } keys) return null;
                    union.AddRange(keys);
                }
                return union;
            default:
                return null;
        }
    }

    // NULL 参数（WhereById(null)）匹配不到行，不算主键
    private static List<object> Values(IEnumerable<ParameterExpr> parameters, ColumnMetadata key) =>
        [.. parameters.Select(p => ModelValue(key, p.Value)).OfType<object>()];

    // 参数是数据库值：经转换器还原成属性值
    private static object? ModelValue(ColumnMetadata key, object? value)
    {
        if (value is null) return null;
        if (key.Converter is { } converter) value = converter.FromProvider(ValueReader.ConvertTo(value, converter.ProviderType));
        return ValueReader.ConvertTo(value, key.ClrType);
    }

    // 目标表自己的主键列（连接进来的同实体表是另一个来源）
    private bool IsKey(SqlExpr e, ColumnMetadata key) => e is ColumnExpr c && ReferenceEquals(c.Table, Table) && c.Name == key.Name;
}

/// <summary>
/// 一个会话（或一个事务）里成功执行的写入，结束时按表合并成 <see cref="WriteEvent"/> 通知写入监听器。
/// 没有写入监听器时不创建（<see cref="Create"/> 返回 null），执行路径上什么也不记。
/// </summary>
internal sealed class WriteLog
{
    private readonly EasyDb _db;
    private readonly List<WriteMark> _marks = [];
    private readonly Lock _lock = new();

    private WriteLog(EasyDb db) => _db = db;

    /// <summary>有写入监听器时新建，否则为 null。</summary>
    public static WriteLog? Create(EasyDb db) => db.Options.WriteListeners.Count > 0 ? new WriteLog(db) : null;

    /// <summary>记下一次成功执行的语句（批量时是一批）里的写入。</summary>
    public void Record(IReadOnlyList<SqlStatement> statements)
    {
        lock (_lock)
            foreach (var statement in statements)
                if (statement.Write is { } mark) _marks.Add(mark);
    }

    /// <summary>并入另一份记录（批量写入自己的本地事务提交之后）。</summary>
    public void Append(WriteLog other)
    {
        WriteMark[] marks;
        lock (other._lock) marks = [.. other._marks];
        lock (_lock) _marks.AddRange(marks);
    }

    /// <summary>
    /// 执行不在事务里的一个会话，结束后通知。中途失败时，失败之前已经自动提交的写入照样通知，再抛出原来的异常
    /// （通知也失败时两个异常一起放进 <see cref="AggregateException"/>）。
    /// </summary>
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        T result;
        try
        {
            result = await work().ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            try
            {
                await NotifyAsync(ct).ConfigureAwait(false);
            }
            catch (Exception listener)
            {
                throw new AggregateException(failure, listener);
            }
            throw;
        }
        await NotifyAsync(ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>按表合并记下的写入，依次通知每个监听器；全部通知完之后，有失败的把异常包成 <see cref="EasyQueryException"/> 抛出。</summary>
    public async Task NotifyAsync(CancellationToken ct)
    {
        WriteMark[] marks;
        lock (_lock)
        {
            marks = [.. _marks];
            _marks.Clear();
        }
        if (marks.Length == 0) return;

        List<Exception>? failures = null;
        foreach (var e in Merge(marks))
            foreach (var listener in _db.Options.WriteListeners)
            {
                try
                {
                    await listener.OnWrittenAsync(e, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    (failures ??= []).Add(ex);
                }
            }
        if (failures is null) return;
        throw new EasyQueryException($"数据库已写入，写入监听器失败：{failures[0].Message}", null,
            failures.Count == 1 ? failures[0] : new AggregateException(failures));
    }

    // 同一张表（schema + 表名）合并：种类取并集，主键取并集（有一条不明就是不明），实体类型不一致时为 null
    private IEnumerable<WriteEvent> Merge(WriteMark[] marks) =>
        marks.GroupBy(m => (m.Table.Schema, m.Table.TableName)).Select(g =>
        {
            var types = g.Select(m => m.EntityType).Distinct().ToList();
            var kind = g.Aggregate((WriteKind)0, (k, m) => k | m.Kind);
            List<object>? keys = [];
            foreach (var mark in g)
            {
                if (mark.Keys() is not { } some)
                {
                    keys = null;
                    break;
                }
                keys.AddRange(some);
            }
            return new WriteEvent(_db, g.Key.Schema, g.Key.TableName, types.Count == 1 ? types[0] : null, kind, keys?.Distinct().ToList());
        });
}
