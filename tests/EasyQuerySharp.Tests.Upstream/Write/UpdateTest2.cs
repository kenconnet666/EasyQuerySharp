using EasyQuerySharp.Commands;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Filtering;

namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>
/// 对应 Java：UpdateTest2.java（全局的更新列策略、只作用于查询的值过滤、按属性名映射 DTO）。
/// Java 用 Oracle 方言连 MySQL 只为拿 SQL，这里用不连库的 Oracle 方言直接渲染。
/// </summary>
public sealed class UpdateTest2(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static EasyDb Oracle(Action<EasyDbOptions>? configure = null) => new(o =>
    {
        o.Use(OracleDialect.Instance);
        o.UpdateStrategy = ColumnStrategy.OnlyNotNull;
        configure?.Invoke(o);
    });

    // C# 的 DTO 投影本来就按属性名找列（Java 要换成 PropertyFirstEntityMappingRule）：title 取自列 abc
    [Fact, Upstream("UpdateTest2.java#test11")]
    public void Test11() =>
        SqlOnly.AssertStatement(Oracle().Query<Topic2>().Where(t => t.Title == "123").Select<Topic2DTO>().ToStatement(),
            "SELECT t.\"id\",t.\"abc\" AS \"title\" FROM \"t_topic\" t WHERE t.\"abc\" = ?", "123(String)");

    // 全局 UpdateStrategy = 只写非 null 列
    [Fact, Upstream("UpdateTest2.java#testUpdateNull")]
    public void TestUpdateNull()
    {
        var db = Oracle();
        SqlOnly.AssertStatement(db.Update(new Topic { Id = "12312333sss", Title = "123" }).ToStatement(),
            "UPDATE \"t_topic\" SET \"title\" = ? WHERE \"id\" = ?", "123(String),12312333sss(String)");
        SqlOnly.AssertStatement(db.Update(new Topic { Id = "12312333sss", Title = "123", Stars = 1 }).ToStatement(),
            "UPDATE \"t_topic\" SET \"stars\" = ?,\"title\" = ? WHERE \"id\" = ?", "1(Integer),123(String),12312333sss(String)");
    }

    // 查询的默认值过滤跳过 null 条件；更新、删除不受影响（stars = null 照写）
    [Fact, Upstream("UpdateTest2.java#testUpdateValueTest",
         Deviation = "C# 的 == null 按 C# 语义翻译成 IS NULL（Java 的 eq(null) 写成 = ? 并传 null，永远不成立）；值过滤只作用于查询这一点相同")]
    public void TestUpdateValueTest()
    {
        var db = Oracle(o => o.QueryValueFilter = ValueFilter.NotNullOrBlank);
        int? stars = null;
        SqlOnly.AssertStatement(db.Update<Topic>().Set(t => t.Title, "1").Where(t => t.Stars == stars).ToStatement(),
            "UPDATE \"t_topic\" SET \"title\" = ? WHERE \"stars\" IS NULL", "1(String)");
        SqlOnly.AssertStatement(db.Delete<Topic>().Where(t => t.Stars == stars).ToStatement()!,
            "DELETE FROM \"t_topic\" WHERE \"stars\" IS NULL");
        SqlOnly.AssertStatement(db.Query<Topic>().Where(t => t.Stars == stars).Where(t => t.Title == "123").ToStatement(),
            "SELECT \"id\",\"stars\",\"title\",\"create_time\" FROM \"t_topic\" WHERE \"title\" = ?", "123(String)");
    }

    // UPDATE 的条件里用同表的子查询：MySQL 要求包成派生表（Java 的 select("*")）
    [Fact, Upstream("UpdateTest2.java#update1")]
    public async Task Update1() =>
        await Db.Update<Topic>().Set(t => t.Stars, 123123)
            .Where(t => t.Id.In(Db.Query<Topic>().Select(t1 => Sql.Max(t1.Id)).AsSubQuery()) && t.Id == "x123321xxs")
            .ExecuteAsync(Ct);
}
