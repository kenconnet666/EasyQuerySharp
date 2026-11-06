using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 二级缓存依赖的 HybridCache 行为（0007 的调研里列为风险的几条），换版本时先看这里：
/// 只探测不创建、删除同时清本机层与 L2、写入同时写 L2、MemoryDistributedCache 不算 L2。
/// </summary>
public sealed class HybridCacheProbeTest
{
    private static readonly HybridCacheEntryOptions Probe = new() { Flags = HybridCacheEntryFlags.DisableUnderlyingData };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ValueTask<string?> ProbeAsync(HybridCache cache, string key) =>
        cache.GetOrCreateAsync<string?>(key, _ => throw new InvalidOperationException("探测不应调用工厂"), Probe, cancellationToken: Ct);

    // DisableUnderlyingData：没有时返回 default、不调用工厂、也不写入
    [Fact]
    public async Task ProbeReturnsDefaultOnMissWithoutCreating()
    {
        var l2 = new SharedDistributedCache();
        var cache = CacheSupport.NewHybridCache(l2);
        Assert.Null(await ProbeAsync(cache, "k"));
        Assert.Null(await ProbeAsync(cache, "k"));
        Assert.Empty(l2.Keys);

        Assert.Equal("v", await cache.GetOrCreateAsync("k", _ => ValueTask.FromResult("v"), cancellationToken: Ct));
        Assert.Equal("v", await ProbeAsync(cache, "k"));
    }

    // SetAsync 同时写本机层与 L2：另一个实例从 L2 读到
    [Fact]
    public async Task SetWritesThroughToL2()
    {
        var l2 = new SharedDistributedCache();
        var a = CacheSupport.NewHybridCache(l2);
        var b = CacheSupport.NewHybridCache(l2);
        await a.SetAsync("k", "v", cancellationToken: Ct);
        Assert.True(l2.Contains("k"));
        Assert.Equal("v", await ProbeAsync(b, "k"));
    }

    // RemoveAsync 清掉自己的本机层与 L2；别的实例的本机层还在（0007 §8 的限制：最多再旧 LocalCacheExpiration）
    [Fact]
    public async Task RemoveClearsLocalAndL2ButNotOtherInstances()
    {
        var l2 = new SharedDistributedCache();
        var a = CacheSupport.NewHybridCache(l2);
        var b = CacheSupport.NewHybridCache(l2);
        await a.SetAsync("k", "v", cancellationToken: Ct);
        Assert.Equal("v", await ProbeAsync(b, "k"));

        await a.RemoveAsync("k", Ct);
        Assert.False(l2.Contains("k"));
        Assert.Null(await ProbeAsync(a, "k"));
        Assert.Equal("v", await ProbeAsync(b, "k"));
        Assert.Null(await ProbeAsync(CacheSupport.NewHybridCache(l2), "k"));
    }

    // 本机层的过期时间短于 L2 时，过期后从 L2 重新取到别的实例写的新值
    [Fact]
    public async Task LocalLayerExpiresBeforeL2()
    {
        var l2 = new SharedDistributedCache();
        var a = CacheSupport.NewHybridCache(l2);
        var b = CacheSupport.NewHybridCache(l2);
        var options = new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMilliseconds(200) };
        await a.SetAsync("k", "v1", options, cancellationToken: Ct);
        Assert.Equal("v1", await b.GetOrCreateAsync<string?>("k", _ => ValueTask.FromResult<string?>(null), options, cancellationToken: Ct));
        await a.SetAsync("k", "v2", options, cancellationToken: Ct);
        Assert.Equal("v1", await ProbeAsync(b, "k"));
        await Task.Delay(400, Ct);
        Assert.Equal("v2", await ProbeAsync(b, "k"));
    }

    private static readonly AsyncLocal<string?> Ambient = new();

    // 工厂在不带调用方 ExecutionContext 的工作项里执行：AsyncLocal（环境事务、追踪作用域、按当前用户取值的租户拦截器、SqlCapture）都看不到，
    // 所以缓存包不在工厂里查库
    [Fact]
    public async Task FactoryDoesNotSeeTheCallersAsyncLocals()
    {
        var cache = CacheSupport.NewHybridCache();
        Ambient.Value = "caller";
        var seen = await cache.GetOrCreateAsync("k", _ => ValueTask.FromResult(Ambient.Value ?? "(none)"), cancellationToken: Ct);
        Assert.Equal("(none)", seen);
    }

    // 本机层的过期时间取把值放进本机层的那次调用的选项：从 L2 读进本机层的探测也要带上 LocalCacheExpiration，否则用默认的
    [Fact]
    public async Task LocalExpirationComesFromTheCallThatFillsIt()
    {
        var l2 = new SharedDistributedCache();
        var a = CacheSupport.NewHybridCache(l2);
        var b = CacheSupport.NewHybridCache(l2);
        await a.SetAsync("k", "v1", new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMilliseconds(200) }, cancellationToken: Ct);
        Assert.Equal("v1", await ProbeAsync(b, "k"));
        await a.SetAsync("k", "v2", cancellationToken: Ct);
        await Task.Delay(400, Ct);
        Assert.Equal("v1", await ProbeAsync(b, "k"));
    }

    // 注册的 IDistributedCache 是 MemoryDistributedCache 时 HybridCache 不把它当 L2：两个实例互相看不到
    [Fact]
    public async Task MemoryDistributedCacheIsNotUsedAsL2()
    {
        var shared = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        HybridCache New()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDistributedCache>(shared);
            services.AddHybridCache();
            return services.BuildServiceProvider().GetRequiredService<HybridCache>();
        }

        await New().SetAsync("k", "v", cancellationToken: Ct);
        Assert.Null(await ProbeAsync(New(), "k"));
        Assert.Null(await shared.GetAsync("k", Ct));
    }
}
