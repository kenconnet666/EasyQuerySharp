using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream;

// QueryTest.java 里的 union / unionAll 用例。Java 的 easyQueryClient（字符串属性名）与 easyEntityQuery 两种写法在 C# 里是同一个 API
public sealed partial class QueryTest
{
    private static string TopicPart(string alias, string? where = null) =>
        "(SELECT " + alias + ".`id`," + alias + ".`stars`," + alias + ".`title`," + alias + ".`create_time` FROM `t_topic` " + alias + (where is null ? "" : " WHERE " + where) + ")";

    private static string TopicUnion(string outer, string op, params string[] parts) =>
        "SELECT " + outer + ".`id`," + outer + ".`stars`," + outer + ".`title`," + outer + ".`create_time` FROM ( " + string.Join("  " + op + "  ", parts) + " ) " + outer;

    private Query<Topic> NotNullId => Db.Query<Topic>().Where(o => o.Id != null);

    private Query<Topic> NullId => Db.Query<Topic>().Where(o => o.Id == null);

    private Query<Topic> Id123 => Db.Query<Topic>().Where(o => o.Id == "123");

    [Fact, Upstream("QueryTest.java#query52"), Upstream("QueryTest.java#query52_1")]
    public void Query52() =>
        Assert.Equal(TopicUnion("t3", "UNION", TopicPart("t", "t.`id` IS NOT NULL"), TopicPart("t1", "t1.`id` IS NULL"), TopicPart("t2", "t2.`id` = ?")),
            NotNullId.Union(NullId, Id123).ToSql());

    [Fact, Upstream("QueryTest.java#query52_1_1"), Upstream("QueryTest.java#query52_1_2")]
    public async Task Query52_1_1()
    {
        // Java 用例开头顺带跑了一个自连接查询，这里同样执行一次
        await Db.Query<Topic>().LeftJoin<Topic>((t, t1) => t.Id == t1.Id).Where((a, b) => a.Id == "1").Where((a, b) => a.Id == b.Id).FirstOrDefaultAsync(Ct);
        Assert.Equal(TopicUnion("t3", "UNION ALL", TopicPart("t", "t.`id` IS NOT NULL"), TopicPart("t1", "t1.`id` IS NULL"), TopicPart("t2", "t2.`id` = ?")),
            NotNullId.UnionAll(NullId, Id123).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query52_2"), Upstream("QueryTest.java#query52_3")]
    public void Query52_2() =>
        Assert.Equal(TopicUnion("t2", "UNION", TopicPart("t", "t.`id` IS NOT NULL"), TopicPart("t1", "t1.`id` = ?")), NotNullId.Union(Id123).ToSql());

    [Fact, Upstream("QueryTest.java#query52_3_1")]
    public void Query52_3_1() =>
        Assert.Equal(TopicUnion("t2", "UNION ALL", TopicPart("t", "t.`id` IS NOT NULL"), TopicPart("t1", "t1.`id` = ?")), NotNullId.UnionAll(Id123).ToSql());

    private Query<Topic> AllTopics3 => Db.Query<Topic>().Union(Db.Query<Topic>(), Db.Query<Topic>());

    private static readonly string AllTopics3Sql = TopicUnion("t3", "UNION", TopicPart("t"), TopicPart("t1"), TopicPart("t2"));

    [Fact, Upstream("QueryTest.java#query53")]
    public void Query53() => Assert.Equal(AllTopics3Sql, AllTopics3.ToSql());

    [Fact, Upstream("QueryTest.java#query54")]
    public void Query54() => Assert.Equal(AllTopics3Sql + " WHERE t3.`id` = ?", AllTopics3.Where(o => o.Id == "123321").ToSql());

    [Fact, Upstream("QueryTest.java#query55")]
    public void Query55() =>
        Assert.Equal("SELECT t3.`create_time`,t3.`stars`" + AllTopics3Sql[AllTopics3Sql.IndexOf(" FROM ( ", StringComparison.Ordinal)..],
            AllTopics3.Select(o => new Topic { CreateTime = o.CreateTime, Stars = o.Stars }).ToSql());

    [Fact, Upstream("QueryTest.java#query56")]
    public void Query56()
    {
        // Java 的 asStr() 只改变表达式类型、不生成 SQL；C# 用 (string)(object) 转换表达同样的意思
        var q4 = Db.Query<Topic>().Where(o => (string)(object)o.CreateTime! == "123");
        var inner = "(" + TopicUnion("t3", "UNION", TopicPart("t", "t.`id` IS NOT NULL"), TopicPart("t1", "t1.`id` IS NULL"), TopicPart("t2", "t2.`id` = ?")) + ")";
        Assert.Equal(TopicUnion("t5", "UNION ALL", inner, TopicPart("t4", "t4.`create_time` = ?")),
            NotNullId.Union(NullId, Id123).UnionAll(q4).ToSql());
    }

    [Fact, Upstream("QueryTest.java#query57")]
    public async Task Query57() => Assert.Empty(await AllTopics3.Where(o => o.Id == "123321").ToListAsync(Ct));
}
