using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EasyQuerySharp.Execution;
using Microsoft.Extensions.Caching.Hybrid;

namespace EasyQuerySharp.Caching;

/// <summary>
/// 键与桶的布局（0007 §4）与对 HybridCache 的读写，只用 GetOrCreateAsync / SetAsync / RemoveAsync，不用标签：
/// <code>
/// {前缀}:{表}:epoch              表纪元（随机令牌），整表失效时换新，旧纪元的桶随过期消亡
/// {前缀}:{表}:{纪元}:r:{主键}     行桶：{ 变体键 → CachedItem }
/// {前缀}:{表}:{纪元}:i           索引桶：{ 变体键 → CachedIndex }
/// </code>
/// 桶存成 JSON 文本（字符串是 HybridCache 内置的序列化，不受使用方配置的序列化器影响）；桶里的每一项带自己的到期时间。
/// 查库不放进 GetOrCreateAsync 的工厂：工厂在不带调用方 ExecutionContext 的工作项里执行，租户这类按 AsyncLocal 取值的拦截器
/// 会看不到当前用户（HybridCacheProbeTest）。所以桶都是"探测、没有就查库、写回"。
/// </summary>
internal sealed class CacheStore
{
    // 主键超过这么多时不逐个删，改为整表换纪元
    public const int MaxKeysPerEviction = 256;

    // 纪元只是一个令牌，比桶活得久，桶不会因为纪元先过期而整表失效；本机层仍按 LocalCacheExpiration，别的实例换的纪元在那之后看到
    private static readonly TimeSpan EpochExpiration = TimeSpan.FromDays(1);

    private static JsonTypeInfo<Dictionary<string, CachedItem>> Rows => BucketJson.Default.DictionaryStringCachedItem;

    private static JsonTypeInfo<Dictionary<string, CachedIndex>> Indexes => BucketJson.Default.DictionaryStringCachedIndex;

    private readonly HybridCache _cache;
    private readonly string _prefix;
    private readonly TimeSpan _local;
    private readonly HybridCacheEntryOptions _epochOptions;
    private readonly HybridCacheEntryOptions _probeOptions;

    public CacheStore(HybridCache cache, EasyCacheOptions options)
    {
        _cache = cache;
        _prefix = options.KeyPrefix;
        Expiration = options.Expiration;
        NullExpiration = options.NullExpiration;
        _local = options.LocalCacheExpiration ?? options.Expiration;
        _epochOptions = new HybridCacheEntryOptions { Expiration = EpochExpiration, LocalCacheExpiration = _local };
        // 从 L2 读进本机层时，本机层的过期时间取这次调用的选项（不带就是 HybridCache 的默认值）
        _probeOptions = new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableUnderlyingData, Expiration = Expiration, LocalCacheExpiration = _local };
    }

    public TimeSpan Expiration { get; }

    public TimeSpan NullExpiration { get; }

    public static long Now() => TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds();

    /// <summary>当前的表纪元，没有时生成一个（工厂只生成随机令牌，不查库）。</summary>
    public ValueTask<string> EpochAsync(string table, CancellationToken ct) =>
        _cache.GetOrCreateAsync(EpochKey(table), static _ => ValueTask.FromResult(NewEpoch()), _epochOptions, cancellationToken: ct);

    /// <summary>行桶里这个变体的缓存项；没有或已过期时为 null。</summary>
    public ValueTask<CachedItem?> GetRowAsync(string table, string epoch, string key, string variant, CancellationToken ct) =>
        GetAsync(RowKey(table, epoch, key), variant, Rows, ct);

    /// <summary>把一个变体的缓存项并进行桶。</summary>
    public Task SetRowAsync(string table, string epoch, string key, string variant, CachedItem item, CancellationToken ct) =>
        MergeAsync(RowKey(table, epoch, key), variant, item, Rows, ct);

    /// <summary>索引桶里这个变体（条件加排序）的主键列表；没有或已过期时为 null。</summary>
    public ValueTask<CachedIndex?> GetIndexAsync(string table, string epoch, string variant, CancellationToken ct) =>
        GetAsync(IndexKey(table, epoch), variant, Indexes, ct);

    /// <summary>把一个变体的主键列表并进索引桶。</summary>
    public Task SetIndexAsync(string table, string epoch, string variant, CachedIndex index, CancellationToken ct) =>
        MergeAsync(IndexKey(table, epoch), variant, index, Indexes, ct);

    /// <summary>
    /// 按 0007 §7 失效：主键已知且不多时删这些行桶与索引桶；只有插入且主键不明时只删索引桶；其余换新表纪元。
    /// 删的是当前纪元下的桶（旧纪元的桶已经读不到）；还没有纪元说明这张表什么也没缓存。
    /// </summary>
    public async Task InvalidateAsync(string table, WriteKind kind, IReadOnlyCollection<string>? keys, CancellationToken ct)
    {
        if ((keys is null && kind != WriteKind.Insert) || keys?.Count > MaxKeysPerEviction)
        {
            await _cache.SetAsync(EpochKey(table), NewEpoch(), _epochOptions, cancellationToken: ct).ConfigureAwait(false);
            return;
        }
        if (await ProbeAsync(EpochKey(table), ct).ConfigureAwait(false) is not { } epoch) return;
        if (keys is null || keys.Count == 0)
        {
            await _cache.RemoveAsync(IndexKey(table, epoch), ct).ConfigureAwait(false);
            return;
        }
        await _cache.RemoveAsync([.. keys.Select(k => RowKey(table, epoch, k)), IndexKey(table, epoch)], ct).ConfigureAwait(false);
    }

    private string EpochKey(string table) => $"{_prefix}:{table}:epoch";

    private string RowKey(string table, string epoch, string key) => $"{_prefix}:{table}:{epoch}:r:{key}";

    private string IndexKey(string table, string epoch) => $"{_prefix}:{table}:{epoch}:i";

    private static string NewEpoch() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

    // 只读不建：没有时返回 null，不调用工厂也不写入
    private ValueTask<string?> ProbeAsync(string key, CancellationToken ct) =>
        _cache.GetOrCreateAsync<string?>(key, static _ => ValueTask.FromResult<string?>(null), _probeOptions, cancellationToken: ct);

    private async ValueTask<T?> GetAsync<T>(string key, string variant, JsonTypeInfo<Dictionary<string, T>> type, CancellationToken ct) where T : class, IExpiring
    {
        if (await ProbeAsync(key, ct).ConfigureAwait(false) is not { } json) return null;
        return Read(json, type).TryGetValue(variant, out var item) && item.ExpiresAt > Now() ? item : null;
    }

    // 读出桶、清掉过期的项、放进这一项再写回；桶的过期时间取最晚到期的项，本机层不超过 LocalCacheExpiration。
    // 两个请求同时并进同一个桶时后写的覆盖先写的，丢掉的变体下次再查库，不会读到错的值
    private async Task MergeAsync<T>(string key, string variant, T item, JsonTypeInfo<Dictionary<string, T>> type, CancellationToken ct) where T : class, IExpiring
    {
        var now = Now();
        var bucket = await ProbeAsync(key, ct).ConfigureAwait(false) is { } json ? Read(json, type) : new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var (name, existing) in bucket)
            if (existing.ExpiresAt <= now) bucket.Remove(name);
        bucket[variant] = item;
        var expiration = TimeSpan.FromMilliseconds(Math.Max(bucket.Values.Max(i => i.ExpiresAt) - now, 1));
        var options = new HybridCacheEntryOptions { Expiration = expiration, LocalCacheExpiration = expiration < _local ? expiration : _local };
        await _cache.SetAsync(key, JsonSerializer.Serialize(bucket, type), options, cancellationToken: ct).ConfigureAwait(false);
    }

    private static Dictionary<string, T> Read<T>(string json, JsonTypeInfo<Dictionary<string, T>> type) =>
        JsonSerializer.Deserialize(json, type) is { } bucket ? new Dictionary<string, T>(bucket, StringComparer.Ordinal) : new(StringComparer.Ordinal);
}

internal interface IExpiring
{
    long ExpiresAt { get; }
}

/// <summary>行桶里的一项：<see cref="Json"/> 为 null 表示这个条件下查无此行。<see cref="ExpiresAt"/> 是 Unix 毫秒。</summary>
internal sealed record CachedItem(string? Json, long ExpiresAt) : IExpiring;

/// <summary>索引桶里的一项：这个条件（含排序）下的主键文本，按顺序。</summary>
internal sealed record CachedIndex(List<string> Keys, long ExpiresAt) : IExpiring;

[JsonSerializable(typeof(Dictionary<string, CachedItem>))]
[JsonSerializable(typeof(Dictionary<string, CachedIndex>))]
internal sealed partial class BucketJson : JsonSerializerContext;
