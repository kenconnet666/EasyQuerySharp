# 裁剪与 Native AOT

EasyQuerySharp 的三个包（核心、依赖注入、二级缓存）都可以用在开了裁剪（`PublishTrimmed`）或 Native AOT（`PublishAot`）的应用里，写法不变。设计见 [0008](../decisions/0008-native-aot.md)。

```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
</PropertyGroup>
```

驱动本身也要支持 AOT：Microsoft.Data.Sqlite、Npgsql 支持（Npgsql 推荐用 `NpgsqlSlimDataSourceBuilder`）；其余驱动以各自的文档为准。

## 不用做什么

EasyQuerySharp 在运行时反射读取实体的属性、特性与构造函数。裁剪器看不到这些用法，会删掉 setter、构造函数一类成员。核心包自带的生成器会在你的项目里替你保留它们：

- 交给 EasyQuerySharp 的类型：`db.Query<User>()`、`Select<UserDto>()`、`db.Insert(user)`、`[MapFrom<User>]` 里的类型参数，以及标了 `[Table]` 的类；
- 再顺着属性找到的导航目标、值对象、集合元素，以及 `[Navigate(Mapping = …)]` 的中间表；
- 值类型集合属性（`List<decimal>`）和枚举属性要用到的泛型实例化。

生成器随核心包安装，不需要配置。JIT 下它生成的代码什么也不做。

## 要做什么

**JSON 列**：`JsonValueConverter<T>` 默认按反射序列化，Native AOT 下反射 JSON 默认关闭。写一个子类，把源生成的元数据传给受保护的构造函数：

```csharp
[JsonSerializable(typeof(Address))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)] // 属性名 camelCase，与反射版一致
internal sealed partial class AppJson : JsonSerializerContext;

internal sealed class AddressJson() : JsonValueConverter<Address>(AppJson.Default.Address);

public class Customer
{
    [Column(Converter = typeof(AddressJson))]
    public Address? Address { get; set; }
}
```

**二级缓存**：缓存把实体存成 JSON，同样要给源生成的元数据，包含所有要缓存的实体（导航属性等不是列的成员照样不进缓存）：

```csharp
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(Province))]
internal sealed partial class CacheJson : JsonSerializerContext;

var cache = new EasyCache(hybridCache, o =>
{
    o.Cache<User>().Cache<Province>();
    o.Json = new JsonSerializerOptions { TypeInfoResolver = CacheJson.Default };
});
```

没给 `TypeInfoResolver`、反射 JSON 又关闭时，第一次用缓存会报错说明这一点。

**生成器找不到的类型**：只通过非泛型的方式交给 EasyQuerySharp 的类型（例如别的程序集里的泛型方法转手传进来的 DTO），生成器看不到。Native AOT 下用到它时会因为成员被裁掉而出错，这时自己保留一下：

```csharp
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ReportRow))]
static void KeepTypes() { }
```

## 会看到的警告

发布时，表达式树里的对象初始化、构造与数组（`new UserDto { Name = u.Name }`、`new[] { 1, 2 }`）会报 IL2026 / IL3050。这是 .NET 给 `Expression.Bind`、`Expression.New`、`Expression.NewArrayInit` 加的标注，任何用表达式树写查询的库都一样。用到的类型已由生成器保留，可以在项目里压下这几处：

```xml
<!-- 只在确认来源都是查询的表达式树时这样做 -->
<NoWarn>$(NoWarn);IL2026;IL3050</NoWarn>
```

更稳妥的做法是只在写查询的方法上加 `[UnconditionalSuppressMessage("Trimming", "IL2026")]`。EasyQuerySharp 自己的程序集不会产生裁剪 / AOT 警告。

## 限制

- **性能**：Native AOT 下不能在运行时生成代码，读取函数由表达式树解释执行，实体的列经反射写入，读大量行时比 JIT 慢。翻译 SQL 不受影响。编译期预编译查询（interceptors）记在 [BACKLOG](../BACKLOG.md)。
- **单文件发布、ReadyToRun** 一直都支持，与 Native AOT 无关。
