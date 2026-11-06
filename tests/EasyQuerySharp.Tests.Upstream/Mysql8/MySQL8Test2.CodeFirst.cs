namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/MySQL8Test2.java 的 code-first 用例（带两个索引的表：同步、删表、再同步）；其余用例见 <see cref="MySQL8Test2"/>。</summary>
public sealed class MySQL8Test2CodeFirst(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    // Java 的 MD5 对应下面的语句（已按 Java 的写法核对过 MD5）；C# 建表、索引的写法与它一致，只差 column4 / create_time 上的列默认值：
    // CREATE TABLE IF NOT EXISTS `m8_test_index` ( \n`column1` VARCHAR(255) NOT NULL ,\n`column2` VARCHAR(255) NULL ,\n`column3` VARCHAR(255) NULL ,
    // \n`column4` VARCHAR(255) NULL  DEFAULT '',\n`create_time` DATETIME(3) NOT NULL  DEFAULT CURRENT_TIMESTAMP(3), \n PRIMARY KEY (`column1`)\n) Engine=InnoDB;
    // CREATE INDEX m8_test_index_column1_column2_idx ON `m8_test_index` (`column1` ASC,`column2` ASC);
    // CREATE UNIQUE INDEX m8_test_index_column2_uidx ON `m8_test_index` (`column2` ASC);
    [Fact(Skip = "等列默认值（Java 的 @Column(dbDefault)）定下来，见 ROADMAP 4.1"), Upstream("mysql8/MySQL8Test2.java#test", Pending = true)]
    public async Task Test()
    {
        await Db.CodeFirst.SyncTablesAsync([typeof(M8TestIndex)], Ct);
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(M8TestIndex)], Ct);
        var sync = await Db.CodeFirst.SyncTablesAsync([typeof(M8TestIndex)], Ct);
        Assert.Equal("dbbff7673035e7092de7c1c6aae99fcb", JavaMd5.OfCodeFirst(sync));
    }
}
