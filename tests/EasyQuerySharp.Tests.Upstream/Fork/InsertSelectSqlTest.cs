namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>对应 Java fork：insert/InsertSelectSqlTest.java（INSERT … SELECT：来源查询包成 eq_src，按列名写进目标表，可追加固定值列）。</summary>
public sealed class InsertSelectSqlTest
{
    [Fact, Upstream("fork/insert/InsertSelectSqlTest.java#mysqlCopiesEntityColumnsPlusExtraColumn")]
    public void MysqlCopiesEntityColumnsPlusExtraColumn() =>
        SqlOnly.AssertStatement(SqlOnly.Db("mysql").Query<ForkPost>().Where(p => new[] { "u1", "u2" }.Contains(p.UserId))
                .InsertInto("t_fork_post_archive").Column("archived_at", new DateTime(2026, 10, 28)).ToStatement(),
            "INSERT INTO `t_fork_post_archive` (`id`,`user_id`,`title`,`amount`,`archived_at`)"
            + " SELECT eq_src.`id`,eq_src.`user_id`,eq_src.`title`,eq_src.`amount`,?"
            + " FROM (SELECT `id`,`user_id`,`title`,`amount` FROM `t_fork_post` WHERE `user_id` IN (?,?)) eq_src",
            "2026-10-28T00:00(LocalDateTime),u1(String),u2(String)");

    [Fact, Upstream("fork/insert/InsertSelectSqlTest.java#defaultTargetIsTheEntityTable")]
    public void DefaultTargetIsTheEntityTable() =>
        Assert.Equal("INSERT INTO \"t_fork_post\" (\"id\",\"user_id\",\"title\",\"amount\") SELECT eq_src.\"id\",eq_src.\"user_id\",eq_src.\"title\",eq_src.\"amount\""
            + " FROM (SELECT \"id\",\"user_id\",\"title\",\"amount\" FROM \"t_fork_post\" WHERE \"title\" = ?) eq_src",
            SqlOnly.Db("pgsql").Query<ForkPost>().Where(p => p.Title == "x").InsertInto().ToSql());

    [Fact, Upstream("fork/insert/InsertSelectSqlTest.java#hanaUsesQuotedNames")]
    public void HanaUsesQuotedNames() =>
        Assert.Equal("INSERT INTO \"t_fork_post_archive\" (\"id\",\"user_id\",\"title\",\"amount\") SELECT eq_src.\"id\",eq_src.\"user_id\",eq_src.\"title\",eq_src.\"amount\""
            + " FROM (SELECT \"id\",\"user_id\",\"title\",\"amount\" FROM \"t_fork_post\") eq_src",
            SqlOnly.Db("hana").Query<ForkPost>().InsertInto("t_fork_post_archive").ToSql());
}
