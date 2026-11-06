using System.Diagnostics.CodeAnalysis;

namespace EasyQuerySharp;

public static partial class Sql
{
    /// <summary>
    /// MySQL 专属函数的入口（对应 Java 的 MySQL.json / MySQL.match），只能写在查询表达式里，能用在隐式子查询中：
    /// <code>
    /// db.Query&lt;User&gt;().Where(u =&gt; Sql.MySql.Json(u.Attrs).Contains("\"vip\"", "$.tags"))      // JSON_CONTAINS(attrs,?,?)
    /// db.Query&lt;User&gt;().Where(u =&gt; Sql.MySql.Match(u.Title, u.Body).Against("easy query"))   // MATCH(..) AGAINST(? IN NATURAL LANGUAGE MODE)
    /// </code>
    /// 用在别的方言上时渲染期抛出 <see cref="NotSupportedException"/>。读 JSON 里的值用通用的 <see cref="Json"/>。
    /// </summary>
    public static class MySql
    {
        /// <summary>把列当作 JSON 文档（JSON 类型或存 JSON 文字的列）。</summary>
        public static MySqlJson<T> Json<T>(T column) => throw OnlyInQuery();

        /// <summary>全文检索：列要在同一张表上并有覆盖它们的 FULLTEXT 索引。</summary>
        public static MySqlMatch Match(params object?[] columns) => throw OnlyInQuery();
    }

    /// <summary>
    /// PostgreSQL 专属函数的入口（对应 Java 的 PgSQL.jsonb / array / fulltext），只能写在查询表达式里，能用在隐式子查询中：
    /// <code>
    /// db.Query&lt;User&gt;().Where(u =&gt; Sql.Pg.Jsonb(u.Attrs).Contains("{\"vip\":true}"))   // (attrs)::jsonb @&gt; (?)::jsonb
    /// db.Query&lt;Post&gt;().Where(p =&gt; Sql.Pg.Array(p.Tags).Has("orm"))                 // ? = ANY(tags)
    /// db.Query&lt;Post&gt;().Where(p =&gt; Sql.Pg.FullText(p.Body).Matches("easy -jdbc"))
    /// </code>
    /// 用在别的方言上时渲染期抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    public static class Pg
    {
        /// <summary>把列当作 jsonb（jsonb、json 或存 JSON 文字的列，都先转成 jsonb）。</summary>
        public static PgJsonb<T> Jsonb<T>(T column) => throw OnlyInQuery();

        /// <summary>数组列（<c>text[]</c>、<c>int[]</c> …）的条件。</summary>
        public static PgArray Array(object? column) => throw OnlyInQuery();

        /// <summary>全文检索：查询串用 <c>websearch_to_tsquery</c> 解析（<c>easy query</c>、<c>"短语"</c>、<c>a or b</c>、<c>-排除</c>）。</summary>
        public static PgFullText FullText(object? column) => throw OnlyInQuery();
    }

    /// <summary>
    /// SAP HANA 专属函数的入口（对应 Java 的 Hana.text），只能写在查询表达式里，能用在隐式子查询中：
    /// <code>
    /// db.Query&lt;Doc&gt;().Where(d =&gt; Sql.Hana.Text(d.Title, d.Body).Fuzzy("easy qurey", 0.8))   // CONTAINS((title,body),?,FUZZY(0.8))
    ///     .OrderByDescending(d =&gt; Sql.Hana.Text(d.Title).Score())                            // SCORE()
    /// </code>
    /// 用在别的方言上时渲染期抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    public static class Hana
    {
        /// <summary>全文检索：列上要有 FULLTEXT INDEX（模糊检索要 <c>FUZZY SEARCH INDEX ON</c>），否则 CONTAINS 按整列的值匹配。</summary>
        public static HanaText Text(params object?[] columns) => throw OnlyInQuery();
    }
}

/// <summary>
/// 一列 JSON 在 MySQL 上的函数（由 <see cref="Sql.MySql.Json{T}"/> 得到）。候选值是 JSON 文字（如 <c>"\"vip\""</c>、<c>"{\"a\":1}"</c>），
/// path 形如 <c>$.a.b</c>、<c>$.tags[0]</c>。函数按 MySQL 8.0.17+ 可用的来选。
/// </summary>
[SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class MySqlJson<T>
{
    private MySqlJson()
    {
    }

    /// <summary>文档包含 <paramref name="candidate"/>：<c>JSON_CONTAINS(col,?)</c>。</summary>
    public bool Contains(string candidate) => throw Sql.OnlyInQuery();

    /// <summary>path 处的值包含 <paramref name="candidate"/>：<c>JSON_CONTAINS(col,?,?)</c>。</summary>
    public bool Contains(string candidate, string path) => throw Sql.OnlyInQuery();

    /// <summary>两个文档有共同的元素或键值对：<c>JSON_OVERLAPS(col,?)</c>。</summary>
    public bool Overlaps(string document) => throw Sql.OnlyInQuery();

    /// <summary>这一列是 JSON 数组且含有 <paramref name="value"/>：<c>? MEMBER OF(col)</c>。</summary>
    public bool HasMember(object? value) => throw Sql.OnlyInQuery();

    /// <summary>path 处的数组含有 <paramref name="value"/>：<c>? MEMBER OF(JSON_EXTRACT(col,?))</c>。</summary>
    public bool HasMember(object? value, string path) => throw Sql.OnlyInQuery();

    /// <summary>至少有一个 path 存在：<c>JSON_CONTAINS_PATH(col,'one',?,…)</c>。</summary>
    public bool ContainsPath(params string[] paths) => throw Sql.OnlyInQuery();

    /// <summary>所有 path 都存在：<c>JSON_CONTAINS_PATH(col,'all',?,…)</c>。</summary>
    public bool ContainsAllPaths(params string[] paths) => throw Sql.OnlyInQuery();

    /// <summary>
    /// 按调用顺序修改这一列的 JSON，结果与列同类型，用在更新里写回：
    /// <c>.Set(u =&gt; u.Attrs, u =&gt; Sql.MySql.Json(u.Attrs).Edit(e =&gt; e.Set("$.level", 3).Remove("$.old")))</c>。
    /// </summary>
    public T Edit(Func<MySqlJsonEdit, MySqlJsonEdit> changes) => throw Sql.OnlyInQuery();
}

/// <summary>一串 MySQL JSON 修改，按调用顺序套在列外面（<c>JSON_REMOVE(JSON_SET(col,…),…)</c>）。</summary>
[SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class MySqlJsonEdit
{
    private MySqlJsonEdit()
    {
    }

    /// <summary>把 path 处设为 <paramref name="value"/>（文字或数；布尔、对象、数组用 <see cref="SetJson"/>）：<c>JSON_SET(…,?,?)</c>。</summary>
    public MySqlJsonEdit Set(string path, object? value) => throw Sql.OnlyInQuery();

    /// <summary>把 path 处设为一段 JSON（<c>"true"</c>、<c>"{\"a\":1}"</c>）：<c>JSON_SET(…,?,JSON_EXTRACT(?,'$'))</c>（MariaDB 没有 <c>CAST(? AS JSON)</c>）。</summary>
    public MySqlJsonEdit SetJson(string path, string json) => throw Sql.OnlyInQuery();

    /// <summary>往 path 处的数组末尾追加 <paramref name="value"/>：<c>JSON_ARRAY_APPEND(…,?,?)</c>。</summary>
    public MySqlJsonEdit ArrayAppend(string path, object? value) => throw Sql.OnlyInQuery();

    /// <summary>删掉这些 path：<c>JSON_REMOVE(…,?,…)</c>。</summary>
    public MySqlJsonEdit Remove(params string[] paths) => throw Sql.OnlyInQuery();
}

/// <summary>MySQL 全文检索的方式。</summary>
public enum MySqlMatchMode
{
    /// <summary>自然语言（默认）：<c>IN NATURAL LANGUAGE MODE</c>。</summary>
    NaturalLanguage,

    /// <summary>布尔检索（<c>+easy -slow "exact phrase"</c>）：<c>IN BOOLEAN MODE</c>。</summary>
    Boolean,

    /// <summary>自然语言加查询扩展：<c>WITH QUERY EXPANSION</c>。</summary>
    QueryExpansion,
}

/// <summary>MySQL 全文检索 <c>MATCH(列) AGAINST(? 方式)</c>（由 <see cref="Sql.MySql.Match"/> 得到）。</summary>
[SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class MySqlMatch
{
    private MySqlMatch()
    {
    }

    /// <summary>自然语言检索的条件。</summary>
    public bool Against(string words) => throw Sql.OnlyInQuery();

    /// <summary>按 <paramref name="mode"/> 检索的条件。</summary>
    public bool Against(string words, MySqlMatchMode mode) => throw Sql.OnlyInQuery();

    /// <summary>相关度（0 表示不相关），自然语言检索；放在投影或排序里。</summary>
    public double Score(string words) => throw Sql.OnlyInQuery();

    /// <summary>按 <paramref name="mode"/> 检索的相关度。</summary>
    public double Score(string words, MySqlMatchMode mode) => throw Sql.OnlyInQuery();
}

/// <summary>
/// 一列 jsonb 在 PostgreSQL 上的函数（由 <see cref="Sql.Pg.Jsonb{T}"/> 得到）。
/// 键是否存在用 <c>jsonb_exists*</c> 函数而不是 <c>?</c> 运算符（<c>?</c> 会和参数占位符混淆）。
/// </summary>
[SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class PgJsonb<T>
{
    private PgJsonb()
    {
    }

    /// <summary>包含 <paramref name="document"/>（JSON 文字）：<c>(col)::jsonb @&gt; (?)::jsonb</c>。</summary>
    public bool Contains(string document) => throw Sql.OnlyInQuery();

    /// <summary>被 <paramref name="document"/> 包含：<c>(col)::jsonb &lt;@ (?)::jsonb</c>。</summary>
    public bool ContainedBy(string document) => throw Sql.OnlyInQuery();

    /// <summary>顶层有这个键（或数组里有这个文字元素）：<c>jsonb_exists((col)::jsonb,?)</c>。</summary>
    public bool HasKey(string key) => throw Sql.OnlyInQuery();

    /// <summary>顶层有其中任一个键：<c>jsonb_exists_any((col)::jsonb,ARRAY[?,…]::text[])</c>。</summary>
    public bool HasAnyKey(params string[] keys) => throw Sql.OnlyInQuery();

    /// <summary>顶层这些键都有：<c>jsonb_exists_all((col)::jsonb,ARRAY[?,…]::text[])</c>。</summary>
    public bool HasAllKeys(params string[] keys) => throw Sql.OnlyInQuery();

    /// <summary>
    /// 按调用顺序局部修改这一列，结果与列同类型，用在更新里写回：
    /// <c>.Set(u =&gt; u.Attrs, u =&gt; Sql.Pg.Jsonb(u.Attrs).Edit(e =&gt; e.Set("level", "3").Remove("old")))</c>。
    /// </summary>
    public T Edit(Func<PgJsonbEdit, PgJsonbEdit> changes) => throw Sql.OnlyInQuery();
}

/// <summary>一串 jsonb 修改，按调用顺序套在 <c>(col)::jsonb</c> 外面。path 用点分隔（<c>a.b</c>、数组下标 <c>tags.0</c>），值是 JSON 文字。</summary>
[SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class PgJsonbEdit
{
    private PgJsonbEdit()
    {
    }

    /// <summary>把 path 处设为 <paramref name="json"/>（没有就加上）：<c>jsonb_set(…,(?)::text[],(?)::jsonb,true)</c>。</summary>
    public PgJsonbEdit Set(string path, string json) => throw Sql.OnlyInQuery();

    /// <summary>删掉 path 处（键或数组元素）：<c>(… #- (?)::text[])</c>。</summary>
    public PgJsonbEdit Remove(string path) => throw Sql.OnlyInQuery();

    /// <summary>与 <paramref name="document"/> 合并（同名键以它为准，数组则拼接）：<c>(… || (?)::jsonb)</c>。</summary>
    public PgJsonbEdit Merge(string document) => throw Sql.OnlyInQuery();
}

/// <summary>一列 PostgreSQL 数组的条件（由 <see cref="Sql.Pg.Array"/> 得到）。都写成 <c>? = ANY(col)</c>，参数与元素类型不必完全一致。</summary>
[SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class PgArray
{
    private PgArray()
    {
    }

    /// <summary>含有 <paramref name="value"/>：<c>? = ANY(col)</c>。</summary>
    public bool Has(object? value) => throw Sql.OnlyInQuery();

    /// <summary>这些值都有：<c>(? = ANY(col) AND ? = ANY(col) …)</c>。</summary>
    public bool HasAll(params object?[] values) => throw Sql.OnlyInQuery();

    /// <summary>至少有其中一个：<c>(? = ANY(col) OR …)</c>。</summary>
    public bool HasAny(params object?[] values) => throw Sql.OnlyInQuery();

    /// <summary>元素个数（NULL 数组为 NULL、空数组为 0）：<c>cardinality(col)</c>。</summary>
    public int? Length() => throw Sql.OnlyInQuery();
}

/// <summary>
/// PostgreSQL 全文检索（由 <see cref="Sql.Pg.FullText"/> 得到）。不传配置时用 <c>'simple'</c>；配置名直接写进 SQL（要与表达式索引一致才走索引），只接受标识符。
/// </summary>
[SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class PgFullText
{
    private PgFullText()
    {
    }

    /// <summary>匹配：<c>to_tsvector('simple',col) @@ websearch_to_tsquery('simple',?)</c>。</summary>
    public bool Matches(string query) => throw Sql.OnlyInQuery();

    /// <summary>用指定的文本搜索配置（如 <c>english</c>）匹配。</summary>
    public bool Matches(string config, string query) => throw Sql.OnlyInQuery();

    /// <summary>相关度，越大越相关：<c>ts_rank(to_tsvector(..),websearch_to_tsquery(..))</c>。</summary>
    public double Rank(string query) => throw Sql.OnlyInQuery();

    /// <summary>用指定的文本搜索配置算相关度。</summary>
    public double Rank(string config, string query) => throw Sql.OnlyInQuery();
}

/// <summary>SAP HANA 全文检索 <c>CONTAINS(列,?)</c>（由 <see cref="Sql.Hana.Text"/> 得到）；多列时写成 <c>CONTAINS((a,b),?)</c>。</summary>
[SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class HanaText
{
    private HanaText()
    {
    }

    /// <summary>精确检索（按词，支持 <c>"短语"</c>、<c>OR</c>、<c>-排除</c>、<c>*</c>）：<c>CONTAINS(col,?)</c>。</summary>
    public bool Contains(string query) => throw Sql.OnlyInQuery();

    /// <summary>容错检索，<paramref name="threshold"/> 是 0–1 的最低相似度（写进 SQL）：<c>CONTAINS(col,?,FUZZY(0.8))</c>。</summary>
    public bool Fuzzy(string query, double threshold) => throw Sql.OnlyInQuery();

    /// <summary>同一查询里 CONTAINS 的相关度（0–1）：<c>SCORE()</c>，放在投影或排序里。</summary>
    public double Score() => throw Sql.OnlyInQuery();
}
