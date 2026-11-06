namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>对应 Java：pgsql/InsertTest.java 的冲突处理（ON CONFLICT … DO NOTHING / DO UPDATE SET … = EXCLUDED.…）。</summary>
public sealed class PgInsertTest(PgFixture fixture) : PgTest(fixture)
{
    private static TopicAuto NewTopicAuto() => new() { Stars = 999, Title = "title999", CreateTime = DateTime.Now.AddDays(99) };

    private static EasyDb Pg => SqlOnly.Db("pgsql");

    private const string Insert = "INSERT INTO \"t_topic_auto\" (\"stars\",\"title\",\"create_time\") VALUES (?,?,?)";

    // 换表、换 schema 后照样写冲突忽略（Java 执行失败后取 SQL；C# 直接渲染）
    [Fact, Upstream("pgsql/InsertTest.java#insertTest1")]
    public void InsertTest1() =>
        Assert.Equal("INSERT INTO \"xxx\".\"t_topic_autoaaa\" (\"stars\",\"title\",\"create_time\") VALUES (?,?,?) ON CONFLICT DO NOTHING",
            Pg.Insert(NewTopicAuto()).OnConflictIgnore().DisableInterceptors().AsTable(o => o + "aaa").AsSchema("xxx").ToSql());

    [Fact, Upstream("pgsql/InsertTest.java#insertDuplicateKeyIgnore2")]
    public void InsertDuplicateKeyIgnore2() =>
        Assert.Equal(Insert + " ON CONFLICT DO NOTHING", Pg.Insert(NewTopicAuto()).OnConflictIgnore().ToSql());

    // 更新列里去掉冲突列；自增主键不在插入列里，也不进冲突列与更新列
    [Fact, Upstream("pgsql/InsertTest.java#insertDuplicateKeyUpdate2")]
    public void InsertDuplicateKeyUpdate2() =>
        Assert.Equal(Insert + " ON CONFLICT (\"title\") DO UPDATE SET \"stars\" = EXCLUDED.\"stars\", \"create_time\" = EXCLUDED.\"create_time\"",
            Pg.Insert(NewTopicAuto()).OnConflictUpdate(s => new { s.Stars, s.CreateTime }, s => s.Title).ToSql());

    [Fact, Upstream("pgsql/InsertTest.java#insertDuplicateKeyUpdate2_1")]
    public void InsertDuplicateKeyUpdate2_1() =>
        Assert.Equal(Insert + " ON CONFLICT (\"title\") DO NOTHING", Pg.Insert(NewTopicAuto()).OnConflictIgnore(s => s.Title).ToSql());

    [Fact, Upstream("pgsql/InsertTest.java#insertDuplicateKeyUpdate3")]
    public void InsertDuplicateKeyUpdate3()
    {
        Assert.Equal(Insert + " ON CONFLICT (\"title\") DO UPDATE SET \"stars\" = EXCLUDED.\"stars\", \"create_time\" = EXCLUDED.\"create_time\"",
            Pg.Insert(NewTopicAuto()).OnConflictUpdate(conflictColumns: s => s.Title).ToSql());
        Assert.Equal(Insert + " ON CONFLICT (\"stars\") DO UPDATE SET \"title\" = EXCLUDED.\"title\"",
            Pg.Insert(NewTopicAuto()).OnConflictUpdate(s => new { s.Id, s.Title }, s => new { s.Id, s.Stars }).ToSql());
        Assert.Equal(Insert + " ON CONFLICT (\"stars\") DO UPDATE SET \"title\" = EXCLUDED.\"title\", \"create_time\" = EXCLUDED.\"create_time\"",
            Pg.Insert(NewTopicAuto()).OnConflictUpdate(conflictColumns: s => new { s.Id, s.Stars }).ToSql());
    }

    // 冲突时按主键更新全部列：第二次插入同一主键改成更新
    [Fact, Upstream("pgsql/InsertTest.java#insertTest2")]
    public async Task InsertTest2()
    {
        await Db.Delete<BlogEntity>().WhereById("200").ExecuteAsync(Ct);
        var begin = new DateTime(2000, 1, 1, 1, 1, 1);
        var blog = new BlogEntity
        {
            Id = "200", CreateBy = "200", CreateTime = begin.AddDays(1), UpdateBy = "200", UpdateTime = begin.AddDays(1), Title = "title200", Content = "content200",
            Url = "http://blog.easy-query.com/200", Star = 1, Score = 1.2m, Status = 1, Order = 1.2m, IsTop = false, Top = false, Deleted = false,
        };
        Assert.Equal(1, await Db.Insert(blog).OnConflictUpdate().ExecuteAsync(Ct));
        blog.Content = "abc";
        Assert.Equal(1, await Db.Insert(blog).OnConflictUpdate().ExecuteAsync(Ct));
        Assert.Equal("abc", (await Db.Query<BlogEntity>().WhereById("200").FirstOrDefaultAsync(Ct))!.Content);
        await Db.Delete<BlogEntity>().WhereById("200").ExecuteAsync(Ct);
    }
}
