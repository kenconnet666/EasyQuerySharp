using System.Linq.Expressions;
using System.Reflection;
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
    private HashSet<string>? _onlyColumns;
    private HashSet<string>? _ignoredColumns;

    internal UpdateCommand(EasyDb db, IReadOnlyList<T>? entities) : base(db, entities)
    {
    }

    /// <summary>用成员初始化表达式设置多列，值可以引用原列：<c>t => new Topic { Stars = t.Stars + 1 }</c>。</summary>
    public UpdateCommand<T> Set(Expression<Func<T, T>> setter)
    {
        if (setter.Body is not MemberInitExpression init)
            throw new ArgumentException("Set 需要成员初始化表达式，如 t => new Topic { Title = \"x\" }", nameof(setter));
        foreach (var binding in init.Bindings.Cast<MemberAssignment>())
            _sets.Add(new SetSpec(binding.Member, Expression.Lambda(binding.Expression, setter.Parameters), SetKind.Expression));
        return this;
    }

    /// <summary>把一列设为常量值。</summary>
    public UpdateCommand<T> Set<TValue>(Expression<Func<T, TValue>> column, TValue value)
    {
        _sets.Add(new SetSpec(MemberOf(column), Expression.Lambda(Expression.Constant(value, typeof(TValue)), column.Parameters), SetKind.Expression));
        return this;
    }

    /// <summary>把一列设为表达式的值（可以引用原列）。</summary>
    public UpdateCommand<T> Set<TValue>(Expression<Func<T, TValue>> column, Expression<Func<T, TValue>> value)
    {
        _sets.Add(new SetSpec(MemberOf(column), value, SetKind.Expression));
        return this;
    }

    /// <summary><paramref name="condition"/> 为 true 时才设置这一列。</summary>
    public UpdateCommand<T> SetIf<TValue>(bool condition, Expression<Func<T, TValue>> column, TValue value) => condition ? Set(column, value) : this;

    /// <summary>把一列设为 NULL。</summary>
    public UpdateCommand<T> SetNull<TValue>(Expression<Func<T, TValue>> column) => Set(column, default(TValue)!);

    /// <summary>自增：<c>col = col + ?</c>（对应 Java 的 increment）。</summary>
    public UpdateCommand<T> Increment<TValue>(Expression<Func<T, TValue>> column, TValue by)
    {
        _sets.Add(new SetSpec(MemberOf(column), Expression.Lambda(Expression.Constant(by, typeof(TValue)), column.Parameters), SetKind.Increment));
        return this;
    }

    /// <summary>自减：<c>col = col - ?</c>（对应 Java 的 decrement）。</summary>
    public UpdateCommand<T> Decrement<TValue>(Expression<Func<T, TValue>> column, TValue by)
    {
        _sets.Add(new SetSpec(MemberOf(column), Expression.Lambda(Expression.Constant(by, typeof(TValue)), column.Parameters), SetKind.Decrement));
        return this;
    }

    /// <summary>按实体更新时写哪些列；显式指定后优先于追踪的差异（与 Java 的优先级一致：用户设置 &gt; 追踪 &gt; 默认）。</summary>
    public UpdateCommand<T> Columns(ColumnStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    /// <summary>
    /// 按实体更新时只写这些列（对应 Java 的 setColumns(o -> o.stars())）：<c>db.Update(topics).SetColumns(t => new { t.Stars, t.Title })</c>。
    /// </summary>
    public UpdateCommand<T> SetColumns(Expression<Func<T, object?>> columns)
    {
        _onlyColumns = Querying.AutoProjection.MemberNames(columns);
        return this;
    }

    /// <summary>按实体更新时不写这些列（对应 Java 的 setIgnoreColumns）：<c>db.Update(blog).IgnoreColumns(b =&gt; new { b.CreateTime, b.CreateBy })</c>。</summary>
    public UpdateCommand<T> IgnoreColumns(Expression<Func<T, object?>> columns)
    {
        _ignoredColumns = Querying.AutoProjection.MemberNames(columns);
        return this;
    }

    /// <summary>
    /// 按实体更新时用这些列定位行（代替主键，对应 Java 的 whereColumns），这些列不再出现在 SET 里。
    /// 实体被追踪时条件取追踪时的原始值，值为 null 时写 <c>IS NULL</c>：
    /// <c>db.Update(topic).WhereColumns(t => new { t.Id, t.Title })</c>。
    /// </summary>
    public UpdateCommand<T> WhereColumns(Expression<Func<T, object?>> columns)
    {
        var names = Querying.AutoProjection.MemberNames(columns);
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
        _columnSql[MemberOf(column).Name] = template;
        return this;
    }

    /// <summary>渲染第一条语句（按实体更新时是第一个实体的语句），不执行；没有要写的列时抛出异常。</summary>
    public string ToSql() => (Statements().FirstOrDefault()
        ?? throw new InvalidOperationException($"{typeof(T).Name} 的更新没有要写的列")).Statement.Text;

    /// <summary>执行，返回受影响的总行数。追踪的实体没有变化时不发语句。</summary>
    public override async Task<long> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (Entities is not null)
            foreach (var e in Entities) Db.Interceptors.BeforeWrite(e, insert: false, Interceptors);
        var statements = Statements().ToList();
        var rows = await Db.Executor.SessionAsync(async session =>
        {
            long total = 0;
            foreach (var s in statements) total += await session.ExecuteAsync(s.Statement, cancellationToken).ConfigureAwait(false);
            return total;
        }, cancellationToken).ConfigureAwait(false);
        // 写入成功后，追踪的实体以当前值为新的原始值
        foreach (var s in statements) s.Tracked?.Refresh();
        return rows;
    }

    private IEnumerable<RenderedUpdate> Statements()
    {
        if (Entities is null)
        {
            yield return Render(null)!;
            yield break;
        }
        foreach (var entity in Entities)
            if (Render(entity) is { } rendered) yield return rendered;
    }

    // 按实体更新时，追踪的实体没有变化返回 null（不发语句）
    private RenderedUpdate? Render(T? entity)
    {
        var table = NewTable();
        var update = new UpdateStatement(table);
        var tracked = entity is null ? null : Db.Tracker.Find(entity);
        if (entity is not null) AddEntitySets(update, table, entity, tracked);
        AddExpressionSets(update, table);
        if (update.Sets.Count == 0)
        {
            if (tracked is not null) return null;
            throw new InvalidOperationException($"{typeof(T).Name} 的更新没有要写的列");
        }
        update.Where.AddRange(BuildWhere(table, entity, applyLogicDelete: true));
        return new RenderedUpdate(SqlRenderer.Render(Db.Options.Dialect, update), tracked);
    }

    private void AddEntitySets(UpdateStatement update, EntityTableSource table, T entity, Tracking.TrackedEntry? tracked)
    {
        var skipped = _sets.Select(s => s.Member.Name).ToHashSet(StringComparer.Ordinal);
        // 显式策略优先；否则追踪的实体只写变化的列
        var changed = _strategy is null && tracked is not null ? tracked.ChangedColumns().ToHashSet() : null;
        // 定位列不写进 SET；差异更新时定位列取原始值，改过的定位列照常写（与 Java 一致）
        if (_whereColumns is not null && changed is null) skipped.UnionWith(_whereColumns.Select(c => c.PropertyName));
        foreach (var column in Entity.Columns)
        {
            if (column.IsPrimaryKey || column.IsGenerated || skipped.Contains(column.PropertyName)) continue;
            if (_onlyColumns is not null && !_onlyColumns.Contains(column.PropertyName)) continue;
            if (_ignoredColumns is not null && _ignoredColumns.Contains(column.PropertyName)) continue;
            if (changed is not null && !changed.Contains(column)) continue;
            // 忽略更新的列；标了 WhenTrackedChanged 的在差异更新里照常写
            if (column.UpdateIgnore && !(changed is not null && column.UpdateWhenTrackedChanged)) continue;
            var value = column.GetProviderValue(entity);
            if (_strategy == ColumnStrategy.OnlyNotNull && value is null) continue;
            if (_strategy == ColumnStrategy.OnlyNull && value is not null) continue;
            var target = table.Column(column);
            SqlExpr parameter = ExpressionTranslator.Parameter(value, column.Converter?.ProviderType ?? column.ClrType);
            if (_columnSql.TryGetValue(column.PropertyName, out var template)) parameter = new FragmentExpr(template, [target, parameter]);
            update.Sets.Add((target, parameter));
        }
    }

    // WhereColumns 指定时按这些列定位（追踪的实体取原始值），否则按主键
    private protected override IEnumerable<SqlExpr> EntityPredicates(EntityTableSource table, T entity)
    {
        if (_whereColumns is null) return base.EntityPredicates(table, entity);
        var tracked = Db.Tracker.Find(entity);
        return _whereColumns.Select(c =>
        {
            var value = tracked is not null ? tracked.Original[c] : c.GetProviderValue(entity);
            return value is null
                ? (SqlExpr)new IsNullExpr(table.Column(c), false)
                : new BinaryExpr(table.Column(c), SqlBinaryOperator.Equal, ExpressionTranslator.Parameter(value, c.Converter?.ProviderType ?? c.ClrType));
        }).ToList();
    }

    private sealed record RenderedUpdate(SqlStatement Statement, Tracking.TrackedEntry? Tracked);

    private void AddExpressionSets(UpdateStatement update, EntityTableSource table)
    {
        foreach (var set in _sets)
        {
            var column = table.Column(Entity.GetColumn(set.Member.Name));
            var translator = new ExpressionTranslator([]);
            translator.Bind(set.Value, [new EntityBinding(table)]);
            var value = ExpressionTranslator.ApplyConverter(translator.Translate(set.Value.Body), column);
            update.Sets.Add((column, set.Kind switch
            {
                // 自增 / 自减不加括号：`star` = `star` + ?（Java 版的写法）
                SetKind.Increment => new FragmentExpr("{0} + {1}", [column, value]),
                SetKind.Decrement => new FragmentExpr("{0} - {1}", [column, value]),
                _ => value,
            }));
        }
    }

    private static MemberInfo MemberOf(LambdaExpression column) =>
        ExpressionTranslator.StripConvert(column.Body) is MemberExpression m
            ? m.Member
            : throw new ArgumentException($"需要直接的属性访问，如 t => t.Title：{column}", nameof(column));

    private enum SetKind
    {
        Expression,
        Increment,
        Decrement,
    }

    private sealed record SetSpec(MemberInfo Member, LambdaExpression Value, SetKind Kind);
}
