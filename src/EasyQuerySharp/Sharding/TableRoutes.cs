using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Sharding;

/// <summary>
/// 一条语句里分片表的实际表名（按表来源对象区分）：一张时直接替换表名；几张时渲染成
/// <c>(SELECT * FROM a UNION ALL SELECT * FROM b) t</c>（表名按名字排序，与 Java 的 SHARDING_UNION_ALL 逐字一致）。
/// </summary>
internal sealed class TableRoutes : Dictionary<EntityTableSource, IReadOnlyList<string>>
{
    public TableRoutes() : base(ReferenceEqualityComparer.Instance)
    {
    }

    /// <summary>这张表路由到了几张分表（要写成 UNION ALL 派生表，单表时也要有别名）。</summary>
    public bool IsUnion(TableSource table) => table is EntityTableSource e && TryGetValue(e, out var tables) && tables.Count > 1;
}
