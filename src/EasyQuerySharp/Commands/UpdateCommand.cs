using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>按实体更新时写哪些列。</summary>
public enum ColumnStrategy
{
    /// <summary>全部列（默认，与 Java 版一致）。</summary>
    All,

    /// <summary>只写值不为 null 的列（对应 Java 的 ONLY_NOT_NULL_COLUMNS）。</summary>
    OnlyNotNull,

    /// <summary>只写值为 null 的列（对应 Java 的 ONLY_NULL_COLUMNS）。</summary>
    OnlyNull,
}

/// <summary>
/// UPDATE。两种用法：
/// <list type="bullet">
/// <item>按条件：<c>db.Update&lt;Topic&gt;().Set(t => new Topic { Title = "x", Stars = t.Stars + 1 }).Where(t => t.Id == id)</c>；</item>
/// <item>按实体：<c>db.Update(topic)</c>，写全部列（主键、<see cref="UpdateIgnoreAttribute"/> 除外），按主键定位。
/// 实体在追踪作用域里被追踪时只写变化的列（见 <see cref="EasyDb.BeginTracking"/>）。</item>
/// </list>
/// 实体有逻辑删除列时条件里自动加"未删除"；没有任何条件的按条件更新会被拒绝（除非调用 <c>All()</c>）。
/// </summary>
public sealed class UpdateCommand<T> : WriteCommandBase<UpdateCommand<T>, T> where T : class
{
    private readonly List<SetSpec> _sets = [];
    private readonly Dictionary<string, string> _columnSql = new(StringComparer.Ordinal);
    private ColumnStrategy? _strategy;
    private List<ColumnMetadata>? _whereColumns;
    private List<string>? _onlyColumns;
    private List<string>? _ignoredColumns;
    private T? _before;

    internal UpdateCommand(EasyDb db, IReadOnlyList<T>? entities) : base(db, entities)
    {
    }

    /// <summary>
    /// 用成员初始化表达式设置多列，值可以引用原列：<c>t => new Topic { Stars = t.Stars + 1 }</c>。
    /// 值对象可以整个赋值（<c>Address = address</c>，为 null 时它的列都写 NULL），也可以只写其中几项（<c>Address = new Address { City = "hz" }</c>）。
    /// </summary>
    public UpdateCommand<T> Set(Expression<Func<T, T>> setter)
    {
        if (setter.Body is not MemberInitExpression init)
            throw new ArgumentException("Set 需要成员初始化表达式，如 t => new Topic { Title = \"x\" }", nameof(setter));
        foreach (var binding in init.Bindings.Cast<MemberAssignment>()) AddSet(binding.Member.Name, binding.Expression, setter.Parameters);
        return this;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    private void AddSet(string path, Expression value, IReadOnlyList<ParameterExpression> parameters)
    {
        if (!Entity.ValueObjects.TryGetValue(path, out var valueObject))
        {
            _sets.Add(new SetSpec(path, Expression.Lambda(value, parameters), SetKind.Expression));
            return;
        }
        if (value is MemberInitExpression members)
        {
            foreach (var binding in members.Bindings.Cast<MemberAssignment>()) AddSet(path + "." + binding.Member.Name, binding.Expression, parameters);
            return;
        }
        // 整个值对象：不引用原行时先在客户端求出来，各列取它的值（为 null 时都写 NULL）；引用原行时各列取对应的成员
        var usesRow = ClientEvaluator.MustTranslate(value);
        var evaluated = usesRow ? null : ClientEvaluator.Evaluate(value);
        foreach (var (column, relative) in valueObject.Columns.Zip(valueObject.Shape.Columns))
        {
            Expression columnValue = usesRow
                ? relative.OwnerPath.Append(relative.Property).Aggregate(value, Expression.Property)
                : Expression.Constant(evaluated is null ? null : relative.Getter(evaluated), typeof(object));
            _sets.Add(new SetSpec(column.PropertyName, Expression.Lambda(columnValue, parameters), SetKind.Expression));
        }
    }

    /// <summary>把一列设为常量值。</summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    public UpdateCommand<T> Set<TValue>(Expression<Func<T, TValue>> column, TValue value)
    {
        _sets.Add(new SetSpec(ExpressionTranslator.MemberPath(column), Expression.Lambda(Expression.Constant(value, typeof(TValue)), column.Parameters), SetKind.Expression));
        return this;
    }

    /// <summary>把一列设为表达式的值（可以引用原列）。</summary>
    public UpdateCommand<T> Set<TValue>(Expression<Func<T, TValue>> column, Expression<Func<T, TValue>> value)
    {
        _sets.Add(new SetSpec(ExpressionTranslator.MemberPath(column), value, SetKind.Expression));
        return this;
    }

    /// <summary><paramref name="condition"/> 为 true 时才设置这一列。</summary>
    public UpdateCommand<T> SetIf<TValue>(bool condition, Expression<Func<T, TValue>> column, TValue value) => condition ? Set(column, value) : this;

    /// <summary>把一列设为 NULL。</summary>
    public UpdateCommand<T> SetNull<TValue>(Expression<Func<T, TValue>> column) => Set(column, default(TValue)!);

    /// <summary>自增：<c>col = col + ?</c>（对应 Java 的 increment）。</summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    public UpdateCommand<T> Increment<TValue>(Expression<Func<T, TValue>> column, TValue by)
    {
        _sets.Add(new SetSpec(ExpressionTranslator.MemberPath(column), Expression.Lambda(Expression.Constant(by, typeof(TValue)), column.Parameters), SetKind.Increment));
        return this;
    }

    /// <summary>自减：<c>col = col - ?</c>（对应 Java 的 decrement）。</summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = AotJustification.Lambda)]
    public UpdateCommand<T> Decrement<TValue>(Expression<Func<T, TValue>> column, TValue by)
    {
        _sets.Add(new SetSpec(ExpressionTranslator.MemberPath(column), Expression.Lambda(Expression.Constant(by, typeof(TValue)), column.Parameters), SetKind.Decrement));
        return this;
    }

    /// <summary><paramref name="condition"/> 为 true 时才自增（对应 Java 的 setColumns(condition, c -&gt; c.increment(..))）。</summary>
    public UpdateCommand<T> IncrementIf<TValue>(bool condition, Expression<Func<T, TValue>> column, TValue by) => condition ? Increment(column, by) : this;

    /// <summary><paramref name="condition"/> 为 true 时才自减。</summary>
    public UpdateCommand<T> DecrementIf<TValue>(bool condition, Expression<Func<T, TValue>> column, TValue by) => condition ? Decrement(column, by) : this;

    /// <summary>
    /// 不追踪时的差异更新：只写 <paramref name="before"/> 与要更新的实体之间值不同的列（主键除外），没有不同就不发语句；
    /// 用 <see cref="WhereColumns"/> 时定位列取 <paramref name="before"/> 的值。只能用在更新一个实体时：
    /// <c>db.Update(after).Changes(before)</c>。显式的列策略、<see cref="SetColumns"/> 优先于它。
    /// </summary>
    public UpdateCommand<T> Changes(T before)
    {
        ArgumentNullException.ThrowIfNull(before);
        if (Entities is not [var after]) throw new InvalidOperationException("Changes 只能用在更新一个实体时：db.Update(after).Changes(before)");
        if (ReferenceEquals(before, after)) throw new ArgumentException("before 与要更新的实体是同一个对象，比不出差异：传修改前的副本", nameof(before));
        _before = before;
        return this;
    }

    /// <summary>按实体更新时写哪些列；显式指定后优先于追踪的差异（与 Java 的优先级一致：用户设置 &gt; 追踪 &gt; 默认）。</summary>
    public UpdateCommand<T> Columns(ColumnStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    /// <summary>
    /// 按实体更新时只写这些列（对应 Java 的 setColumns(o -> o.stars())），SET 按这里的顺序：<c>db.Update(topics).SetColumns(t => new { t.Stars, t.Title })</c>。
    /// 指定的列即使也在 <see cref="WhereColumns"/> 里也照常写；拦截器可以用 <c>BeforeEntityUpdateColumns</c> 再补列。
    /// </summary>
    public UpdateCommand<T> SetColumns(Expression<Func<T, object?>> columns)
    {
        _onlyColumns = ColumnPaths(columns);
        return this;
    }

    /// <summary>按实体更新时不写这些列（对应 Java 的 setIgnoreColumns）：<c>db.Update(blog).IgnoreColumns(b =&gt; new { b.CreateTime, b.CreateBy })</c>。</summary>
    public UpdateCommand<T> IgnoreColumns(Expression<Func<T, object?>> columns)
    {
        _ignoredColumns = ColumnPaths(columns);
        return this;
    }

    /// <summary>
    /// 按实体更新时用这些列定位行（代替主键，对应 Java 的 whereColumns），这些列不再出现在 SET 里。
    /// 实体被追踪时条件取追踪时的原始值，值为 null 时写 <c>IS NULL</c>：
    /// <c>db.Update(topic).WhereColumns(t => new { t.Id, t.Title })</c>。
    /// </summary>
    public UpdateCommand<T> WhereColumns(Expression<Func<T, object?>> columns)
    {
        var names = ColumnPaths(columns);
        _whereColumns = Entity.Columns.Where(c => names.Contains(c.PropertyName)).ToList();
        return this;
    }

    /// <summary>
    /// 按实体更新时这一列的值用 SQL 模板包一层（对应 Java 的 columnConfigure）：<c>{0}</c> 是列本身，<c>{1}</c> 是实体上的值，
    /// 如 <c>.ColumnSql(t => t.Stars, "ifnull({0},0)+{1}")</c> 渲染成 <c>`stars` = ifnull(`stars`,0)+?</c>。
    /// </summary>
    public UpdateCommand<T> ColumnSql<TValue>(Expression<Func<T, TValue>> column, string template)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);
        _columnSql[ExpressionTranslator.MemberPath(column)] = template;
        return this;
    }

    // 列清单里的属性路径，整个值对象展开成它的各列
    private List<string> ColumnPaths(LambdaExpression columns) =>
        ExpressionTranslator.MemberNames(columns)
            .SelectMany(path => Entity.ValueObjects.TryGetValue(path, out var valueObject) ? valueObject.Columns.Select(c => c.PropertyName) : [path])
            .ToList();

    /// <summary>渲染第一条语句（按实体更新时是第一个实体的语句），不执行；没有要写的列时抛出异常。</summary>
    public string ToSql() => ToStatement().Text;

    /// <summary>同 <see cref="ToSql"/>，连同参数。</summary>
    public SqlStatement ToStatement() => (Statements().FirstOrDefault()
        ?? throw new InvalidOperationException($"{typeof(T).Name} 的更新没有要写的列")).Statement;

    /// <summary>执行，返回受影响的总行数。追踪的实体没有变化时不发语句。</summary>
    public override async Task<long> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (Entities is not null)
            foreach (var e in Entities) Db.Interceptors.BeforeWrite(e, insert: false, Interceptors);
        var statements = Statements(routed: true).ToList();
        var rows = await ExecuteRoutedAsync(statements, s => s.Statement, async (session, items) =>
        {
            long total = 0;
            foreach (var batch in WriteBatches.Of(items, s => s.Statement, UseBatch))
            {
                var rows = await session.ExecuteBatchAsync([.. batch.Select(s => s.Statement)], cancellationToken).ConfigureAwait(false);
                // 乐观锁：写成功才把新版本写回实体，冲突（0 行）时实体保持原版本
                for (var i = 0; i < batch.Count; i++)
                    if (rows.Wrote(i) && batch[i] is { Entity: { } written, NewVersion: { } version }) Entity.VersionColumn!.Setter?.Invoke(written, version);
                total += rows.Total;
            }
            return total;
        }, cancellationToken).ConfigureAwait(false);
        // 写入成功后，追踪的实体以当前值为新的原始值
        foreach (var s in statements) s.Tracked?.Refresh();
        return rows;
    }

    // routed 为 true 时分片实体按落点每个节点一条（按条件更新时是条件裁剪出的各节点）
    private IEnumerable<RenderedUpdate> Statements(bool routed = false)
    {
        if (Entities is null) return Render(null, routed: routed);
        return Entities.SelectMany(entity => Render(entity, routed: routed));
    }

    private protected override SqlStatement RenderReturning(IReadOnlyList<ColumnMetadata> columns) => Render(null, columns).Single().Statement;

    // 按实体更新时，追踪的实体没有变化返回空（不发语句）；returning 不为 null 时带 RETURNING
    private List<RenderedUpdate> Render(T? entity, IReadOnlyList<ColumnMetadata>? returning = null, bool routed = false)
    {
        var scope = NewScope();
        var table = scope.Table;
        var update = new UpdateStatement(table);
        var tracked = entity is null ? null : Db.Tracker.Find(entity);
        if (entity is not null) AddEntitySets(update, table, entity, tracked);
        AddExpressionSets(update, scope);
        if (entity is null && update.Sets.Count > 0) AddInterceptorSets(update, table);
        if (update.Sets.Count == 0)
        {
            // 按实体更新时没有要写的列（追踪无变化、按列策略全被筛掉）不发语句，受影响 0 行（与 Java 一致）
            if (entity is not null) return [];
            throw new InvalidOperationException($"{typeof(T).Name} 的更新没有要写的列");
        }
        var newVersion = AddVersionSet(update, table, entity);
        update.Where.AddRange(BuildWhere(scope, entity, applyLogicDelete: true));
        update.Joins.AddRange(scope.Joins);
        update.Returning = returning?.Select(table.Column).ToList();
        if (!routed) return [new RenderedUpdate(SqlRenderer.Render(Db.Options.Dialect, update), tracked, entity, newVersion)];
        // 行按旧的分片键所在的表定位，改了分片键也不会迁移到新表
        if (tracked is not null && ShardRule is { } rule && tracked.ChangedColumns().FirstOrDefault(c => rule.KeyProperties.Contains(c.PropertyName)) is { } changedKey)
            throw new InvalidOperationException($"{typeof(T).Name} 的分片键 {changedKey.PropertyName} 改了：分库分表不会把行迁移到新的表，请先删除再插入");
        return [.. Routed(table, update.Where, update.Joins, update.Sets.Select(s => s.Value), entity, routes => SqlRenderer.Render(Db.Options.Dialect, update, routes))
            .Select(statement => new RenderedUpdate(statement, tracked, entity, newVersion))];
    }

    private void AddEntitySets(UpdateStatement update, EntityTableSource table, T entity, Tracking.TrackedEntry? tracked)
    {
        var skipped = _sets.Select(s => s.Column).ToHashSet(StringComparer.Ordinal);
        // 显式策略、显式列优先；否则 Changes 给出的原值、追踪的快照只写变化的列（与 Java 的优先级一致：用户设置 > 追踪 > 默认）
        var changed = _strategy is not null || _onlyColumns is not null ? null
            : _before is not null ? Entity.Columns.Where(c => !c.IsPrimaryKey && !Equals(c.GetProviderValue(_before), c.GetProviderValue(entity))).ToHashSet()
            : tracked?.ChangedColumns().ToHashSet();
        // 定位列不写进 SET；差异更新时定位列取原始值，改过的定位列照常写；显式指定的列照常写（与 Java 一致）
        if (_whereColumns is not null && changed is null && _onlyColumns is null) skipped.UnionWith(_whereColumns.Select(c => c.PropertyName));
        // 没有显式策略、也不是差异更新时用全局的默认策略（Java 的 updateStrategy）
        var strategy = _strategy ?? (changed is null && _onlyColumns is null ? Db.Options.UpdateStrategy : ColumnStrategy.All);
        foreach (var column in CandidateColumns())
        {
            if (column.IsPrimaryKey || column.IsGenerated || skipped.Contains(column.PropertyName)) continue;
            // 版本列由乐观锁单独写；生效的逻辑删除列已在条件里（Java 不写条件里出现的列）
            if (column == Entity.VersionColumn || (column == Entity.LogicDeleteColumn && !LogicDeleteDisabled)) continue;
            if (_ignoredColumns is not null && _ignoredColumns.Contains(column.PropertyName)) continue;
            if (changed is not null && !changed.Contains(column)) continue;
            // 忽略更新的列；标了 WhenTrackedChanged 的在差异更新里照常写
            if (column.UpdateIgnore && !(changed is not null && column.UpdateWhenTrackedChanged)) continue;
            var value = column.GetProviderValue(entity);
            if (strategy == ColumnStrategy.OnlyNotNull && value is null) continue;
            if (strategy == ColumnStrategy.OnlyNull && value is not null) continue;
            var target = table.Column(column);
            var parameter = ExpressionTranslator.WriteValue(column, value);
            if (_columnSql.TryGetValue(column.PropertyName, out var template)) parameter = new FragmentExpr(template, [target, parameter]);
            update.Sets.Add((target, parameter));
        }
    }

    // 要写的候选列：SetColumns 指定时按指定的顺序，拦截器补的列在后面；否则是实体的全部列
    private IEnumerable<ColumnMetadata> CandidateColumns() =>
        _onlyColumns is null
            ? Entity.Columns
            : Db.Interceptors.EntityUpdateColumns(typeof(T), _onlyColumns, Interceptors).Select(Entity.GetColumn);

    // WhereColumns 指定时按这些列定位（追踪的实体取原始值），否则按主键
    private protected override IEnumerable<SqlExpr> EntityPredicates(EntityTableSource table, T entity)
    {
        if (_whereColumns is null) return base.EntityPredicates(table, entity);
        var tracked = Db.Tracker.Find(entity);
        return _whereColumns.Select(c =>
        {
            var value = _before is not null ? c.GetProviderValue(_before) : tracked is not null ? tracked.Original[c] : c.GetProviderValue(entity);
            return value is null
                ? (SqlExpr)new IsNullExpr(table.Column(c), false)
                : new BinaryExpr(table.Column(c), SqlBinaryOperator.Equal, ExpressionTranslator.WriteValue(c, value));
        }).ToList();
    }

    private sealed record RenderedUpdate(SqlStatement Statement, Tracking.TrackedEntry? Tracked, T? Entity, object? NewVersion);

    // 按条件更新：拦截器追加的列（修改人、修改时间…）写在用户的列之后，用户已经 Set 过的列跳过
    private void AddInterceptorSets(UpdateStatement update, EntityTableSource table)
    {
        foreach (var (property, value) in Db.Interceptors.ConditionUpdateSets(typeof(T), _sets.Select(s => s.Column), Interceptors))
        {
            var column = Entity.GetColumn(property);
            update.Sets.Add((table.Column(column), ExpressionTranslator.ParameterFromModel(column, value)));
        }
    }

    // 值里可以用导航（对一导航补出 JOIN），与条件共用翻译作用域
    private void AddExpressionSets(UpdateStatement update, WriteScope scope)
    {
        foreach (var set in _sets)
        {
            if (ShardRule is { } rule && rule.KeyProperties.Contains(set.Column))
                throw new InvalidOperationException($"{typeof(T).Name}.{set.Column} 是分片键，不能在 Set 里改：分库分表不会把行迁移到新的表，请先删除再插入");
            var column = scope.Table.Column(Entity.GetColumn(set.Column));
            var value = ExpressionTranslator.ApplyConverter(scope.Translate(set.Value, predicate: false), column, write: true);
            update.Sets.Add((column, set.Kind switch
            {
                // 自增 / 自减不加括号：`star` = `star` + ?（Java 版的写法）
                SetKind.Increment => new FragmentExpr("{0} + {1}", [column, value]),
                SetKind.Decrement => new FragmentExpr("{0} - {1}", [column, value]),
                _ => value,
            }));
        }
    }

    private enum SetKind
    {
        Expression,
        Increment,
        Decrement,
    }

    // Column 是属性路径（值对象里的列如 Address.City）
    private sealed record SetSpec(string Column, LambdaExpression Value, SetKind Kind);
}
