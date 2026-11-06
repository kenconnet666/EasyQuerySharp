# 分析器

`src/EasyQuerySharp.Generator` 里的分析器在编译期指出运行时才会失败的查询写法。它是 netstandard2.0 程序集，Roslyn 固定在 4.14（不能高于使用者 SDK 自带的编译器）。测试在 `tests/EasyQuerySharp.Tests.Generator`：把代码片段连同真实的核心库一起编译，断言报出的诊断，每条诊断都有正例和反例。

EQ0001、EQ0002 只检查**查询表达式**：作为表达式树传给 EasyQuerySharp 方法的 lambda（`Where`、`Select`、`OrderBy`、`Set` 等），连同里面嵌套的 lambda（`u.Posts.Any(p => …)`）。普通的 LINQ to Objects 不检查。EQ0007 反过来，检查查询表达式**之外**的代码。

| 编号 | 级别 | 内容 | 怎么改 |
|---|---|---|---|
| EQ0001 | 警告 | 调用了用到查询参数、又不能翻译成 SQL 的方法（不是 BCL、`Sql.*`，也没标 `[SqlFunction]`） | 给静态方法标 `[SqlFunction("模板")]`，或者在查询外算好再传进来；不引用查询参数的调用会在客户端先求值，不报 |
| EQ0002 | 提示 | 用了 `?.`（表达式树不支持，编译器同时报 CS8072） | 改成 `!.`：导航翻译成 LEFT JOIN / 子查询，取不到时本来就是 NULL。诊断消息里直接给出改写后的整段写法，嵌套的 `a?.b?.c` 一次改完 |
| EQ0003 | 警告 | `[MapFrom<T>]` 的 DTO 成员在实体上对不上：同名属性（忽略大小写）不存在，或 `[MapPath]` 的路径某一段找不到 | 改名，或用 `[MapPath]` / `[MapExpression]` 指定，或标 `[ColumnIgnore]` |
| EQ0004 | 错误 | `[Navigate]` 的 `ExtraFilter` / `ExtraMappingFilter` 引用的成员不存在、不是静态，或类型不是 `Expression<Func<目标（中间表）, bool>>` | 用 `nameof` 引用本实体上的静态表达式属性或字段 |
| EQ0005 | 警告 | `[MapFrom<T>]` 的 DTO 把没有标 `Required` 的可空对一导航映射成了非空成员 | DTO 成员加 `?`，或在导航上标 `Required = true` |
| EQ0006 | 错误 | `[Projection]` 方法写法生成器处理不了：不是静态、是泛型、所在类型不是 partial 或是泛型、方法体不止一个表达式 | 按诊断说明改写；方法体只写一个表达式（`=> 表达式` 或只有一句 `return`） |
| EQ0007 | 错误 | 只能写在查询表达式里的方法（`Sql.*`，`Sql.Identifier` 除外；标了 `[SqlFunction]` 的方法）在普通代码里被调用，执行时会抛异常。写在任意一层转换成 `Expression<…>` 的 lambda 里、`[Projection]` 方法体里、LINQ 查询语法里都不报 | 挪进查询表达式；要在内存里算，就用对应的 BCL 写法 |

## 生成器

`ProjectionGenerator` 为每个 `[Projection]` 方法在所在的 partial 类型里生成一个模块初始化方法：把方法体原样写成 `Expression<Func<…>>`（在原来的 using 与类型里编译，名字解析与原方法一致），登记到 `ProjectionRegistry`。查询里调用该方法时内联成这棵表达式；在内存里调用就是普通方法。没有引用生成器时，查询里用到 `[Projection]` 方法会在运行时报错并说明要引用生成器。

规则的发布记录在 `AnalyzerReleases.Unshipped.md`（发布时挪到 `Shipped`）。

没有做自动修复：代码修复要引用 Roslyn Workspaces，而分析器程序集不能引用它（RS1038），要单独再建一个程序集。等规则多到值得时再拆。

EQ0001 不报 `[Projection]` 方法（它们会内联）。`[MapFrom<T>]` 的映射表达式目前在运行时构造（与其它 DTO 的 `Select<TDto>()` 相同），编译期生成随 1.1 的生成器一起做。
