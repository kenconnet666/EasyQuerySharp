# 0001 查询 API 路线：POCO 实体 + 表达式树

状态：提议（2026-11-04）

## 背景

EasyQuerySharp 要完整对标 easy-query（Java）的能力，API 写法可以不同，但要更顺手。Java 版和 TS 版拿到"类型安全的列引用"的办法都是**录制**：

- Java：APT 为每个实体生成 `XxxProxy`，lambda 在录制上下文里真实执行，`x.name().like(..)` 把谓词写进构建器。原因是 Java 没有表达式树。
- TS：`makeRef` 运行时生成引用对象，lambda 收到它，返回 `Expr<T>`。原因同上，TS 也拿不到 lambda 的语法树。

C# 编译器能把 lambda 直接编译成表达式树（`Expression<Func<...>>`），所以 Java 和 TS 版不得不录制的东西，C# 天然就有。

## 决定

1. **实体就是 POCO**，导航属性就是关系；查询 lambda 直接写在实体上：`u => u.Company!.Name.StartsWith("Acme")`。
2. **不复刻 Java Proxy**，不生成 `UserProxy`。用户只面对一套类型。
3. **Source Generator 只做三件事**：实体元数据（列、键、关系）、`DbDataReader` 物化器（不靠反射，支持 AOT 和裁剪）、`[Projection]` 方法与 `[MapFrom<T>]` DTO 对应的表达式（见 [0002](0002-object-tree-and-dto.md)）。
4. **自己定义流式接口，不实现 `IQueryable<T>`**。`IQueryable` 会把调用链交给 `System.Linq.Queryable`，`WhereIf`、`ToPageAsync`、带过滤的 `Include` 这类专有方法接不进去，不支持的写法也只能等到运行时才报。以后需要时再提供 `AsQueryable()` 适配。
5. 查询对象只要有 `Where` / `Select` / `SelectMany` / `Join` / `GroupBy` / `OrderBy`，LINQ 查询语法（`from u in db.Query<User>() where … select …`）就能用，这是附带的好处，不额外维护。

## 探针证据（.NET 10.0.400 / C# 14 实测）

### 表达式树能覆盖 easy-query 的核心卖点

```csharp
db.Query<User>()
  .Where(u => u.Name.Contains(keyword) && u.Company!.Name.StartsWith("Acme"))   // 隐式 LEFT JOIN
  .WhereIf(minAge != null, u => u.Age >= minAge)
  .Where(u => u.Posts.Any(p => p.Title.Like("%ef%")))                           // EXISTS 子查询
  .Where(u => ids.Contains(u.Id))                                               // IN (...)
  .Select(u => new UserDto(u.Id, u.Name, u.Company!.Name, u.Posts.Count()));    // record DTO + 标量子查询
```

一个约 200 行的翻译器就渲染出了正确的 SQL：导航属性变成 `LEFT JOIN`，`Any` 变成 `EXISTS`，`Count()` 变成标量子查询，三元表达式变成 `CASE WHEN`，`??` 变成 `COALESCE`，`== null` 变成 `IS NULL`，闭包变量都参数化。

### 表达式树里不能写的语法

这些限制决定了 API 的形状：

| 不能写 | 编译错误 | API 对策 |
|---|---|---|
| `?.` | CS8072 | 写 `u.Company!.Name`；语义规定为"隐式 LEFT JOIN，取不到就是 NULL" |
| `is null`、`is "a" or "b"` | CS8122 | 写 `== null`；多值用 `In(...)` |
| `switch` 表达式 | CS8514 | 用三元表达式或 `Sql.Case()` |
| 元组字面量 `(a, b)` | CS8143 | 投影用匿名类型或 record |
| 集合表达式 `[..]` | CS9175 | 用 `new[] { }` 或外部变量 |
| C# 14 **扩展属性** | CS9296 | 列上的 SQL 函数一律做成**扩展方法**（扩展方法实测可以） |
| `x[1..]`、`^1` | CS8790 / CS8792 | 用 `Substring` 系列函数 |
| `params ReadOnlySpan<T>` 展开 | CS9226 / CS8640 | 函数签名用 `params T[]` |

能写的有：匿名类型、record 构造、成员初始化、`??`、三元、字符串插值（编译成 `string.Format`）、`is T` 类型测试、`!`、`params T[]`、省略可选参数。

这些限制会由 Roslyn Analyzer 在编译期提示，见 [架构](../design/architecture.md#分析器)。

### 必须处理的语言细节

- **C# 14 first-class span**：`long[] ids; ids.Contains(x)` 绑定到的是 `MemoryExtensions.Contains(ReadOnlySpan<T>, T)`，参数外面包了一层 `op_Implicit`，不是 `Enumerable.Contains`。翻译器要识别这种形状，EF Core 升级 C# 14 时也踩过这个坑。`List<T>.Contains` 仍是实例方法。
- **闭包变量求值**：探针里用 `Expression.Lambda(e).Compile()` 求值时每次翻译约 367 μs，改成按"常量对象.字段"直接反射取值后约 23 μs（量级参考）。规则是**翻译路径上禁止 `Compile()`**。

### 对照：Proxy + 运算符重载路线

也做了一版：`SqlCol<T>` 重载 `==` / `>` 等运算符，`SqlBool` 重载 `&` / `|` 和 `true` / `false`，让 `&&` / `||` 可用。

```csharp
u => u.Name.Like("ali") && (u.Age == null || u.Age >= 18)
```

| | 表达式树 | Proxy |
|---|---|---|
| 用户面对的类型 | 只有实体 | 实体 + `UserProxy` 两套 |
| 投影到 DTO | `new UserDto(u.Id, u.Name)` | lambda 里拿到的是列对象不是值，要另造一套 select 语法 |
| 可空性分析 | 正常 | 重载的 `==` 与 null 比较会干扰流分析，实测出现 CS8604 警告 |
| 动态条件 | `WhereIf`，或条件拼接 | 语句 lambda 里可以写 `if` |
| 构造开销 | 约 23 μs（未缓存），需要缓存 | 约 0.7 μs |
| 代码生成 | 只要元数据和物化器 | 每个实体还要生成一个 Proxy |

性能差距可以靠缓存解决（下一节），类型体验上的差距解决不了，所以选表达式树。

## 性能对策

1. **形状缓存**：参考 EF Core 的查询缓存，表达式按结构算哈希（闭包里的常量当作参数，不进哈希），命中缓存后只提取参数值，不再翻译。
2. **参数提取不走 `Compile()`**：常量对象的字段和属性直接反射读取；复杂表达式退回到编译后缓存的委托。
3. **以后再做**：Source Generator 加 interceptors，在编译期把固定形状的查询预翻译成 SQL（参考 EF Core 预编译查询和 Dapper.AOT）。interceptors 在官方文档里仍标为实验性，先不依赖。

## 后果

- 每个能力都要回答一个问题："它在表达式树里怎么写"。SQL 函数是扩展方法加 `[SqlFunction]` 标记，方言专属函数放在静态入口下，比如 `MySql.JsonContains(u.Attrs, "$.a")`，对应 Java fork 的 decisions/0001。
- 不支持的写法默认在运行时报 `NotSupportedException`，消息里要带上原始表达式和位置；分析器做好之后改成编译期报错。
- 表达式缓存和参数提取是核心基础设施，第一期就要做，不能等到优化阶段。
