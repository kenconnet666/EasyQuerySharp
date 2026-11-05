using EasyQuerySharp.Commands;
using EasyQuerySharp.Dialects;

namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>
/// 对应 Java：InsertTest1.java（换表名插入、冲突时全部更新、按字典插入时列的 SQL 模板与键名换算）。
/// 按批次合并相同语句的 testInsert 随批量写入（DbBatch）移植。
/// </summary>
public sealed class InsertTest1(MySqlFixture fixture) : MySqlTest(fixture)
{
    private static Topic NewTopic() => new() { Id = "1", Stars = 101, Title = "标题1", CreateTime = new DateTime(2021, 1, 1, 1, 1, 1) };

    [Fact, Upstream("InsertTest1.java#testColumnConfigure", NotApplicable = "Java 用例体是空的")]
    public void TestColumnConfigure() { }

    [Fact, Upstream("InsertTest1.java#inertTest1")]
    public async Task InertTest1()
    {
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => Db.Insert(NewTopic()).AsTable("t_topic_abc").ExecuteAsync(Ct));
        SqlOnly.AssertStatement(ex.Statement!, "INSERT INTO `t_topic_abc` (`id`,`stars`,`title`,`create_time`) VALUES (?,?,?,?)",
            "1(String),101(Integer),标题1(String),2021-01-01T01:01:01(LocalDateTime)");
    }

    // 冲突时更新全部列（Java 的 onConflictThen(s -> s.FETCHER.allFields())）：主键不在更新列里
    [Fact, Upstream("InsertTest1.java#inertTest2")]
    public async Task InertTest2()
    {
        var ex = await Assert.ThrowsAsync<EasyQueryException>(() => Db.Insert(NewTopic()).AsTable("t_topic_abc").OnConflictUpdate().ExecuteAsync(Ct));
        SqlOnly.AssertStatement(ex.Statement!,
            "INSERT INTO `t_topic_abc` (`id`,`stars`,`title`,`create_time`) VALUES (?,?,?,?) ON DUPLICATE KEY UPDATE `stars` = VALUES(`stars`), `title` = VALUES(`title`), `create_time` = VALUES(`create_time`)",
            "1(String),101(Integer),标题1(String),2021-01-01T01:01:01(LocalDateTime)");
    }

    // 字典插入时某列的值用模板包一层：ifnull(列,0)+值
    [Fact, Upstream("InsertTest1.java#testColumnConfigure2")]
    public void TestColumnConfigure2()
    {
        var row = new Dictionary<string, object?> { ["id"] = "1", ["stars"] = "2" };
        SqlOnly.AssertStatement(Db.InsertMap("xxx", row).ColumnSql("stars", "ifnull({0},0)+{1}").ToStatement(),
            "INSERT INTO `xxx` (`id`,`stars`) VALUES (?,ifnull(`stars`,0)+?)", "1(String),2(String)");
    }

    // 键名换算成大写（Java 的 UpperMapKeyNameConversion），插入与按字典更新都生效
    [Fact, Upstream("InsertTest1.java#tesInsertMapKey2")]
    public void TesInsertMapKey2()
    {
        var db = new EasyDb(o =>
        {
            o.Use(OracleDialect.Instance);
            o.UpdateStrategy = ColumnStrategy.OnlyNotNull;
            o.MapKeyConversion = key => key.ToUpperInvariant();
        });
        var row = new Dictionary<string, object?> { ["id"] = "1", ["stars"] = "2", ["UserName"] = "2" };
        SqlOnly.AssertStatement(db.InsertMap("xxx", row).ColumnSql("stars", "ifnull({0},0)+{1}").ToStatement(),
            "INSERT INTO \"xxx\" (\"ID\",\"STARS\",\"USERNAME\") VALUES (?,ifnull(\"STARS\",0)+?,?)", "1(String),2(String),2(String)");
        var update = new Dictionary<string, object?> { ["id"] = "123", ["name"] = "123", ["name1"] = null };
        Assert.Equal("UPDATE \"aaa\" SET \"NAME\" = ?,\"NAME1\" = ? WHERE \"ID\" = ?",
            db.UpdateMap("aaa", update).Columns(ColumnStrategy.All).WhereColumns("id").ToSql());
    }
}
