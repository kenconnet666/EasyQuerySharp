using System.Diagnostics.CodeAnalysis;

namespace EasyQuerySharp;

/// <summary>
/// 只能写在查询表达式里的 SQL 标记方法：翻译器认得它们，运行时直接调用会抛出 <see cref="InvalidOperationException"/>。
/// 列上的函数做成扩展方法（扩展属性写不进表达式树，见 0001）。
/// </summary>
[SuppressMessage("Usage", "CA1801:Review unused parameters", Justification = "参数只在表达式树里被翻译")]
public static partial class Sql
{
    /// <summary>
    /// 原生 SQL 插值里的标识符：<c>$"SELECT * FROM {Sql.Identifier("t_user")}"</c> 按方言加引号（MySQL 反引号、PG 双引号、SQL Server 方括号）。
    /// 与其它 Sql 方法不同，它在运行时求值，不写在查询表达式里。
    /// </summary>
    public static SqlIdentifier Identifier(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new SqlIdentifier(name);
    }

    /// <summary>
    /// 原样的 LIKE：<paramref name="pattern"/> 里的 % 与 _ 由调用方自己写。
    /// 一般写法用 <see cref="string.Contains(string)"/>（<c>%v%</c>）、<see cref="string.StartsWith(string)"/>（<c>v%</c>）、<see cref="string.EndsWith(string)"/>（<c>%v</c>）。
    /// </summary>
    public static bool Like(this string? value, string pattern) => throw OnlyInQuery();

    /// <summary>原样的 NOT LIKE。</summary>
    public static bool NotLike(this string? value, string pattern) => throw OnlyInQuery();

    /// <summary>
    /// 包含（对应 Java 的 contains）：值原样作参数，% 在 SQL 里拼：<c>LIKE CONCAT('%',?,'%')</c>。
    /// 与 <see cref="string.Contains(string)"/>（参数里带 %：<c>LIKE ?</c>，对应 Java 的 like）结果相同，只是 SQL 写法不同。
    /// </summary>
    public static bool LikeContains(this string? value, string? part) => throw OnlyInQuery();

    /// <summary>以 <paramref name="part"/> 开头（对应 Java 的 startsWith）：<c>LIKE CONCAT(?,'%')</c>。</summary>
    public static bool LikeStartsWith(this string? value, string? part) => throw OnlyInQuery();

    /// <summary>以 <paramref name="part"/> 结尾（对应 Java 的 endsWith）：<c>LIKE CONCAT('%',?)</c>。</summary>
    public static bool LikeEndsWith(this string? value, string? part) => throw OnlyInQuery();

    /// <summary>
    /// 原生 SQL 片段，<c>{0}</c>、<c>{1}</c>… 依次替换为参数（列或值，值会参数化）；对应 Java 的 sqlSegment。
    /// </summary>
    /// <example><code>.Select(b => Sql.Raw&lt;long&gt;("rank() over(order by {0} desc)", b.Stars))</code></example>
    public static T Raw<T>(string template, params object?[] arguments) => throw OnlyInQuery();

    /// <summary>
    /// 投影里的值读出后在客户端换算（对应 Java 的 valueConvert）：SQL 只选 <paramref name="value"/>，
    /// 读到的值再交给 <paramref name="convert"/>。<c>.Select(t =&gt; new { Label = Sql.ClientSide(t.Stars, s =&gt; s + "-") })</c>。
    /// </summary>
    public static TResult ClientSide<T, TResult>(T value, Func<T, TResult> convert) => throw OnlyInQuery();

    /// <summary>
    /// 按属性路径动态取列（对应 Java 的 anyColumn）：<c>Sql.Column&lt;int?&gt;(c, "User.Age")</c> 等同于 <c>c.User.Age</c>，
    /// 路径里的属性名忽略大小写，可以经过对一导航。路径在翻译时解析，适合按前端传入的字段名排序、过滤。
    /// </summary>
    public static T Column<T>(object? entity, string path) => throw OnlyInQuery();

    /// <summary>
    /// 转换成 <typeparamref name="T"/>（对应 Java 的 toNumber / asStr / cast），写法按方言，如 MySQL 的 <c>CAST(x AS SIGNED)</c>。
    /// 常见写法也能直接用：<c>int.Parse(t.Title)</c>、<c>Convert.ToInt64(x)</c>、<c>t.Stars.ToString()</c>。
    /// </summary>
    public static T Cast<T>(object? value) => throw OnlyInQuery();

    /// <summary>
    /// 把客户端的值作为参数放进 SQL，之后的运算在数据库里做（对应 Java 的 expression().constant(x)）：
    /// <c>b.Title.Contains(Sql.Constant("ABc").ToLower())</c> → <c>LIKE CONCAT('%',LOWER(?),'%')</c>。
    /// </summary>
    public static T Constant<T>(T value) => throw OnlyInQuery();

    /// <summary>排序键：NULL 排在最前（对应 Java 的 asc(NULLS_FIRST) / desc(NULLS_FIRST)）：<c>.OrderBy(t => Sql.NullsFirst(t.CreateTime))</c>。</summary>
    public static T NullsFirst<T>(T value) => throw OnlyInQuery();

    /// <summary>排序键：NULL 排在最后：<c>.OrderByDescending(t => Sql.NullsLast(t.Score))</c>。</summary>
    public static T NullsLast<T>(T value) => throw OnlyInQuery();

    /// <summary>
    /// 条件组：原样输出并加括号 <c>(a AND b)</c>，不与外层的 AND 合并（对应 Java 的 and(x -> …) 分组）。
    /// 结果与不加括号等价，只在需要与既有 SQL 逐字一致时使用。
    /// </summary>
    public static bool Group(bool predicate) => throw OnlyInQuery();

    // 拼表达式树时包一层 Sql.Group(...)
    internal static System.Linq.Expressions.Expression GroupExpression(System.Linq.Expressions.Expression predicate) =>
        System.Linq.Expressions.Expression.Call(typeof(Sql).GetMethod(nameof(Group))!, predicate);

    /// <summary>
    /// 闭区间 <c>lower &lt;= x &lt;= upper</c>，渲染为 <c>(x &gt;= ? AND x &lt;= ?)</c>（对应 Java 的 rangeClosed）。
    /// x 是函数（如 <c>b.CreateTime ?? def</c>）、两端都是值时渲染为 <c>BETWEEN</c>，与 Java 一致。
    /// 配合 <see cref="Querying.QueryBase{TSelf,T}.UseValueFilter"/> 时，被拒绝的一端（如 null）不加条件。
    /// </summary>
    public static bool RangeClosed<T>(this T value, T lower, T upper) => throw OnlyInQuery();

    /// <summary>开区间 <c>lower &lt; x &lt; upper</c>（对应 Java 的 rangeOpen）。</summary>
    public static bool RangeOpen<T>(this T value, T lower, T upper) => throw OnlyInQuery();

    /// <summary>左闭右开 <c>lower &lt;= x &lt; upper</c>（对应 Java 的 rangeClosedOpen）。</summary>
    public static bool RangeClosedOpen<T>(this T value, T lower, T upper) => throw OnlyInQuery();

    /// <summary>左开右闭 <c>lower &lt; x &lt;= upper</c>（对应 Java 的 rangeOpenClosed）。</summary>
    public static bool RangeOpenClosed<T>(this T value, T lower, T upper) => throw OnlyInQuery();

    /// <summary>区间标记方法的两端是否为开区间；不是区间方法时返回 null。</summary>
    internal static (bool LowerOpen, bool UpperOpen)? RangeKind(System.Reflection.MethodInfo method) =>
        method.DeclaringType != typeof(Sql) ? null
        : method.Name switch
        {
            nameof(RangeClosed) => (false, false),
            nameof(RangeOpen) => (true, true),
            nameof(RangeClosedOpen) => (false, true),
            nameof(RangeOpenClosed) => (true, false),
            _ => null,
        };

    /// <summary>数据库的当前时间（MySQL 的 <c>NOW()</c>）；与 <c>DateTime.Now</c> 不同，后者是客户端的值，作为参数传。</summary>
    public static DateTime Now() => throw OnlyInQuery();

    /// <summary>数据库的当前 UTC 时间（MySQL 的 <c>UTC_TIMESTAMP()</c>）。</summary>
    public static DateTime UtcNow() => throw OnlyInQuery();

    /// <summary>数据库的随机数（对应 Java 的 random()），多用于随机排序：<c>.OrderBy(t =&gt; Sql.Random())</c>。</summary>
    public static double Random() => throw OnlyInQuery();

    /// <summary>
    /// 截断到 <paramref name="unit"/> 的起点，更小的部分清零（对应 Java 的 dateTrunc）：PG 写 <c>DATE_TRUNC('month', x)</c>，
    /// MySQL 用 DATE_FORMAT 清零后转回 DATETIME；周从周一开始。其余方言不支持时渲染期抛出 <see cref="NotSupportedException"/>。
    /// </summary>
    public static DateTime DateTrunc(this DateTime value, DateTruncUnit unit) => throw OnlyInQuery();

    /// <summary>同 <see cref="DateTrunc(DateTime, DateTruncUnit)"/>，可空的日期。</summary>
    public static DateTime? DateTrunc(this DateTime? value, DateTruncUnit unit) => throw OnlyInQuery();

    /// <summary><c>NULLIF(x, other)</c>：等于 <paramref name="other"/> 时为 NULL，否则是原值；常见用法 <c>u.Name.NullIf("")</c> 把空串当 NULL（对应 Java 的 nullIf）。</summary>
    public static T? NullIf<T>(this T value, T other) => throw OnlyInQuery();

    /// <summary>
    /// null 安全的相等：两边都为 NULL 也算相等（对应 Java 的 eqNullSafe）。MySQL 写 <c>a &lt;=&gt; b</c>，PG 写 <c>a IS NOT DISTINCT FROM b</c>，
    /// SQLite 写 <c>a IS b</c>，其余写成 <c>((a = b) OR (a IS NULL AND b IS NULL))</c>。
    /// </summary>
    public static bool EqualsNullSafe<T>(this T value, T other) => throw OnlyInQuery();

    /// <summary>
    /// <paramref name="value"/> 在逗号分隔的列表 <paramref name="csv"/> 里（对应 MySQL 的 <c>FIND_IN_SET</c>、TS 版的 inCsv）：不去空格，
    /// <paramref name="csv"/> 为 NULL 时不匹配。PG 写 <c>(值)::text = ANY(string_to_array(列表,','))</c>，SQLite / Oracle 用 <c>instr</c>、
    /// SQL Server 用 <c>CHARINDEX</c> 在两头补了逗号的列表里找。导航上的 <see cref="Metadata.KeyMatch.SelfCsv"/> 就写成它。
    /// </summary>
    public static bool InCsv<T>(T value, string? csv) => throw OnlyInQuery();

    /// <summary>ISO 星期几：周一 1 … 周日 7（C# 的 <c>x.DayOfWeek</c> 是周日 0 … 周六 6）。</summary>
    public static int IsoDayOfWeek(this DateTime value) => throw OnlyInQuery();

    /// <summary>ISO 星期几：周一 1 … 周日 7。</summary>
    public static int IsoDayOfWeek(this DateOnly value) => throw OnlyInQuery();

    /// <summary>值在给定集合里（<c>IN (…)</c>）；与 <c>ids.Contains(x)</c> 等价，写法更接近 SQL。</summary>
    public static bool In<T>(this T value, params T[] values) => throw OnlyInQuery();

    /// <summary>值不在给定集合里（<c>NOT IN (…)</c>）。</summary>
    public static bool NotIn<T>(this T value, params T[] values) => throw OnlyInQuery();

    /// <summary>值在子查询的结果里：<c>x IN (SELECT …)</c>。子查询须投影成单列。</summary>
    public static bool In<T>(this T value, Querying.Query<T> subQuery) => throw OnlyInQuery();

    /// <summary>值不在子查询的结果里：<c>x NOT IN (SELECT …)</c>。</summary>
    public static bool NotIn<T>(this T value, Querying.Query<T> subQuery) => throw OnlyInQuery();

    /// <summary>
    /// 子查询有结果：<c>EXISTS(SELECT 1 …)</c>。子查询的条件可以引用外层查询的参数（关联子查询）：
    /// <c>.Where(t => Sql.Exists(db.Query&lt;Blog&gt;().Where(b => b.Id == t.Id)))</c>。
    /// </summary>
    public static bool Exists<TSelf, T>(Querying.QueryBase<TSelf, T> subQuery) where TSelf : Querying.QueryBase<TSelf, T> =>
        throw OnlyInQuery();

    /// <summary>子查询没有结果：<c>NOT EXISTS(SELECT 1 …)</c>。</summary>
    public static bool NotExists<TSelf, T>(Querying.QueryBase<TSelf, T> subQuery) where TSelf : Querying.QueryBase<TSelf, T> =>
        throw OnlyInQuery();

    /// <summary>
    /// 只返回一个值的子查询 <c>(SELECT …)</c>，可以放在投影、条件、排序里（对应 Java 的 subQueryColumn）。
    /// 子查询可以引用外层参数：<c>BlogCount = Sql.SubQuery(db.Query&lt;Blog&gt;().Where(b => b.Id == t.Id).Select(b => Sql.Count(b.Id)))</c>。
    /// </summary>
    public static T SubQuery<T>(Querying.Query<T> subQuery) => throw OnlyInQuery();

    /// <summary>
    /// 这个集合导航的隐式子查询不带逻辑删除条件（对应 Java 的 nav().disableLogicDelete()）：
    /// <c>.Where(u => u.Blogs!.DisableLogicDelete().Any(b => …))</c>。
    /// </summary>
    public static IEnumerable<T> DisableLogicDelete<T>(this IEnumerable<T> navigation) => throw OnlyInQuery();

    /// <summary>
    /// 这个对一导航的隐式 JOIN 不带逻辑删除条件（对应 Java 的 nav().configure(r -> r.disableLogicDelete())）：
    /// <c>.Where(u => Sql.DisableLogicDelete(u.MyBlog)!.Star == 1)</c>。同一导航带不同配置时各自 JOIN 一次。
    /// </summary>
    public static T DisableLogicDelete<T>(T navigation) where T : class? => throw OnlyInQuery();

    /// <summary>
    /// 这个集合导航的隐式子查询（或分组合并）改用表 <paramref name="tableName"/>（对应 Java 的 nav().configure(r -> r.asTable(..))）：
    /// <c>.Where(u => u.Blogs!.AsTable("t_blog_2024").Any(b => b.Star == 1))</c>。
    /// </summary>
    public static IEnumerable<T> AsTable<T>(this IEnumerable<T> navigation, string tableName) => throw OnlyInQuery();

    /// <summary>
    /// 这个对一导航的隐式 JOIN 改用表 <paramref name="tableName"/>：<c>.Where(u => Sql.AsTable(u.MyBlog, "t_blog_2024")!.Star == 1)</c>。
    /// 同一导航带不同配置时各自 JOIN 一次。
    /// </summary>
    public static T AsTable<T>(T navigation, string tableName) where T : class? => throw OnlyInQuery();

    /// <summary>
    /// 这个集合导航的隐式子查询（或分组合并）里目标表用别名 <paramref name="alias"/>（对应 Java 的 nav().configure(r -> r.asAlias(..))），
    /// 别名仍占用自动编号：<c>.Where(u => u.Roles!.AsAlias("myRole").Any(r => …))</c>。
    /// </summary>
    public static IEnumerable<T> AsAlias<T>(this IEnumerable<T> navigation, string alias) => throw OnlyInQuery();

    /// <summary>这个对一导航的隐式 JOIN 用别名 <paramref name="alias"/>：<c>Sql.AsAlias(u.Company, "c")!.Name</c>。同一导航带不同配置时各自 JOIN 一次。</summary>
    public static T AsAlias<T>(T navigation, string alias) where T : class? => throw OnlyInQuery();

    // ---------- Map 投影 ----------

    /// <summary>
    /// 实体的全部列，作为 Map 投影（键是列名，对应 Java 的 MapProxy.selectAll）。可再追加别的键：
    /// <c>.Select((b, t) => new Dictionary&lt;string, object?&gt;(Sql.Columns(b)) { { "xx", t.CreateTime } })</c>。
    /// </summary>
    public static Dictionary<string, object?> Columns<T>(T entity) => throw OnlyInQuery();

    /// <summary>实体除去 <paramref name="excluded"/>（写成 <c>b.CreateTime, b.Title</c>）以外的全部列（对应 Java 的 selectAll + selectIgnores）。</summary>
    public static Dictionary<string, object?> ColumnsExcept<T>(T entity, params object?[] excluded) => throw OnlyInQuery();

    /// <summary>
    /// 整个实体但不选 <paramref name="excluded"/> 这几列（对应 Java 的 entity.selectIgnores(..)），读出后这些属性保持默认值；
    /// 可以嵌在 Part / DTO 里：<c>new Part&lt;BankCard, string?&gt;(Sql.Without(c, c.Id), c.Bank!.Name)</c>。
    /// </summary>
    public static T Without<T>(T entity, params object?[] excluded) => throw OnlyInQuery();

    // ---------- 聚合（不分组时作用于整个结果，分组时作用于每组）----------

    /// <summary><c>COUNT(*)</c>。</summary>
    public static long Count() => throw OnlyInQuery();

    /// <summary><c>COUNT(x)</c>：非 NULL 的个数。</summary>
    public static long Count<T>(T value) => throw OnlyInQuery();

    /// <summary><c>COUNT((CASE WHEN filter THEN x ELSE NULL END))</c>：满足条件的非 NULL 个数（对应 Java 的 count().filter(…)）。</summary>
    public static long Count<T>(T value, bool filter) => throw OnlyInQuery();

    /// <summary><c>COUNT(DISTINCT x)</c>。</summary>
    public static long CountDistinct<T>(T value) => throw OnlyInQuery();

    /// <summary><c>COUNT(DISTINCT (CASE WHEN filter THEN x ELSE NULL END))</c>。</summary>
    public static long CountDistinct<T>(T value, bool filter) => throw OnlyInQuery();

    /// <summary>字符串聚合（对应 Java 的 joining）：MySQL 的 <c>GROUP_CONCAT(x SEPARATOR ?)</c>、PG / SQL Server 的 STRING_AGG、Oracle 的 LISTAGG。</summary>
    public static string StringJoin<T>(T value, string separator) => throw OnlyInQuery();

    /// <summary><c>SUM(x)</c>。</summary>
    public static T Sum<T>(T value) => throw OnlyInQuery();

    /// <summary><c>SUM((CASE WHEN filter THEN x ELSE 0 END))</c>。</summary>
    public static T Sum<T>(T value, bool filter) => throw OnlyInQuery();

    /// <summary><c>SUM(DISTINCT x)</c>。</summary>
    public static T SumDistinct<T>(T value) => throw OnlyInQuery();

    /// <summary><c>SUM(DISTINCT (CASE WHEN filter THEN x ELSE 0 END))</c>。</summary>
    public static T SumDistinct<T>(T value, bool filter) => throw OnlyInQuery();

    /// <summary><c>MAX(x)</c>。</summary>
    public static T Max<T>(T value) => throw OnlyInQuery();

    /// <summary><c>MAX((CASE WHEN filter THEN x ELSE NULL END))</c>。</summary>
    public static T Max<T>(T value, bool filter) => throw OnlyInQuery();

    /// <summary><c>MIN(x)</c>。</summary>
    public static T Min<T>(T value) => throw OnlyInQuery();

    /// <summary><c>MIN((CASE WHEN filter THEN x ELSE NULL END))</c>。</summary>
    public static T Min<T>(T value, bool filter) => throw OnlyInQuery();

    /// <summary><c>AVG(x)</c>。</summary>
    public static T Avg<T>(T value) => throw OnlyInQuery();

    /// <summary><c>AVG((CASE WHEN filter THEN x ELSE NULL END))</c>。</summary>
    public static T Avg<T>(T value, bool filter) => throw OnlyInQuery();

    /// <summary><c>AVG(DISTINCT x)</c>。</summary>
    public static T AvgDistinct<T>(T value) => throw OnlyInQuery();

    /// <summary><c>AVG(DISTINCT (CASE WHEN filter THEN x ELSE NULL END))</c>。</summary>
    public static T AvgDistinct<T>(T value, bool filter) => throw OnlyInQuery();

    // ---------- 通用 ----------

    /// <summary><c>(CASE a WHEN b THEN 1 ELSE 0 END)</c>：相等为真（对应 Java 的 equalsWith）。</summary>
    public static bool EqualsWith<T>(T a, T b) => throw OnlyInQuery();

    internal static InvalidOperationException OnlyInQuery() =>
        new("这个方法只能写在 EasyQuerySharp 的查询表达式里，由翻译器转成 SQL，不能直接调用");
}
