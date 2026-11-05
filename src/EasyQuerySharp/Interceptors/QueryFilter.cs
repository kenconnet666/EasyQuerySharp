using System.Linq.Expressions;

namespace EasyQuerySharp.Interceptors;

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

    /// <inheritdoc />
    public virtual bool IsProtected => false;

    /// <summary>这次执行要追加的条件；返回 null 表示这次不加。</summary>
    public abstract Expression<Func<TEntity, bool>>? Filter();

    LambdaExpression? IQueryFilterInterceptor.FilterLambda() => Filter();
}
