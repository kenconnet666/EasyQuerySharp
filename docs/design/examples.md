# 示例卡片与 SQL 快照

指南里的代码和 SQL 不手写：每段代码都是一张示例卡片，在 SQLite 示例库上实际执行，各方言的 SQL 由 `tools/DocsExamples` 生成并存成快照。文档因此也是一层测试。做法照搬 TS 版的文档站（`apps/docs`，约 500 张卡片）；站点本身暂不做，见 [BACKLOG](../BACKLOG.md)。

## 示例模型与数据

`samples/EasyQuerySharp.Samples` 是 TS 文档站模型（`apps/docs/src/site/model.ts`）与种子数据的原样移植：公司、用户、帖子、评论、标签、订单等 17 张表，类名、表名、数据都一致，两个版本的同一张卡片可以直接对比 SQL 和结果。README 与入门教程也用这个模型。

## 卡片

```csharp
// docs/examples/Query/BasicsExamples.cs
public static class BasicsExamples
{
    [Example("query/basics", "选表达式")]
    public static Task<List<PostCard>> SelectExpressions(EasyDb db) =>
        db.Query<Post>().Where(p => p.Status == PostStatus.Published)
          .Select(p => new PostCard(p.Title.ToUpper(), p.Views + 1, p.Views >= 2000))
          .Take(3).ToListAsync();

    [Example("query/json", "jsonb 包含", Only = DialectName.PostgreSql)]
    public static Task<List<Doc>> JsonbContains(EasyDb db) => ...;
}
```

- 卡片 id 是 `页面/方法名`，显示的代码由工具用 Roslyn 截取方法体。
- `Only` 限定方言；`Unsupported` 写已知不支持的方言，快照里照样写出报错，但不算校验失败。
- 卡片随生成器与分析器一起编译。

## `tools/DocsExamples`

1. 建 SQLite 内存库，执行种子数据。
2. 逐张卡片执行：在 SQLite 上真跑，用“磁带”（`Tape`）记下语句和结果；其余方言换成不连库的回放连接，按顺序还回 SQLite 的结果，同时记下这个方言发出的语句；方言不支持时记下错误信息。
3. 写出 `docs/sql-snapshots/<页面>.<方言>.sql`（提交进仓库）：每段由 `-- 名字`、SQL、`-- params: [...]` 组成，不支持时写 `-- error: …`。改了渲染器后，评审时直接看各方言 SQL 的 diff。快照覆盖 SQLite、MySQL、PostgreSQL、SQL Server、Oracle、达梦；其余第二档方言的 SQL 由 `tests` 里的 `XxxDialectTest` 核对。
4. 重写指南里的生成区域：
   - `<!-- card: 页面/方法名 sql=mysql,pgsql -->` 与 `<!-- /card -->` 之间写成卡片的方法体和所列方言的 SQL；
   - `<!-- snippet: 名字 -->` 与 `<!-- /snippet -->` 之间写成 `docs/examples` 或示例模型里 `// <snippet:名字>` 与 `// </snippet:名字>` 之间的代码（实体声明、配置这类不是卡片的代码）。

`--check` 只校验不写入（CI 的 `checks` 任务），以下任一情况都算失败：快照或指南过期；卡片在没声明 `Only` / `Unsupported` 的方言上报错；SQLite 上真跑失败；指南引用了不存在的卡片或片段。

## 限制

- 回放按语句条数还回结果：各方言发出的语句条数不同时（比如 PG 插入取自增键用 RETURNING，SQLite 多一条查询），那张卡片在该方言上报“回放的结果不够”，要标 `Unsupported` 或改写。
- 回放的参数借用 SQLite 的参数对象，不支持输出参数，Oracle / 达梦取自增键的 `RETURNING … INTO ?` 标 `Unsupported`。
- 注册代码（依赖注入、二级缓存）没有 SQL 可比，不做卡片，只写指南。
