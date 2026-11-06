using EasyQuerySharp.Querying;

namespace EasyQuerySharp;

/// <summary>
/// 自动映射（<c>Select&lt;TDto&gt;()</c>）里嵌套的 DTO 被带出时，给带出查询加条件、排序、条数（对应 Java DTO 上的静态字段 EXTRA_AUTO_INCLUDE_CONFIGURE）。
/// DTO 实现这个接口，<typeparamref name="TEntity"/> 是它映射自的实体。
/// </summary>
/// <example><code>
/// public sealed class CardDto : IAutoIncludeConfigure&lt;SysBankCard&gt;
/// {
///     public string? Code { get; set; }
///     public static Query&lt;SysBankCard&gt; Configure(Query&lt;SysBankCard&gt; query) =&gt; query.Where(c =&gt; c.Type == "储蓄卡");
/// }
/// </code></example>
public interface IAutoIncludeConfigure<TEntity>
{
    /// <summary>配置带出这个 DTO 时的查询（在按父键过滤之前）。</summary>
    static abstract Query<TEntity> Configure(Query<TEntity> query);
}
