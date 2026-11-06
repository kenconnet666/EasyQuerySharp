namespace EasyQuerySharp.Tests.Upstream.Features;

/// <summary>
/// 对应 Java：MigrationTest.java（MySQL 上的 code-first 同步）。Java 的 <c>MigrationsSQLGenerator.generateMigrationSQL</c>
/// 对应 C# 的 <c>CodeFirst.PlanSyncAsync</c>（读库结构、得出语句、不执行），<c>syncTableCommand</c> + <c>executeWithTransaction</c>
/// 对应 <c>CodeFirst.SyncTablesAsync</c>；<c>@Table(oldName)</c> / <c>@Column(oldName)</c> 对应 <c>[RenamedFrom]</c>。
/// </summary>
public sealed class MigrationTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    // 库里已有 t_topic、t_blog 且与实体一致，只建 my_migration_blog
    [Fact, Upstream("MigrationTest.java#createTableSQLTest1")]
    public async Task CreateTableSQLTest1()
    {
        var sync = await Db.CodeFirst.PlanSyncAsync([typeof(Topic), typeof(BlogEntity), typeof(MyMigrationBlog)], Ct);
        Assert.Equal("CREATE TABLE IF NOT EXISTS `my_migration_blog` ( \n" +
                     "`id` VARCHAR(255) NOT NULL ,\n" +
                     "`create_time` DATETIME(3) NULL  COMMENT '创建时间;创建时间',\n" +
                     "`update_time` DATETIME(3) NULL  COMMENT '修改时间;修改时间',\n" +
                     "`create_by` VARCHAR(255) NULL  COMMENT '创建人;创建人',\n" +
                     "`update_by` VARCHAR(255) NULL  COMMENT '修改人;修改人',\n" +
                     "`deleted` TINYINT(1) NULL  COMMENT '是否删除;是否删除',\n" +
                     "`title` VARCHAR(255) NULL  COMMENT '标题',\n" +
                     "`content` VARCHAR(255) NULL  COMMENT '内容',\n" +
                     "`url` VARCHAR(255) NULL  COMMENT '博客链接',\n" +
                     "`star` INT(11) NULL  COMMENT '点赞数',\n" +
                     "`publish_time` DATETIME(3) NULL  COMMENT '发布时间',\n" +
                     "`score` DECIMAL(16,2) NULL  COMMENT '评分',\n" +
                     "`status` INT(11) NULL  COMMENT '状态',\n" +
                     "`order` DECIMAL(16,2) NULL  COMMENT '排序',\n" +
                     "`is_top` TINYINT(1) NULL  COMMENT '是否置顶',\n" +
                     "`top` TINYINT(1) NULL  COMMENT '是否置顶', \n" +
                     " PRIMARY KEY (`id`)\n" +
                     ") Engine=InnoDB;", Assert.Single(sync.Statements));
    }

    // 库里没有 t_topic2、有 t_topic：先改表名，再按旧表的列加缺的列
    [Fact, Upstream("MigrationTest.java#createTableSQLTest2")]
    public async Task CreateTableSQLTest2()
    {
        var sync = await Db.CodeFirst.PlanSyncAsync([typeof(NewTopic)], Ct);
        Assert.Equal(["ALTER TABLE `t_topic` RENAME TO `t_topic2`;", "ALTER TABLE `t_topic2` ADD `abc` VARCHAR(255) NULL;"], sync.Statements);
    }

    // 库里没有 abc、有 title：CHANGE 成新列名（连同声明的类型）
    [Fact, Upstream("MigrationTest.java#createTableSQLTest3")]
    public async Task CreateTableSQLTest3()
    {
        var sync = await Db.CodeFirst.PlanSyncAsync([typeof(NewTopic3)], Ct);
        Assert.Equal("ALTER TABLE `t_topic` CHANGE `title` `abc` varchar(200) NULL;", Assert.Single(sync.Statements));
    }

    // 建表语句的 MD5 与 Java 一致（Java 的 getSQL() 在每条语句前加换行），建好后是空表，删表后再查报错
    [Fact, Upstream("MigrationTest.java#createTableSQLTest4")]
    public async Task CreateTableSQLTest4()
    {
        if (await Db.CodeFirst.TableExistsAsync(typeof(MyMigrationBlog0), Ct))
        {
            await Db.Query<MyMigrationBlog0>().AnyAsync(Ct);
            await Db.CodeFirst.DropTablesIfExistsAsync([typeof(MyMigrationBlog0)], Ct);
        }
        var sync = await Db.CodeFirst.SyncTablesAsync([typeof(MyMigrationBlog0)], Ct);
        Assert.Equal("2a927e4ae0dbf7d6f6d687a50888df93", JavaMd5.OfCodeFirst(sync));

        Assert.False(await Db.Query<MyMigrationBlog0>().AnyAsync(Ct));
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(MyMigrationBlog0)], Ct);
        await Assert.ThrowsAsync<EasyQueryException>(() => Db.Query<MyMigrationBlog0>().AnyAsync(Ct));
    }
}
