using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// 对应 Java：pgsql/PartitionByTest.java。窗口函数投影成 <see cref="Part{TEntity,T1,T2}"/>，再按计算列过滤、再投影；
/// 只比对 PostgreSQL 的 SQL。
/// </summary>
public sealed class PgPartitionByTest
{
    private const string BlogColumns = "t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"title\",t.\"content\",t.\"url\",t.\"star\",t.\"publish_time\",t.\"score\",t.\"status\",t.\"order\",t.\"is_top\",t.\"top\"";

    private static string Sql16(string rowNumberOrder) =>
        "SELECT t1.\"__part__column1\" AS \"value1\",t1.\"__part__column2\" AS \"value2\",t1.\"id\" AS \"value3\" FROM (SELECT " + BlogColumns
        + ",(COUNT(t.\"star\") OVER (PARTITION BY t.\"title\")) AS \"__part__column1\",(ROW_NUMBER() OVER (PARTITION BY t.\"title\" ORDER BY " + rowNumberOrder
        + ")) AS \"__part__column2\" FROM \"t_blog\" t WHERE t.\"deleted\" = ? AND t.\"create_time\" > ?) t1 WHERE t1.\"star\" > ? AND t1.\"__part__column1\" < ? AND t1.\"__part__column2\" < ?";

    private const string Params16 = "false(Boolean),2020-01-01T01:01(LocalDateTime),1(Integer),2(Long),2(Long)";

    [Fact, Upstream("pgsql/PartitionByTest.java#testPartitionBy16_1")]
    public void TestPartitionBy16_1()
    {
        var db = SqlOnly.Db("pgsql");
        var statement = db.Query<BlogEntity>().Where(b => b.CreateTime > new DateTime(2020, 1, 1, 1, 1, 0))
            .Select(b => new Part<BlogEntity, long, long>(b, Sql.Over.Count(b.Star, w => w.PartitionBy(b.Title)), Sql.Over.RowNumber(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
            .Where(p => p.Entity.Star > 1 && p.Value1 < 2L && p.Value2 < 2L)
            .Select(p => new Draft<long, long, string?>(p.Value1, p.Value2, p.Entity.Id))
            .ToStatement();
        SqlOnly.AssertStatement(statement, Sql16("t.\"create_time\" ASC"), Params16);
    }

    // Java 的 orderBy(false, x)：条件为 false 时不加这个排序项；C# 直接不写
    [Fact, Upstream("pgsql/PartitionByTest.java#testPartitionBy16_2")]
    public void TestPartitionBy16_2()
    {
        var db = SqlOnly.Db("pgsql");
        var statement = db.Query<BlogEntity>().Where(b => b.CreateTime > new DateTime(2020, 1, 1, 1, 1, 0))
            .Select(b => new Part<BlogEntity, long, long>(b, Sql.Over.Count(b.Star, w => w.PartitionBy(b.Title)), Sql.Over.RowNumber(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime))))
            .Where(p => p.Entity.Star > 1 && p.Value1 < 2L && p.Value2 < 2L)
            .Select(p => new Draft<long, long, string?>(p.Value1, p.Value2, p.Entity.Id))
            .ToStatement();
        SqlOnly.AssertStatement(statement, Sql16("t.\"create_time\" ASC"), Params16);
    }

    [Fact, Upstream("pgsql/PartitionByTest.java#testPartitionBy16_3")]
    public void TestPartitionBy16_3()
    {
        var db = SqlOnly.Db("pgsql");
        var statement = db.Query<BlogEntity>().Where(b => b.CreateTime > new DateTime(2020, 1, 1, 1, 1, 0))
            .Select(b => new Part<BlogEntity, long, long>(b, Sql.Over.Count(b.Star, w => w.PartitionBy(b.Title)),
                Sql.Over.RowNumber(w => w.PartitionBy(b.Title).OrderBy(b.CreateTime).OrderBy(b.UpdateTime))))
            .Where(p => p.Entity.Star > 1 && p.Value1 < 2L && p.Value2 < 2L)
            .Select(p => new Draft<long, long, string?>(p.Value1, p.Value2, p.Entity.Id))
            .ToStatement();
        SqlOnly.AssertStatement(statement, Sql16("t.\"create_time\" ASC, t.\"update_time\" ASC"), Params16);
    }
}
