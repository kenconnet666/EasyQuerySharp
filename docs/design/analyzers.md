# 分析器

`src/EasyQuerySharp.Generator` 里的分析器在编译期指出运行时才会失败的查询写法。它是 netstandard2.0 程序集，Roslyn 固定在 4.14（不能高于使用者 SDK 自带的编译器）。测试在 `tests/EasyQuerySharp.Tests.Generator`：把代码片段连同真实的核心库一起编译，断言报出的诊断，每条诊断都有正例和反例。

只检查**查询表达式**：作为表达式树传给 EasyQuerySharp 方法的 lambda（`Where`、`Select`、`OrderBy`、`Set` 等），连同里面嵌套的 lambda（`u.Posts.Any(p => …)`）。普通的 LINQ to Objects 不检查。

| 编号 | 级别 | 内容 | 怎么改 |
|---|---|---|---|
| EQ0001 | 警告 | 调用了用到查询参数、又不能翻译成 SQL 的方法（不是 BCL、`Sql.*`，也没标 `[SqlFunction]`） | 给静态方法标 `[SqlFunction("模板")]`，或者在查询外算好再传进来；不引用查询参数的调用会在客户端先求值，不报 |
| EQ0002 | 提示 | 用了 `?.`（表达式树不支持，编译器同时报 CS8072） | 改成 `!.`：导航翻译成 LEFT JOIN / 子查询，取不到时本来就是 NULL。诊断消息里直接给出改写后的整段写法，嵌套的 `a?.b?.c` 一次改完 |

规则的发布记录在 `AnalyzerReleases.Unshipped.md`（发布时挪到 `Shipped`）。

没有做自动修复：代码修复要引用 Roslyn Workspaces，而分析器程序集不能引用它（RS1038），要单独再建一个程序集。等规则多到值得时再拆。

计划中的规则：EQ0003 / EQ0005（`[MapFrom]` 映射，4.7）、EQ0004（导航 ExtraFilter，4.8）。
