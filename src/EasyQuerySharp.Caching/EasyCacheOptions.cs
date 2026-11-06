using System.Text.Json;

namespace EasyQuerySharp.Caching;

/// <summary><see cref="EasyCache"/> 的配置：键前缀、过期时间与要缓存的实体（见 0007）。</summary>
public sealed class EasyCacheOptions
{
    private readonly List<Type> _entities = [];

    /// <summary>键的前缀，默认 <c>EQS</c>。几个库共用一个 HybridCache 时各用各的前缀（键里只有表名，不区分库）。</summary>
    public string KeyPrefix { get; set; } = "EQS";

    /// <summary>缓存的过期时间（分布式层，以及没有设置 <see cref="LocalCacheExpiration"/> 时的本机层），默认 5 分钟。</summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 本机层的过期时间，默认同 <see cref="Expiration"/>。别的实例写入后，本实例的本机层最多再旧这么久（1.0 不带多实例的失效通知），
    /// 多实例部署时按能接受的陈旧时间调小。
    /// </summary>
    public TimeSpan? LocalCacheExpiration { get; set; }

    /// <summary>"查无此行"的缓存时间（防止反复查不存在的主键穿透到库），默认 10 秒。</summary>
    public TimeSpan NullExpiration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 写入后隔多久再失效一次（延迟双删），挡住读到旧值的慢请求在失效之后把旧值写回；默认不做。
    /// 第二次失效在后台执行，失败时不报告（由过期时间兜底）。
    /// </summary>
    public TimeSpan? LateEvictDelay { get; set; }

    /// <summary>实体序列化的选项（System.Text.Json）；为 null 时用默认选项。只序列化实体自己的列，导航属性等总是排除。</summary>
    public JsonSerializerOptions? Json { get; set; }

    /// <summary>登记要缓存的实体：只有登记过的实体能用 <c>Kv&lt;T&gt;()</c> / <c>All&lt;T&gt;()</c>，写入它们的表时失效。</summary>
    public EasyCacheOptions Cache<T>() where T : class
    {
        if (!_entities.Contains(typeof(T))) _entities.Add(typeof(T));
        return this;
    }

    internal IReadOnlyList<Type> Entities => _entities;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(KeyPrefix, nameof(KeyPrefix));
        Positive(Expiration, nameof(Expiration));
        Positive(NullExpiration, nameof(NullExpiration));
        if (LocalCacheExpiration is { } local) Positive(local, nameof(LocalCacheExpiration));
        if (LateEvictDelay is { } late) Positive(late, nameof(LateEvictDelay));
    }

    private static void Positive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(name, value, $"{name} 必须大于 0");
    }
}
