using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"函数"页：字符串、日期、数值、通用、CASE、类型转换、JSON、窗口函数、自定义函数。</summary>
public static partial class FunctionExamples
{
    // ---- 字符串

    /// <summary>字符串函数的结果。</summary>
    public sealed record TextFunctions(string Upper, int Length, string First, string Replaced, string Trimmed, int Position);

    [Example("func", "字符串：BCL 方法直接写")]
    public static Task<List<TextFunctions>> Strings(EasyDb db) =>
        db.Query<Post>().Where(p => p.Id <= 2).OrderBy(p => p.Id)
            .Select(p => new TextFunctions(p.Title.ToUpper(), p.Title.Length, p.Title.Substring(0, 2), p.Title.Replace("查询", "query"),
                (" " + p.Title + " ").Trim(), p.Title.IndexOf("join")))
            .ToListAsync();

    [Example("func", "补齐：PadLeft / PadRight（SQLite 没有 LPAD，拼出填充字符）")]
    public static Task<List<string>> Pad(EasyDb db) =>
        db.Query<User>().Where(u => u.Id <= 3).OrderBy(u => u.Id).Select(u => u.Name.PadLeft(6, '*')).ToListAsync();

    [Example("func", "拼接：+、string.Concat、插值字符串")]
    public static Task<List<string>> Concat(EasyDb db) =>
        db.Query<User>().Where(u => u.Age != null).OrderBy(u => u.Id).Take(3).Select(u => $"{u.Name}（{u.Age} 岁）").ToListAsync();

    [Example("func", "包含：Contains 的 % 在参数里，LikeContains 写成 LIKE CONCAT")]
    public static Task<List<string>> LikeVariants(EasyDb db) =>
        db.Query<Post>().Where(p => p.Title.LikeContains("SQL") || p.Title.Like("%缓存%")).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();

    [Example("func", "空与空白：string.IsNullOrEmpty / IsNullOrWhiteSpace")]
    public static Task<List<long>> Blank(EasyDb db) =>
        db.Query<Profile>().Where(p => string.IsNullOrEmpty(p.Website)).OrderBy(p => p.UserId).Select(p => p.UserId).ToListAsync();

    // ---- 日期

    /// <summary>日期函数的结果。</summary>
    public sealed record DateParts(int Year, int Month, string Day, DateTime NextWeek, int DaysSinceNewYear);

    [Example("func", "日期：取部分、格式化、加减、相差")]
    public static Task<List<DateParts>> Dates(EasyDb db)
    {
        var newYear = new DateTime(2025, 1, 1);
        return db.Query<Order>().Where(o => o.Id <= 2).OrderBy(o => o.Id)
            .Select(o => new DateParts(o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.ToString("yyyy-MM-dd"), o.CreatedAt.AddDays(7), (o.CreatedAt - newYear).Days))
            .ToListAsync();
    }

    /// <summary>每月的订单数。</summary>
    public sealed record MonthOrders(DateTime Month, long Orders);

    [Example("func", "日期截断：按月分组")]
    public static Task<List<MonthOrders>> DateTrunc(EasyDb db) =>
        db.Query<Order>().GroupBy(o => o.CreatedAt.DateTrunc(DateTruncUnit.Month))
            .Select(g => new MonthOrders(g.Key, g.Count())).OrderBy(r => r.Month).ToListAsync();

    // ---- 数值

    /// <summary>数值函数的结果。</summary>
    public sealed record Amounts(decimal Total, decimal Rounded, decimal Floor, decimal Ceiling, decimal Abs);

    [Example("func", "数值：Math 的方法直接写")]
    public static Task<List<Amounts>> Numbers(EasyDb db) =>
        db.Query<OrderItem>().Where(i => i.OrderId == 7).OrderBy(i => i.ProductId)
            .Select(i => new Amounts(i.Price * i.Quantity, Math.Round(i.Price * 0.85m, 1), Math.Floor(i.Price), Math.Ceiling(i.Price), Math.Abs(i.Price - 100)))
            .ToListAsync();

    // ---- 通用

    /// <summary>通用函数的结果。</summary>
    public sealed record General(string Name, int Age, int? NotZero, int Larger);

    [Example("func", "NULL 处理与一行里取最大：??、NullIf、Sql.MaxColumns")]
    public static Task<List<General>> NullHandling(EasyDb db) =>
        db.Query<User>().Where(u => u.Id >= 5 && u.Id <= 7).OrderBy(u => u.Id)
            .Select(u => new General(u.Name, u.Age ?? 0, u.Age.NullIf(17), Sql.MaxColumns(u.Age ?? 0, 30)))
            .ToListAsync();

    [Example("func", "null 安全的相等：EqualsNullSafe")]
    public static Task<List<string>> NullSafeEquals(EasyDb db)
    {
        int? age = null;
        return db.Query<User>().Where(u => u.Age.EqualsNullSafe(age)).Select(u => u.Name).ToListAsync();
    }

    /// <summary>帖子与热度分档。</summary>
    public sealed record PostHeat(string Title, string Heat);

    [Example("func", "CASE WHEN：三元表达式")]
    public static Task<List<PostHeat>> CaseWhen(EasyDb db) =>
        db.Query<Post>().OrderBy(p => p.Id).Take(5)
            .Select(p => new PostHeat(p.Title, p.Views >= 2000 ? "热门" : p.Views >= 500 ? "普通" : "冷门")).ToListAsync();

    [Example("func", "类型转换：ToString() 写成 CAST")]
    public static Task<List<string>> Casts(EasyDb db) =>
        db.Query<Post>().Where(p => p.Views.ToString().StartsWith("8")).OrderBy(p => p.Id).Select(p => p.Title + "：" + p.Views.ToString()).ToListAsync();

    [Example("func", "Convert.ToXxx：各类型的重载都写成 CAST")]
    public static Task<List<string>> ConvertTo(EasyDb db) =>
        db.Query<Post>().Where(p => Convert.ToDecimal(p.Views) / 1000 >= 2).OrderBy(p => p.Id).Select(p => Convert.ToString(p.Views)).ToListAsync();

    // ---- JSON

    /// <summary>用户 id 与技能数、第一项技能。</summary>
    public sealed record SkillSummary(long UserId, int Count, string? First);

    [Example("func", "JSON：Sql.Json(列) 取长度、取元素", Unsupported = "mssql,oracle,dameng")]
    public static Task<List<SkillSummary>> Json(EasyDb db) =>
        db.Query<Profile>().Where(p => Sql.Json(p.Skills).Length() >= 3).OrderBy(p => p.UserId)
            .Select(p => new SkillSummary(p.UserId, Sql.Json(p.Skills).Length(), Sql.Json(p.Skills).Text(0))).ToListAsync();

    // ---- 窗口函数

    /// <summary>帖子在作者内的排名与作者的总阅读量。</summary>
    public sealed record PostRank(string Title, long AuthorId, long Rank, int AuthorViews);

    [Example("func", "窗口函数：ROW_NUMBER 与分区求和")]
    public static Task<List<PostRank>> Windows(EasyDb db) =>
        db.Query<Post>().Where(p => p.AuthorId <= 3).OrderBy(p => p.AuthorId).ThenBy(p => p.Id)
            .Select(p => new PostRank(p.Title, p.AuthorId,
                Sql.Over.RowNumber(w => w.PartitionBy(p.AuthorId).OrderByDescending(p.Views)),
                Sql.Over.Sum(p.Views, w => w.PartitionBy(p.AuthorId))))
            .ToListAsync();

    /// <summary>按时间累计的阅读量。</summary>
    public sealed record RunningViews(string Title, int Views, int Running);

    [Example("func", "窗口范围：ROWS BETWEEN … AND CURRENT ROW")]
    public static Task<List<RunningViews>> WindowFrame(EasyDb db) =>
        db.Query<Post>().Where(p => p.PublishedAt != null).OrderBy(p => p.PublishedAt).Take(4)
            .Select(p => new RunningViews(p.Title, p.Views,
                Sql.Over.Sum(p.Views, w => w.OrderBy(p.PublishedAt).Rows(WindowBound.UnboundedPreceding, WindowBound.CurrentRow))))
            .ToListAsync();

    // ---- 原生片段与自定义函数

    [Example("func", "原生片段：Sql.Raw")]
    public static Task<List<string>> RawFragment(EasyDb db) =>
        db.Query<User>().Where(u => Sql.Raw<int>("LENGTH({0})", u.Name) == 2).OrderBy(u => u.Id).Take(3).Select(u => u.Name).ToListAsync();

    [Example("func", "自定义函数：[SqlFunction]")]
    public static Task<List<string>> CustomFunction(EasyDb db) =>
        db.Query<Post>().Where(p => p.Title.RemoveSpaces().Contains("N+1")).Select(p => p.Title).ToListAsync();

    // <snippet:func-sql-function>
    [SqlFunction("REPLACE({0}, ' ', '')")]
    public static string RemoveSpaces(this string value) => throw new InvalidOperationException("只能在查询里用");
    // </snippet:func-sql-function>

    /// <summary>方言专属函数（不是卡片：只能在对应的库上渲染，这里保证代码能编译）。</summary>
    public static IEnumerable<object> DialectFunctions(EasyDb db)
    {
        // <snippet:func-dialect>
        // MySQL：JSON_CONTAINS、全文索引 MATCH … AGAINST
        var mysql = db.Query<Profile>()
            .Where(p => Sql.MySql.Json(p.Skills).Contains("\"SQL\""))
            .Where(p => Sql.MySql.Match(p.Bio).Against("迁移"));

        // PostgreSQL：jsonb 的 @>、全文检索
        var pg = db.Query<Profile>()
            .Where(p => Sql.Pg.Jsonb(p.Skills).Contains("[\"SQL\"]"))
            .Where(p => Sql.Pg.FullText(p.Bio).Matches("simple", "迁移"));
        // </snippet:func-dialect>
        return [mysql, pg];
    }
}
