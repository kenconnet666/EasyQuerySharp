# 文档站与示例 SQL

文档站的目标和 TS 版一致：**每个能力一页，每页若干张示例卡片；每张卡片显示 C# 代码、各方言的 SQL 和在示例数据上的结果**。卡片在构建时实跑，所以文档本身也是一层测试。

> 现状（2026-11）：站点本身暂不做（见 BACKLOG），1.0 的文档是 `docs/guide/` 下的 Markdown 指南（ROADMAP 5.3）。卡片（`docs/examples`）与各方言的 SQL 快照（`docs/sql-snapshots`）照常随功能补，`tools/DocsExamples --check` 在 CI 里校验；下面关于站点的部分留作以后的设计。

## TS 版的现状（参考对象）

- 示例写在 `apps/docs/src/pages/**/*.examples.ts` 里，共 24 个文件、491 张卡片，页面上引用 505 处。
- 示例模型在 `apps/docs/src/site/model.ts`，有 17 张表：Company、Department、User、Profile、Category、Post、Comment、Tag、PostTag、Follow、Product、Order、OrderItem、Doc、Shop、Payment（多态）、Ticket。示例数据在 `seed-data.ts`，是不依赖 ORM 的纯数据。
- SQL 在构建期生成：
  - 每张卡片先在 SQLite 上真跑，记下驱动返回的结果；
  - 其余方言用"回放驱动"按顺序还回这些结果，同时记下这个方言实际会发出的语句；
  - 一共渲染 11 种方言：pg、sqlite、mysql、mariadb、mssql、oracle、dameng、db2、hana、clickhouse、duckdb。
- 用了方言专属函数的卡片写 `only: 'pg'` 这类标记：PG 的在 PGlite 上执行，其余的只显示 SQL。
- 浏览器里可以用 PGlite 和 SQLite WASM 真跑。站点检查脚本 `apps/docs/test/site.mjs` 用 Playwright 逐页点"运行"。

## C# 版的做法

### 示例模型与数据

把 TS 的 17 张表和 `seed-data.ts` 原样移植到 `samples/EasyQuerySharp.Samples`，类名、表名、数据都保持一致。

- 两个版本的同一张卡片，可以直接对比 SQL 和结果，跨语言对拍。
- 这个项目同时作为 README 和入门教程里的模型。

### 卡片的写法

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

- 卡片上显示的代码由工具用 Roslyn 截取方法体，不手写第二份。
- 卡片 id 用 `页面/方法名`。和 TS 卡片同名的，在元数据里记下 TS 的卡片 id，方便对照。

### 构建流程：`tools/DocsExamples`

1. 建 SQLite 内存库，执行种子数据。
2. 逐张卡片执行：
   - 在 SQLite 上**真跑**，记下语句和结果（序列化成 JSON）。
   - 对其余每种方言，换成 `ReplayDriver` 再执行一遍：驱动按顺序还回 SQLite 的结果，同时记下这个方言发出的语句；方言不支持时，记下错误信息。
   - 带 `Only` 的卡片：配置了对应的 `EQS_*_CS` 就在实库上跑，否则只渲染 SQL。
3. 输出：
   - `apps/docs/generated/examples.json`：站点用的数据，包括代码、各方言的语句和参数、结果、错误。
   - `docs/sql-snapshots/<页面>.<方言>.sql`：**提交进仓库的 SQL 快照**。格式照 TS 的 `eqts snapshot`：每段由 `-- 名字`、SQL 本身、`-- params: [...]` 组成，方言不支持时写 `-- error: …`。改了渲染器以后，评审时直接看各方言 SQL 的 diff。
4. 加 `--check` 时只校验不写入，CI 用这个模式。以下任一情况都算失败：
   - 快照过期；
   - 卡片在没声明 `Only` 的方言上报错；
   - SQLite 上真跑失败；
   - 页面引用了不存在的卡片。

另外有一个 xUnit 包装（`tests/EasyQuerySharp.Tests` 里的 `DocsExamplesTests`），把每张卡片注册成一个测试用例。这样本地运行 `dotnet test` 就能覆盖文档卡片。

### 站点

- 第一版用静态站点：`apps/docs` 读取 `examples.json`，前端可以复用 TS 文档站的组件，比如方言切换、SQL 格式化、结果表格。
- 卡片显示预先算好的结果，不在浏览器里执行。
- 页面的结构对齐 TS：
  - 模型：实体、列类型、关系、值对象、计算属性
  - 查询：基础、条件、排序分页、join、分组、窗口、子查询、CTE、列表页、JSON、复用、原生 SQL
  - 函数
  - 导航：对一、对多、多对多、直达、对象树、视图、树形数据、填充
  - 写入：插入、upsert、更新、删除、保存、事务、锁
  - 列规则：逻辑删除、乐观锁、自动填充、加密、多租户、拦截器
  - 运维
- 每页写清楚：用法、各方言支持情况、限制、和 Java 版的对应关系（Java 的方法名）。
- 以后可选：
  - 和 TS 文档站合并成一个站，同一张卡片切换 TS 和 C# 两种代码；
  - 用 Blazor WASM 加 SQLite WASM 实现在浏览器里运行。

### 规模目标

卡片数量对齐 TS（约 500 张）。每完成一个能力：

- 补齐这个能力对应的页面和卡片；
- 每个公开 API 至少出现在一张卡片里；
- 页面上的方言支持矩阵和 `Dialect` 的能力声明保持一致。

## 现状（2026-11）

- `tools/DocsExamples` 已实现：卡片在 `docs/examples/*.cs`，用 `[Example("页面", "标题")]` 标记；`Only` 限定方言，`Unsupported` 写已知还不支持的方言（快照里照样写出报错，但不算校验失败）。
- 回放用"磁带"（`Tape`）：录制时包住真实的 SQLite 连接，回放时一个不连库的 ADO.NET 连接按顺序还回结果。各方言发出的语句条数不同（比如 PG 插入取自增键用 RETURNING，SQLite 多一条查询）时，那张卡片在该方言上会报"回放的结果不够"，这类卡片标 `Unsupported` 或改写。
- 指南（`docs/guide/*.md`）里的代码与 SQL 由工具生成：`<!-- card: 页面/方法名 sql=mysql,pgsql -->` 与 `<!-- /card -->` 之间重写成卡片的方法体（Roslyn 截取）和所列方言发出的 SQL；`<!-- snippet: 名字 -->` 与 `<!-- /snippet -->` 之间重写成 `docs/examples` 里 `// <snippet:名字>` 与 `// </snippet:名字>` 之间的代码（实体声明、配置这类不是卡片的代码）。`--check` 时指南过期、引用不存在的卡片或片段都算失败。
- CI 的 `core` 任务运行 `DocsExamples --check`。xUnit 包装与站点数据 `examples.json` 等文档站（M8）开工时再做。
