# BACKLOG

记下来但暂时不做的事，一行一条，写清楚触发条件或理由。正确性 bug 不记在这里，要直接修。

- 同步版本的终结方法（`ToList()` 等）：第一期只做异步。有真实需求时再加，避免 API 面积翻倍。
- `IQueryable<T>` 适配（`AsQueryable()`）：有 OData、AutoMapper `ProjectTo` 这类集成需求时再做。
- 浏览器里运行文档卡片（Blazor WASM + SQLite WASM）：第一版只展示预先算好的结果。
- 和 TS 文档站合并成一个站，同一张卡片切换两种代码：两边的卡片都稳定以后再评估。
- 方言能力做进类型系统（参考 TS 的 `Adapter<C>`）：先用运行时检查加分析器。
- 一条 SQL 用 JSON 聚合出整棵对象树：不做，理由见 [0002](decisions/0002-object-tree-and-dto.md)。
- 标量投影之后再聚合（`q.Select(x => x.Score ?? 0m).SumAsync(v => v)`）：选择器参数绑定到单列派生表还不支持，多表时直接用 `SumAsync((a, b) => …)`；有真实需求时再补。
