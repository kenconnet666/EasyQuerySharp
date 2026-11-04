using System.Linq.Expressions;

namespace EasyQuerySharp.Interceptors;

/// <summary>
/// 拦截器的公共部分。<see cref="Name"/> 用于在单条查询上关闭（<c>DisableInterceptor("name")</c>）；
/// <see cref="TargetType"/> 决定作用于哪些实体：实体类型本身、它的基类或它实现的接口都算
/// （例如 <c>QueryFilter&lt;ITenantEntity&gt;</c> 作用于所有租户实体）。
/// </summary>
public interface IInterceptor
{
    /// <summary>名字，默认是类型名。</summary>
    string Name { get; }

    /// <summary>作用的实体类型（或其基类 / 接口）。</summary>
    Type TargetType { get; }

    /// <summary>默认是否生效（对应 Java 的 enable()）；为 false 时只在 <c>EnableInterceptor(name)</c> 的那条语句上生效。</summary>
    bool EnabledByDefault => true;
}

/// <summary>非泛型的查询过滤入口，供注册表调用。</summary>
internal interface IQueryFilterInterceptor : IInterceptor
{
    LambdaExpression? FilterLambda();
}

/// <summary>非泛型的写入前拦截入口，供注册表调用。</summary>
internal interface IEntityWriteInterceptor : IInterceptor
{
    void BeforeInsert(object entity);

    void BeforeUpdate(object entity);

    void BeforeConditionUpdate(UpdateSetCollector collector);
}

/// <summary>按条件更新时拦截器追加的 SET：属性名 → 值；已经要写的列（用户 Set 过、前面的拦截器加过）不再追加。</summary>
internal sealed class UpdateSetCollector(IEnumerable<string> assigned)
{
    private readonly HashSet<string> _assigned = new(assigned, StringComparer.Ordinal);

    /// <summary>按追加顺序的 SET。</summary>
    public List<(string Property, object? Value)> Sets { get; } = [];

    public bool IsSet(string property) => _assigned.Contains(property);

    public void Set(string property, object? value)
    {
        if (_assigned.Add(property)) Sets.Add((property, value));
    }
}

/// <summary>
/// 按条件更新（<c>db.Update&lt;T&gt;().Set(..)</c>）时追加 SET 的入口，见 <see cref="EntityInterceptor{TEntity}.BeforeConditionUpdate"/>。
/// 列用属性访问表达式指定，<typeparamref name="TEntity"/> 可以是接口（按属性名对应到实体的列）。
/// </summary>
public sealed class UpdateSetBuilder<TEntity>
{
    private readonly UpdateSetCollector _collector;

    internal UpdateSetBuilder(UpdateSetCollector collector) => _collector = collector;

    /// <summary>这一列是否已经要写（用户 Set 过，或前面的拦截器加过）。</summary>
    public bool IsSet<TValue>(Expression<Func<TEntity, TValue>> column) => _collector.IsSet(PropertyOf(column));

    /// <summary>追加 <c>列 = 值</c>；这一列已经要写时忽略（与 Java 的"是否已经 set 了"检查一致）。</summary>
    public UpdateSetBuilder<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> column, TValue value)
    {
        _collector.Set(PropertyOf(column), value);
        return this;
    }

    private static string PropertyOf(LambdaExpression column) =>
        (column.Body is UnaryExpression { NodeType: ExpressionType.Convert } u ? u.Operand : column.Body) is MemberExpression m
            ? m.Member.Name
            : throw new ArgumentException($"需要直接的属性访问，如 e => e.UpdateBy：{column}", nameof(column));
}

/// <summary>
/// 查询过滤（对应 Java 的 PredicateFilterInterceptor）：对目标实体的每次查询、更新、删除自动追加条件，
/// 条件写在逻辑删除条件之后。<see cref="Filter"/> 每次执行时调用，可以读取当前上下文（如当前租户）。
/// </summary>
/// <example><code>
/// sealed class TenantFilter(ITenantAccessor tenant) : QueryFilter&lt;ITenantEntity&gt;
/// {
///     public override Expression&lt;Func&lt;ITenantEntity, bool&gt;&gt;? Filter() => e => e.TenantId == tenant.Current;
/// }
/// </code></example>
public abstract class QueryFilter<TEntity> : IQueryFilterInterceptor
{
    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public Type TargetType => typeof(TEntity);

    /// <inheritdoc />
    public virtual bool EnabledByDefault => true;

    /// <summary>这次执行要追加的条件；返回 null 表示这次不加。</summary>
    public abstract Expression<Func<TEntity, bool>>? Filter();

    LambdaExpression? IQueryFilterInterceptor.FilterLambda() => Filter();
}

/// <summary>
/// 写入前修改实体（对应 Java 的 EntityInterceptor）：插入、按实体更新之前调用，常用于填写创建人、修改时间等；
/// 按条件更新没有实体，用 <see cref="BeforeConditionUpdate"/> 追加 SET（对应 Java 的 UpdateSetInterceptor）。
/// </summary>
public abstract class EntityInterceptor<TEntity> : IEntityWriteInterceptor
{
    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public Type TargetType => typeof(TEntity);

    /// <inheritdoc />
    public virtual bool EnabledByDefault => true;

    /// <summary>插入前调用。</summary>
    public virtual void BeforeInsert(TEntity entity)
    {
    }

    /// <summary>按实体更新前调用。</summary>
    public virtual void BeforeUpdate(TEntity entity)
    {
    }

    /// <summary>
    /// 按条件更新（<c>db.Update&lt;T&gt;().Set(..)</c>）时调用，追加要写的列，如
    /// <c>sets.Set(e =&gt; e.UpdateBy, user).Set(e =&gt; e.UpdateTime, DateTime.Now)</c>；用户已经 Set 过的列自动跳过。
    /// </summary>
    public virtual void BeforeConditionUpdate(UpdateSetBuilder<TEntity> sets)
    {
    }

    void IEntityWriteInterceptor.BeforeInsert(object entity) => BeforeInsert((TEntity)entity);

    void IEntityWriteInterceptor.BeforeUpdate(object entity) => BeforeUpdate((TEntity)entity);

    void IEntityWriteInterceptor.BeforeConditionUpdate(UpdateSetCollector collector) => BeforeConditionUpdate(new UpdateSetBuilder<TEntity>(collector));
}
