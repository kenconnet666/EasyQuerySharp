using System.Data.Common;
using System.Linq.Expressions;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EasyQuerySharp.Tests.DependencyInjection;

/// <summary><c>services.AddEasyQuery(..)</c>：默认的库、按名字注入的库、生命周期、从容器补齐的服务、配置校验。</summary>
public sealed class AddEasyQueryTest
{
    // ---------- 默认的库与生命周期 ----------

    [Fact]
    public void DefaultDatabase_IsOneSingletonAcrossScopes()
    {
        var services = new ServiceCollection().AddEasyQuery(o => o.UseSqlite(NewDataSource()));
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, d => d.ServiceType == typeof(EasyDb)).Lifetime);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var root = provider.GetRequiredService<EasyDb>();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(root, first.ServiceProvider.GetRequiredService<EasyDb>());
        Assert.Same(root, second.ServiceProvider.GetRequiredService<EasyDb>());
    }

    [Fact]
    public async Task Singleton_KeepsTrackingAndTransactionsPerAsyncFlow()
    {
        using var provider = new ServiceCollection().AddEasyQuery(o => o.UseSqlite(NewDataSource())).BuildServiceProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // 两个并发的"请求"各自从作用域解析到同一个 EasyDb：一个请求里的追踪作用域与事务不会出现在另一个请求里
        var first = Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EasyDb>();
            using var tracking = db.BeginTracking();
            await using var tx = db.BeginTransaction();
            entered.SetResult();
            await release.Task;
            return db.IsTracking;
        }, TestContext.Current.CancellationToken);
        await entered.Task;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EasyDb>();
            Assert.False(db.IsTracking);
            await using var tx = db.BeginTransaction();   // 已经在事务里时再开会抛异常；这里不抛，说明事务没有串到别的请求
        }
        release.SetResult();
        Assert.True(await first);
    }

    [Fact]
    public void RegisteringAgain_ReplacesTheDatabase()
    {
        using var provider = new ServiceCollection()
            .AddEasyQuery(o => o.UseMySql())
            .AddEasyQuery(o => o.UseSqlite())
            .BuildServiceProvider();
        Assert.IsType<SqliteDialect>(provider.GetRequiredService<EasyDb>().Options.Dialect);
    }

    // ---------- 按名字注入 ----------

    [Fact]
    public void NamedDatabases_AreKeyedSingletons()
    {
        using var provider = new ServiceCollection()
            .AddEasyQuery(o => o.UseSqlite())
            .AddEasyQuery("reporting", o => o.UsePostgreSql())
            .BuildServiceProvider(validateScopes: true);
        var main = provider.GetRequiredService<EasyDb>();
        var reporting = provider.GetRequiredKeyedService<EasyDb>("reporting");

        Assert.NotSame(main, reporting);
        Assert.IsType<SqliteDialect>(main.Options.Dialect);
        Assert.IsType<PostgreSqlDialect>(reporting.Options.Dialect);
        using var scope = provider.CreateScope();
        Assert.Same(reporting, scope.ServiceProvider.GetRequiredKeyedService<EasyDb>("reporting"));
        Assert.Null(provider.GetKeyedService<EasyDb>("archive"));
    }

    [Fact]
    public void NamedDatabase_IsInjectedWithFromKeyedServices()
    {
        using var provider = new ServiceCollection()
            .AddEasyQuery(o => o.UseSqlite())
            .AddEasyQuery("reporting", o => o.UsePostgreSql())
            .AddSingleton<ReportService>()
            .BuildServiceProvider();
        var service = provider.GetRequiredService<ReportService>();

        Assert.Same(provider.GetRequiredService<EasyDb>(), service.Main);
        Assert.Same(provider.GetRequiredKeyedService<EasyDb>("reporting"), service.Reporting);
    }

    // ---------- 从容器补齐的数据源、监听器、拦截器 ----------

    [Fact]
    public async Task DataSourceAndListener_ComeFromContainer()
    {
        var dataSource = NewDataSource();
        var listener = new RecordingListener();
        using var provider = new ServiceCollection()
            .AddSingleton(dataSource)
            .AddSingleton<IExecutionListener>(listener)
            .AddEasyQuery(o => o.UseSqlite())
            .BuildServiceProvider();
        var db = provider.GetRequiredService<EasyDb>();

        Assert.Same(dataSource, db.Options.DataSource);
        Assert.Equal(1L, Assert.Single(await db.SqlQueryRawAsync<long>("SELECT 1", cancellationToken: TestContext.Current.CancellationToken)));
        Assert.Equal("SELECT 1", Assert.Single(listener.Sql));
    }

    [Fact]
    public void ConfiguredDataSource_WinsOverContainer()
    {
        var configured = NewDataSource();
        using var provider = new ServiceCollection()
            .AddSingleton(NewDataSource())
            .AddEasyQuery(o => o.UseSqlite(configured))
            .BuildServiceProvider();
        Assert.Same(configured, provider.GetRequiredService<EasyDb>().Options.DataSource);
    }

    [Fact]
    public void WithoutAnyDataSource_OnlyGeneratesSql()
    {
        using var provider = new ServiceCollection().AddEasyQuery(o => o.UseSqlite()).BuildServiceProvider();
        var db = provider.GetRequiredService<EasyDb>();

        Assert.Null(db.Options.DataSource);
        Assert.Equal("SELECT \"id\",\"tenant_id\" FROM \"ticket\"", db.Query<Ticket>().ToSql());
    }

    [Fact]
    public void InterceptorsFromContainer_ApplyToQueries()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IInterceptor, TenantFilter>()
            .AddEasyQuery(o => o.UseSqlite())
            .BuildServiceProvider();
        var db = provider.GetRequiredService<EasyDb>();

        Assert.Equal("SELECT \"id\",\"tenant_id\" FROM \"ticket\" WHERE \"tenant_id\" = ?", db.Query<Ticket>().ToSql());
    }

    [Fact]
    public void Configure_ResolvesServicesFromContainer()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<TenantFilter>()
            .AddEasyQuery((sp, o) => o.UseSqlite().AddInterceptor(sp.GetRequiredService<TenantFilter>()))
            .BuildServiceProvider();
        var db = provider.GetRequiredService<EasyDb>();

        Assert.Same(provider.GetRequiredService<TenantFilter>(), Assert.Single(db.Interceptors.All));
    }

    [Fact]
    public void NamedDatabase_TakesOnlyServicesRegisteredUnderItsName()
    {
        DbDataSource mainSource = NewDataSource(), reportingSource = NewDataSource();
        RecordingListener mainListener = new(), reportingListener = new();
        using var provider = new ServiceCollection()
            .AddSingleton(mainSource)
            .AddSingleton<IExecutionListener>(mainListener)
            .AddKeyedSingleton("reporting", reportingSource)
            .AddKeyedSingleton<IExecutionListener>("reporting", reportingListener)
            .AddKeyedSingleton<IInterceptor, TenantFilter>("reporting")
            .AddEasyQuery(o => o.UseSqlite())
            .AddEasyQuery("reporting", o => o.UseSqlite())
            .AddEasyQuery("archive", o => o.UseSqlite())
            .BuildServiceProvider();

        var main = provider.GetRequiredService<EasyDb>();
        Assert.Same(mainSource, main.Options.DataSource);
        Assert.Same(mainListener, Assert.Single(main.Options.Listeners));
        Assert.Empty(main.Interceptors.All);

        var reporting = provider.GetRequiredKeyedService<EasyDb>("reporting");
        Assert.Same(reportingSource, reporting.Options.DataSource);
        Assert.Same(reportingListener, Assert.Single(reporting.Options.Listeners));
        Assert.IsType<TenantFilter>(Assert.Single(reporting.Interceptors.All));

        // 默认库的服务（不带键）不会给到命名的库
        var archive = provider.GetRequiredKeyedService<EasyDb>("archive");
        Assert.Null(archive.Options.DataSource);
        Assert.Empty(archive.Options.Listeners);
    }

    [Fact]
    public void ScopedListener_IsRejectedByScopeValidation()
    {
        using var provider = new ServiceCollection()
            .AddScoped<IExecutionListener, RecordingListener>()
            .AddEasyQuery(o => o.UseSqlite())
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        // 单例的 EasyDb 会一直持有监听器，作用域内的服务不能给它
        var e = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<EasyDb>());
        Assert.Contains(typeof(IExecutionListener).FullName!, e.Message, StringComparison.Ordinal);
    }

    // ---------- 配置校验 ----------

    [Fact]
    public void MissingDialect_FailsOnResolveNamingTheDatabase()
    {
        using var provider = new ServiceCollection().AddEasyQuery("reporting", _ => { }).BuildServiceProvider();

        var e = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredKeyedService<EasyDb>("reporting"));
        Assert.StartsWith("AddEasyQuery(\"reporting\", ..) 没有指定方言", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InterceptorBothInContainerAndConfigure_FailsAsDuplicate()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IInterceptor, TenantFilter>()
            .AddEasyQuery(o => o.UseSqlite().AddInterceptor(new TenantFilter()))
            .BuildServiceProvider();

        var e = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<EasyDb>());
        Assert.Equal("拦截器 TenantFilter 重复注册", e.Message);
    }

    [Fact]
    public void InvalidArguments_FailOnRegistration()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddEasyQuery((Action<EasyDbOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddEasyQuery((Action<IServiceProvider, EasyDbOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddEasyQuery(null!, o => o.UseSqlite()));
        Assert.Throws<ArgumentException>(() => services.AddEasyQuery("", o => o.UseSqlite()));
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddEasyQuery(o => o.UseSqlite()));
        Assert.Empty(services);
    }

    private static DbDataSource NewDataSource() => SqliteFactory.Instance.CreateDataSource("Data Source=:memory:");

    private sealed class ReportService(EasyDb main, [FromKeyedServices("reporting")] EasyDb reporting)
    {
        public EasyDb Main => main;

        public EasyDb Reporting => reporting;
    }

    private sealed class RecordingListener : IExecutionListener
    {
        public List<string> Sql { get; } = [];

        public void OnExecuting(SqlExecutionEvent e) => Sql.Add(e.Statement.Text);
    }

    private sealed class TenantFilter : QueryFilter<Ticket>
    {
        public override Expression<Func<Ticket, bool>> Filter() => t => t.TenantId == "acme";
    }
}

internal sealed class Ticket
{
    public long Id { get; set; }

    public string TenantId { get; set; } = "";
}
