using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace EasyQuerySharp.Tests.Upstream.Support;

/// <summary>二级缓存测试的存储：每次新建一个 HybridCache 就是一个应用实例，传入同一个 <see cref="SharedDistributedCache"/> 时它们共享 L2。</summary>
public static class CacheSupport
{
    /// <summary>新建一个 HybridCache（自己的本机层）；<paramref name="l2"/> 为 null 时只有本机层。</summary>
    public static HybridCache NewHybridCache(SharedDistributedCache? l2 = null)
    {
        var services = new ServiceCollection();
        if (l2 is not null) services.AddSingleton<IDistributedCache>(l2);
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}

/// <summary>
/// 进程内的 <see cref="IDistributedCache"/>，模拟多个实例共用的 Redis。不能用 MemoryDistributedCache：
/// HybridCache 把它当作没有 L2（见 HybridCacheProbeTest）。
/// </summary>
public sealed class SharedDistributedCache : IDistributedCache
{
    private readonly ConcurrentDictionary<string, (byte[] Value, DateTimeOffset? Expires)> _items = new(StringComparer.Ordinal);

    /// <summary>键是否在（且没过期）。</summary>
    public bool Contains(string key) => Get(key) is not null;

    /// <summary>当前的键（含已过期未清理的）。</summary>
    public IReadOnlyCollection<string> Keys => [.. _items.Keys];

    public byte[]? Get(string key) =>
        _items.TryGetValue(key, out var item) && (item.Expires is null || item.Expires > DateTimeOffset.UtcNow) ? item.Value : null;

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = options.AbsoluteExpiration
                      ?? (options.AbsoluteExpirationRelativeToNow is { } relative ? now + relative : (DateTimeOffset?)null)
                      ?? (options.SlidingExpiration is { } sliding ? now + sliding : (DateTimeOffset?)null);
        _items[key] = (value, expires);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => _items.TryRemove(key, out _);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
