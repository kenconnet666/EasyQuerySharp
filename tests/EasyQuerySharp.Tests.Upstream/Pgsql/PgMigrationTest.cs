namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>对应 Java：pgsql/MigrationTest.java（PostgreSQL 上比较实体与库，只得出同步语句、不执行）。</summary>
public sealed class PgMigrationTest(PgFixture fixture) : PgTest(fixture)
{
    // Java 只打印语句：固件建好的 t_blog 与实体一致，没有语句；my_migration_blog 没有，整张建（注释接在建表语句后面）
    [Fact, Upstream("pgsql/MigrationTest.java#test1")]
    public async Task Test1()
    {
        var sync = await Db.CodeFirst.PlanSyncAsync([typeof(Topic), typeof(BlogEntity), typeof(MyMigrationBlog)], Ct);
        Assert.DoesNotContain(sync.Statements, s => s.Contains("\"t_blog\"", StringComparison.Ordinal));
        Assert.Contains(sync.Statements, s => s.StartsWith("CREATE TABLE IF NOT EXISTS \"my_migration_blog\" ( \n", StringComparison.Ordinal)
                                              && s.EndsWith("\nCOMMENT ON COLUMN \"my_migration_blog\".\"top\" IS '是否置顶';", StringComparison.Ordinal));
    }
}

/// <summary>
/// 对应 Java：migration/MigrationPgTest.java（PostgreSQL 上建表再删表）。Java 自己连 PG 的 easy-query-test 库，这里用 PG 固件的库。
/// </summary>
public sealed class MigrationPgTest(PgFixture fixture) : PgTest(fixture)
{
    // 建表语句（列注释是接在后面的 COMMENT ON）的 MD5 与 Java 一致，建好后是空表，删表后再查报错
    [Fact, Upstream("migration/MigrationPgTest.java#test")]
    public async Task Test()
    {
        if (await Db.CodeFirst.TableExistsAsync(typeof(MyMigrationBlog0), Ct))
        {
            await Db.Query<MyMigrationBlog0>().AnyAsync(Ct);
            await Db.CodeFirst.DropTablesIfExistsAsync([typeof(MyMigrationBlog0)], Ct);
        }
        var sync = await Db.CodeFirst.SyncTablesAsync([typeof(MyMigrationBlog0)], Ct);
        Assert.Equal("1cd82fb71caf196a00eaa729911127b2", JavaMd5.OfCodeFirst(sync));

        Assert.False(await Db.Query<MyMigrationBlog0>().AnyAsync(Ct));
        await Db.CodeFirst.DropTablesIfExistsAsync([typeof(MyMigrationBlog0)], Ct);
        await Assert.ThrowsAsync<EasyQueryException>(() => Db.Query<MyMigrationBlog0>().AnyAsync(Ct));
    }
}
