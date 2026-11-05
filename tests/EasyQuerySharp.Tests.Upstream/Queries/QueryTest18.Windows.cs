namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>QueryTest18 的窗口函数：Java 的 Select.PART.of(t, rowNumberOver()…) 对应 C# 的 <c>new Part&lt;T, long&gt;(t, Sql.Over.RowNumber(…))</c>。</summary>
public sealed partial class QueryTest18
{
    private static string PartSql(string partition) => "SELECT t1.`id`,t1.`stars`,t1.`title`,t1.`create_time`,t1.`__part__column1` AS `__part__column1` FROM (SELECT t.`id`,t.`stars`,t.`title`,t.`create_time`,(ROW_NUMBER() OVER (PARTITION BY " + partition + " ORDER BY t.`id` ASC, t.`create_time` DESC)) AS `__part__column1` FROM `t_topic` t) t1 WHERE t1.`stars` > ? AND t1.`__part__column1` > ?";

    [Fact, Upstream("QueryTest18.java#testPartitionBy1")]
    public async Task TestPartitionBy1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>()
            .Select(t => new Part<Topic, int>(t, (int)Sql.Over.RowNumber(w => w.PartitionBy(t.Id).OrderBy(t.Id).OrderByDescending(t.CreateTime))))
            .Where(p => p.Entity.Stars > 1 && p.Value1 > 1)
            .ToListAsync(Ct);
        listen.AssertSql(PartSql("t.`id`"));
        listen.AssertParams("1(Integer),1(Integer)");
    }

    [Fact, Upstream("QueryTest18.java#testPartitionBy1_1")]
    public async Task TestPartitionBy1_1()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<Topic>()
            .Select(t => new Part<Topic, int>(t, (int)Sql.Over.RowNumber(w => w.PartitionBy(t.Id, t.Title).OrderBy(t.Id).OrderByDescending(t.CreateTime))))
            .Where(p => p.Entity.Stars > 1 && p.Value1 > 1)
            .ToListAsync(Ct);
        listen.AssertSql(PartSql("t.`id`, t.`title`"));
        Assert.All(list, p => Assert.NotNull(p.Entity));
    }

    [Fact, Upstream("QueryTest18.java#testPartition1", NotApplicable = "Java 用例体全部注释掉了")]
    public void TestPartition1() { }
}
