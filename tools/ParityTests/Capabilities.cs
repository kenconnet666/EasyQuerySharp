using System.Text.RegularExpressions;

namespace ParityTests;

/// <summary>能力的描述：标题与所属里程碑（见 ROADMAP.md）。</summary>
internal sealed record Capability(string Key, string Title, string Milestone);

/// <summary>
/// 能力表与用例归类规则。规则沿用 easy-query-ts 的 tools/capabilities.mjs（先按文件、再按方法体里的 API，越靠前越具体），
/// 另加 fork 用例（fork/ 开头）的文件规则。
/// </summary>
internal static class Capabilities
{
    public static readonly IReadOnlyList<Capability> All =
    [
        new("query.basic", "基本查询（条件、排序、选列、分页）", "M1"),
        new("query.page", "分页结果", "M1"),
        new("sql.native", "原生 SQL", "M1"),
        new("write.insert", "插入", "M1"),
        new("write.update", "更新", "M1"),
        new("write.delete", "删除", "M1"),
        new("column.generated", "自增与生成列", "M1"),
        new("func.string", "字符串函数", "M2"),
        new("func.date", "日期函数", "M2"),
        new("func.general", "通用函数（nullOrDefault、greatest、nullif、null-safe）", "M2"),
        new("func.joining", "字符串聚合（joining）", "M2"),
        new("func.json", "JSON 函数", "M2"),
        new("func.math", "数学函数", "M2"),
        new("expr.case", "CASE WHEN", "M2"),
        new("window", "窗口函数", "M2"),
        new("lock", "行锁", "M2"),
        new("query.join", "显式 join", "M3"),
        new("query.group", "分组与 having（含 rollup / cube）", "M3"),
        new("query.union", "union / union all", "M3"),
        new("query.cte", "CTE", "M3"),
        new("query.map", "无实体的 Map 查询、toMap", "M3"),
        new("query.stream", "流式读取", "M3"),
        new("query.reuse", "复用查询（cloneQueryable）", "M3"),
        new("nav.subquery", "隐式子查询（any / none / count / sum …）", "M4"),
        new("nav.groupJoin", "隐式子查询改分组 join", "M4"),
        new("nav.flat", "一对多展开（flatElement / elements）", "M4"),
        new("nav.include", "include 带出导航", "M4"),
        new("dto.autoInclude", "按 DTO 自动带出（selectAutoInclude）", "M4"),
        new("fill", "按键填充（fillMany / fillOne）", "M4"),
        new("tree", "树（toTreeList / asTreeCTE）", "M4"),
        new("write.upsert", "upsert", "M5"),
        new("write.strategy", "写哪些列、批量执行", "M5"),
        new("write.join", "UPDATE / DELETE 里用导航", "M5"),
        new("tracking", "追踪与差异更新", "M5"),
        new("save.aggregate", "聚合根保存（savable）", "M5"),
        new("interceptor", "拦截器", "M5"),
        new("logicDelete", "逻辑删除", "M5"),
        new("version", "乐观锁版本", "M5"),
        new("column.encryption", "列加密", "M5"),
        new("column.converter", "值转换、枚举、JSON 列", "M5"),
        new("dialect.mssql", "SQL Server 方言", "M6"),
        new("dialect.mssql2008", "SQL Server 2008（ROW_NUMBER 分页）", "M6"),
        new("dialect.dameng", "达梦方言", "M6"),
        new("dialect.kingbase", "人大金仓方言", "M6"),
        new("dialect.duckdb", "DuckDB 方言", "M6"),
        new("dialect.clickhouse", "ClickHouse 方言", "M6"),
        new("dialect.h2", "H2 方言", "M6"),
        new("dialect.hana", "SAP HANA 方言", "M6"),
        new("dialect.functions", "方言静态入口与专属函数", "M6"),
        new("write.returning", "UPDATE / DELETE … RETURNING", "M6"),
        new("codeFirst", "建表与迁移（code-first）", "M7"),
        new("table.rename", "换表名（asTable / 表名映射）", "M7"),
        new("table.schema", "换 schema（asSchema）", "M7"),
        new("table.link", "跨库链接（asTableLink）", "M7"),
        new("sharding", "分库分表", "M7"),
        new("dto.object", "按对象筛选排序（whereObject / orderByObject）", "M7"),
        new("search", "search 扩展", "M7"),
        new("cache", "二级缓存", "M7"),
        new("misc.infra", "测试基础设施与 Java 专属（代理、注入、Bean 工具）", "M0"),
    ];

    private static readonly Dictionary<string, Capability> ByKey = All.ToDictionary(c => c.Key);

    /// <summary>按 key 取能力；未登记的 key 直接报错，防止规则写错静默归类。</summary>
    public static Capability Get(string key) =>
        ByKey.TryGetValue(key, out var c) ? c : throw new InvalidOperationException($"能力 '{key}' 没有在 Capabilities.All 里登记");

    // fork 用例：按文件名归类（live 与 sql 两份共用同一套名字）
    private static readonly (Regex Pattern, string Key)[] ForkFileRules =
    [
        (new(@"^fork/.*DateTrunc"), "func.date"),
        (new(@"^fork/.*Grouping"), "query.group"),
        (new(@"^fork/.*NullSafe|^fork/.*SmallFixes"), "func.general"),
        (new(@"^fork/.*Window"), "window"),
        (new(@"^fork/.*InsertSelect"), "write.insert"),
        (new(@"^fork/.*JsonValueConverter|^fork/json/|^fork/types/"), "column.converter"),
        (new(@"^fork/.*TableNameMapping|^fork/mapping/"), "table.rename"),
        (new(@"^fork/.*Returning"), "write.returning"),
        (new(@"^fork/.*LockWait"), "lock"),
        (new(@"^fork/.*Hana(Dialect|KeysAndTypes)|^fork/dialect/HanaSqlTest"), "dialect.hana"),
        (new(@"^fork/.*(DialectEntry|MySQLJson|PgSQLM4|HanaText)"), "dialect.functions"),
        (new(@"^fork/.*(ProxySmoke|Upstream)"), "misc.infra"),
    ];

    // 上游用例：按文件归类（方言目录、分片、缓存等整文件都是同一种能力）
    private static readonly (Regex Pattern, string Key)[] FileRules =
    [
        (new(@"^sharding/|Sharding(Base)?Test\.java$"), "sharding"),
        (new(@"^cache/"), "cache"),
        (new(@"^search/"), "search"),
        (new(@"^migration/|MigrationTest\.java$"), "codeFirst"),
        (new(@"^mssqlrownumber/"), "dialect.mssql2008"),
        (new(@"^mssql/"), "dialect.mssql"),
        (new(@"^dameng/"), "dialect.dameng"),
        (new(@"^kingbase/"), "dialect.kingbase"),
        (new(@"^duckdb/"), "dialect.duckdb"),
        (new(@"^click/"), "dialect.clickhouse"),
        (new(@"^h2/"), "dialect.h2"),
        (new(@"UpdateDeleteJoinTest|DeleteUpdateJoinTest"), "write.join"),
        (new(@"Encryption|^encryption/"), "column.encryption"),
        (new(@"LogicDelete|^logicdel/"), "logicDelete"),
        (new(@"VersionTest|^version/"), "version"),
        (new(@"Interceptor|^interceptor/"), "interceptor"),
        (new(@"TrackTest|^eqdiff/"), "tracking"),
        (new(@"^increment/"), "column.generated"),
        (new(@"^conversion/|^enums/"), "column.converter"),
    ];

    // 按方法体里的 API 归类，越靠前越具体
    private static readonly (Regex Pattern, string Key)[] BodyRules =
    [
        (new(@"\.savable\("), "save.aggregate"),
        (new(@"syncTableCommand|getDatabaseCodeFirst|createTableCommand"), "codeFirst"),
        (new(@"asTracking|TrackManager|addTracking"), "tracking"),
        (new(@"mapQueryable"), "query.map"),
        (new(@"asTableLink"), "table.link"),
        (new(@"\.asSchema\("), "table.schema"),
        (new(@"\.flatElement\(|\.elements\("), "nav.flat"),
        (new(@"subQueryToGroupJoin"), "nav.groupJoin"),
        (new(@"selectAutoInclude"), "dto.autoInclude"),
        (new(@"whereObject|orderByObject|filterConfigure"), "dto.object"),
        (new(@"toTreeList|asTreeCTE"), "tree"),
        (new(@"fillMany|fillOne"), "fill"),
        (new(@"\.include\(|\.includes\("), "nav.include"),
        (new(@"\.joining\("), "func.joining"),
        (new(@"partitionBy|rowNumberOver|\.lag\(|\.lead\(|firstValue|lastValue|nthValue|\.offset\("), "window"),
        (new(@"caseWhen\("), "expr.case"),
        (new(@"toCteAs|withCte"), "query.cte"),
        (new(@"\.union\(|\.unionAll\("), "query.union"),
        (new(@"sqlNativeSegment|sqlSegment|sqlQuery|executeSQL|sqlQueryMap|toResultSet"), "sql.native"),
        (new(@"forUpdate|forShare|NoWait|SkipLocked"), "lock"),
        (new(@"onConflictThen|insertOrUpdate"), "write.upsert"),
        (new(@"setSQLStrategy|\.batch\(\)"), "write.strategy"),
        (new(@"\.insertable\("), "write.insert"),
        (new(@"\.updatable\("), "write.update"),
        (new(@"\.deletable\("), "write.delete"),
        (new(@"JsonObject|jsonObject|JSONObject|JSONArray|jsonArray|toJSON", RegexOptions.IgnoreCase), "func.json"),
        (new(@"plus(Days|Months|Years|Hours|Minutes|Seconds|Weeks)|duration\(|toDays|toHours|toMinutes|toSeconds|dayOfWeek|dayOfYear|\.format\(|\.now\(\)|utcNow"), "func.date"),
        (new(@"subString|leftPad|rightPad|ltrim|rtrim|toLower|toUpper|stringFormat|nullEmpty|compareTo|\.trim\(|\.replace\(|\.indexOf\(|\.length\(\)"), "func.string"),
        (new(@"\.abs\(|ceiling|\.floor\(|\.round\(|power|sqrt|\.sign\(|\.log\(|\.exp\(|truncate|\.mod\("), "func.math"),
        (new(@"maxColumns|minColumns|nullOrDefault|equalsWith"), "func.general"),
        (new(@"toPageResult|toShardingPageResult"), "query.page"),
        (new(@"groupBy\(|having\("), "query.group"),
        (new(@"leftJoin|rightJoin|innerJoin|crossJoin|fullJoin"), "query.join"),
        (new(@"streamBy|toChunk|toStreamResult|\.stream\("), "query.stream"),
        (new(@"\.toMap\(|\.toMaps\("), "query.map"),
        (new(@"cloneQueryable"), "query.reuse"),
        (new(@"\.asTable\("), "table.rename"),
        (new(@"\.(any|none|count|sum|max|min|avg)\("), "nav.subquery"),
    ];

    /// <summary>把一个 Java 用例归到一种能力。</summary>
    public static string Classify(string file, string body)
    {
        foreach (var (pattern, key) in ForkFileRules)
            if (pattern.IsMatch(file)) return key;
        if (!file.StartsWith("fork/", StringComparison.Ordinal))
            foreach (var (pattern, key) in FileRules)
                if (pattern.IsMatch(file)) return key;
        foreach (var (pattern, key) in BodyRules)
            if (pattern.IsMatch(body)) return key;
        return "query.basic";
    }
}
