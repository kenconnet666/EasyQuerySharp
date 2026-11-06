using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using EasyQuerySharp;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Interceptors;

// 放在容器的命名空间里，引用包后 services.AddEasyQuery 直接可见（与 Npgsql、MySqlConnector 的注册扩展相同）
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 把 <see cref="EasyDb"/> 注册进容器（对应 Java 的 Spring Boot starter）。
/// <para>
/// <see cref="EasyDb"/> 注册成单例：它线程安全，实体元数据按实例缓存；环境事务与追踪作用域借 AsyncLocal 跟随当前异步流程，
/// 每个请求天然隔离，不需要按作用域创建。同一个名字注册多次时后注册的生效（容器的惯例，测试里可以借此替换）。
/// </para>
/// <para>
/// 配置函数在第一次解析时调用一次，收到的是根容器。默认的库取不带键的服务，命名的库取以库名为键的服务，按约定补齐：
/// 容器里登记的 <see cref="IExecutionListener"/>、<see cref="IInterceptor"/> 在配置函数之前依次加入；
/// 配置函数没有指定数据源时，取容器里的 <see cref="DbDataSource"/>（Npgsql 的 <c>AddNpgsqlDataSource</c>、
/// MySqlConnector 的 <c>AddMySqlDataSource</c> 会登记它，带 serviceKey 时按键登记）。
/// 这些服务被单例的 <see cref="EasyDb"/> 一直持有，要登记成单例。
/// </para>
/// </summary>
public static class EasyQueryServiceCollectionExtensions
{
    private const string NotAotReady = "EasyQuerySharp 在运行时反射读取实体元数据、编译读取委托，不支持裁剪与 Native AOT（见 BACKLOG）";

    /// <summary>注册默认的库，按类型注入（<c>EasyDb db</c>）。</summary>
    /// <example><code>
    /// services.AddNpgsqlDataSource(connectionString);
    /// services.AddEasyQuery(o =&gt; o.UsePostgreSql());   // 数据源取容器里的 DbDataSource
    /// </code></example>
    [RequiresUnreferencedCode(NotAotReady)]
    [RequiresDynamicCode(NotAotReady)]
    public static IServiceCollection AddEasyQuery(this IServiceCollection services, Action<EasyDbOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddEasyQuery((_, options) => configure(options));
    }

    /// <summary>注册默认的库；配置函数可以从容器取服务（根容器，只能取单例）。</summary>
    /// <example><code>
    /// services.AddEasyQuery((sp, o) =&gt; o.UseMySql().AddLogicDeleteStrategy("audit", sp.GetRequiredService&lt;AuditDeleteStrategy&gt;()));
    /// </code></example>
    [RequiresUnreferencedCode(NotAotReady)]
    [RequiresDynamicCode(NotAotReady)]
    public static IServiceCollection AddEasyQuery(this IServiceCollection services, Action<IServiceProvider, EasyDbOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddSingleton(provider => Create(provider, null, configure));
    }

    /// <summary>注册名为 <paramref name="name"/> 的库，按名字注入（<c>[FromKeyedServices("reporting")] EasyDb db</c>）。</summary>
    /// <example><code>
    /// services.AddNpgsqlDataSource(reportingConnectionString, serviceKey: "reporting");
    /// services.AddEasyQuery("reporting", o =&gt; o.UsePostgreSql());   // 数据源取键为 "reporting" 的 DbDataSource
    /// </code></example>
    [RequiresUnreferencedCode(NotAotReady)]
    [RequiresDynamicCode(NotAotReady)]
    public static IServiceCollection AddEasyQuery(this IServiceCollection services, string name, Action<EasyDbOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddEasyQuery(name, (_, options) => configure(options));
    }

    /// <summary>注册名为 <paramref name="name"/> 的库；配置函数可以从容器取服务（根容器，只能取单例）。</summary>
    [RequiresUnreferencedCode(NotAotReady)]
    [RequiresDynamicCode(NotAotReady)]
    public static IServiceCollection AddEasyQuery(this IServiceCollection services, string name, Action<IServiceProvider, EasyDbOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddKeyedSingleton(name, (provider, _) => Create(provider, name, configure));
    }

    [RequiresUnreferencedCode(NotAotReady)]
    [RequiresDynamicCode(NotAotReady)]
    private static EasyDb Create(IServiceProvider provider, string? name, Action<IServiceProvider, EasyDbOptions> configure)
    {
        var options = new EasyDbOptions();
        foreach (var listener in Services<IExecutionListener>(provider, name)) options.AddListener(listener);
        foreach (var interceptor in Services<IInterceptor>(provider, name)) options.AddInterceptor(interceptor);
        configure(provider, options);
        options.DataSource ??= name is null ? provider.GetService<DbDataSource>() : provider.GetKeyedService<DbDataSource>(name);
        if (options.Dialect is null)
            throw new InvalidOperationException($"{Registration(name)} 没有指定方言：在配置函数里调用 options.UseMySql(..)、UsePostgreSql(..) 等");
        return new EasyDb(options);
    }

    // 默认的库只走不带键的 API：不支持 keyed service 的第三方容器照样能用
    private static IEnumerable<T> Services<T>(IServiceProvider provider, string? name) =>
        name is null ? provider.GetServices<T>() : provider.GetKeyedServices<T>(name);

    private static string Registration(string? name) => name is null ? "AddEasyQuery(..)" : $"AddEasyQuery(\"{name}\", ..)";
}
