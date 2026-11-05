namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8CommentTest.java（自关联的子评论每条主评论各取前几条）。</summary>
public sealed class M8CommentTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    [Fact, Upstream("mysql8/M8CommentTest.java#testPartitionBy")]
    public async Task TestPartitionBy()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Comment>().Include(m => m.SubComments, s => s.OrderBy(x => x.Time).Take(3)).ToListAsync(Ct);
        listen.AssertSql(0, "SELECT `id`,`parent_id`,`content`,`username`,`time` FROM `comment`");
        listen.AssertSql(1, "SELECT t2.`id` AS `id`,t2.`parent_id` AS `parent_id`,t2.`content` AS `content`,t2.`username` AS `username`,t2.`time` AS `time` FROM (SELECT t1.`id` AS `id`,t1.`parent_id` AS `parent_id`,t1.`content` AS `content`,t1.`username` AS `username`,t1.`time` AS `time` FROM (SELECT t.`id`,t.`parent_id`,t.`content`,t.`username`,t.`time`,(ROW_NUMBER() OVER (PARTITION BY t.`parent_id` ORDER BY t.`time` ASC)) AS `__row__` FROM `comment` t WHERE t.`parent_id` IN (?,?,?,?,?)) t1 WHERE t1.`__row__` >= ? AND t1.`__row__` <= ?) t2");
        listen.AssertParams(1, "1(String),2(String),3(String),4(String),5(String),1(Long),3(Long)");
    }
}
