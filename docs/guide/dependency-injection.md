# 依赖注入

包 `EasyQuerySharp.DependencyInjection` 把 `EasyDb` 注册进 `IServiceCollection`。它只引用 `Microsoft.Extensions.DependencyInjection.Abstractions`，核心包仍然零依赖。对应 Java 的 Spring Boot starter 和 Solon 插件。

## 默认的库

```csharp
builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("Main")!);
builder.Services.AddEasyQuery(o => o.UsePostgreSql());   // 没有指定数据源，就用容器里的 DbDataSource

public sealed class OrderService(EasyDb db) { /* … */ }
```

配置函数需要从容器取服务时，改用带两个参数的重载：

```csharp
builder.Services.AddEasyQuery((sp, o) => o
    .UseMySql(sp.GetRequiredService<MySqlDataSource>())
    .AddLogicDeleteStrategy("audit", sp.GetRequiredService<AuditDeleteStrategy>()));
```

## 多个库按名字注入

名字就是 keyed service 的键（.NET 8 起）：

```csharp
builder.Services.AddNpgsqlDataSource(reportingConnectionString, serviceKey: "reporting");
builder.Services.AddEasyQuery("reporting", o => o.UsePostgreSql());   // 数据源取键为 "reporting" 的 DbDataSource

public sealed class ReportService([FromKeyedServices("reporting")] EasyDb reporting) { /* … */ }
// 手动解析：sp.GetRequiredKeyedService<EasyDb>("reporting")
```

## 从容器补齐的服务

默认的库只取不带键的服务，名为 `name` 的库只取键为 `name` 的服务：

| 服务 | 什么时候用 |
|---|---|
| `DbDataSource` | 配置函数没有指定数据源时。Npgsql 的 `AddNpgsqlDataSource`、MySqlConnector 的 `AddMySqlDataSource` 都会登记它，传了 `serviceKey` 时按键登记 |
| `IExecutionListener` | 全部加入，在配置函数之前，按登记顺序 |
| `IInterceptor` | 同上，例如 `services.AddSingleton<IInterceptor, TenantFilter>()` |

- 不带键的服务不会给命名的库。几个库共用一个监听器时，可以分别登记，也可以在配置函数里写 `o.AddListener(sp.GetRequiredService<SqlLogListener>())`。
- 逻辑删除策略要有名字，所以不自动收集，在配置函数里调用 `AddLogicDeleteStrategy`。
- 同一类型的拦截器既登记在容器里、又在配置函数里添加，会报"重复注册"。

## 生命周期

`EasyDb` 注册成单例：

- 它是线程安全的，实体元数据按实例缓存。如果每个请求新建一个，元数据就要反复反射。
- 环境事务（`BeginTransaction`）和追踪作用域（`BeginTracking`）挂在 AsyncLocal 上，跟随当前异步流程，所以各个请求互不影响，不需要按作用域注册。

由此带来的要求：

- 配置函数在第一次解析时执行一次，收到的是根容器。它用到的服务，以及容器里的监听器和拦截器，都会被 `EasyDb` 一直持有，所以要登记成单例。如果登记成作用域服务，在开启作用域校验时（ASP.NET Core 开发环境默认开启）会报错。随请求变化的值（当前租户、当前用户）通过单例的访问器取，比如 `IHttpContextAccessor`。
- 数据源最好交给容器管理（用驱动的 `AddXxxDataSource` 登记），应用退出时由容器释放。在配置函数里 new 出来的数据源不会被释放。
- 同一个名字注册多次时，后注册的生效。集成测试可以借此换成测试库。

## 限制

- 配置错误（比如没有指定方言）要到第一次解析时才报出来，消息里会写明是哪一次 `AddEasyQuery`。
- 可以用在裁剪 / Native AOT 的应用里，见[裁剪与 Native AOT](native-aot.md)。
- 不绑定 `IConfiguration`。Java 写在 `application.yml` 里的 `easy-query.*` 配置项，在 C# 里由配置函数设置 `EasyDbOptions`。

## 对应 Java

| Java | C# |
|---|---|
| `sql-springboot-starter`：自动装配容器里的 `DataSource`，以及 `Interceptor`、`LogicDeleteStrategy` 等 Bean | `AddEasyQuery`：自动取容器里的 `DbDataSource`、`IExecutionListener`、`IInterceptor`；逻辑删除策略在配置函数里注册 |
| Solon 插件的 `@Db("db1")` 多库注入 | `AddEasyQuery("db1", ..)` 加 `[FromKeyedServices("db1")]` |
| `InjectTest`（Java 自带的简易容器） | 不适用，直接用 Microsoft.Extensions.DependencyInjection |
