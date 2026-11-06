namespace EasyQuerySharp.Tests.Upstream.Dameng;

/// <summary>对应 Java：migration/MigrationDamengTest.java（达梦上建表再删表）。没有达梦实库，只核对建表语句。</summary>
public sealed class MigrationDamengTest
{
    private static EasyDb Db => SqlOnly.Db("dameng");

    // 表不存在时同步就是建表，这里直接取建表语句
    [Fact, Upstream("migration/MigrationDamengTest.java#test",
         Deviation = "达梦建表沿用 TS 版在 DM8 上实测的写法：一条语句一次执行，类型是 INT / BIT / VARCHAR2(255) / TIMESTAMP，列注释是单独的 COMMENT ON 语句；"
                     + "Java 写成 execute immediate '…'; 脚本、类型是 number(11) / number(1) / nvarchar2(255) / timestamp(6)，MD5（88632c6241761dfc3dbdd274080030dc）对不上。没有达梦实库，只核对 SQL 文本")]
    public void Test()
    {
        var statements = Db.CodeFirst.CreateTableSql(typeof(MyMigrationBlog0)).Split('\n');
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"my_migration_blog0\" (\"id\" VARCHAR2(255) NOT NULL, \"create_time\" TIMESTAMP NULL, \"update_time\" TIMESTAMP NULL, "
                     + "\"create_by\" VARCHAR2(255) NULL, \"update_by\" VARCHAR2(255) NULL, \"deleted\" BIT NULL, \"title\" VARCHAR2(255) NULL, \"content\" VARCHAR2(255) NULL, "
                     + "\"url\" VARCHAR2(255) NULL, \"star\" INT NULL, \"publish_time\" TIMESTAMP NULL, \"score\" DECIMAL(16,2) NULL, \"status\" INT NULL, "
                     + "\"order\" DECIMAL(16,2) NULL, \"is_top\" BIT NULL, \"top\" BIT NULL, PRIMARY KEY (\"id\"))", statements[0]);
        Assert.Equal(
        [
            "COMMENT ON COLUMN \"my_migration_blog0\".\"create_time\" IS '创建时间;创建时间'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"update_time\" IS '修改时间;修改时间'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"create_by\" IS '创建人;创建人'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"update_by\" IS '修改人;修改人'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"deleted\" IS '是否删除;是否删除'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"title\" IS '标题'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"content\" IS '内容'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"url\" IS '博客链接'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"star\" IS '点赞数'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"publish_time\" IS '发布时间'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"score\" IS '评分'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"status\" IS '状态'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"order\" IS '排序'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"is_top\" IS '是否置顶'",
            "COMMENT ON COLUMN \"my_migration_blog0\".\"top\" IS '是否置顶'",
        ], statements[1..]);
        Assert.Equal("DROP TABLE IF EXISTS \"my_migration_blog0\"", Db.Options.Dialect.DropTableIfExistsSql(Db.Metadata.Get<MyMigrationBlog0>()));
    }
}
