using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：sqlite/SQLiteTest.java（SQLite 上整表查询、日期区间条件，读出后序列化成 JSON；code-first 建表）。
/// Java 连一个现成的 SQLite 文件库；这里用共享的内存库，code-first 建表并写入一行。
/// 建表用例的列名是 Java 的 SQLite 库里的大驼峰（<c>"Id"</c>、<c>"CreateTime"</c>），用 <see cref="Metadata.NameConversion.UpperCamelCase"/> 的库。
/// </summary>
public sealed class SQLiteTest : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 共享内存库只要还有一个连接开着就一直在
    private readonly SqliteConnection _keepAlive = new("Data Source=eqs-sqlite-test;Mode=Memory;Cache=Shared");
    private readonly EasyDb _db = new(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource("Data Source=eqs-sqlite-test;Mode=Memory;Cache=Shared")));

    public async ValueTask InitializeAsync()
    {
        await _keepAlive.OpenAsync(Ct);
        await _db.CodeFirst.DropTablesIfExistsAsync([typeof(BlogEntity)], Ct);
        await _db.CodeFirst.CreateTablesAsync([typeof(BlogEntity)], Ct);
        await _db.Insert(new BlogEntity
        {
            Id = "1", Title = "title1", Content = "content1", Url = "http://blog.easy-query.com/1", Star = 1, Score = 1.2m, Status = 1, Order = 1.2m,
            IsTop = false, Top = true, Deleted = false, CreateBy = "1", UpdateBy = "1",
            CreateTime = new DateTime(2020, 2, 15, 1, 0, 0), UpdateTime = new DateTime(2020, 2, 15, 1, 0, 0), PublishTime = new DateTime(2020, 2, 15, 1, 0, 0),
        }).ExecuteAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    [Fact, Upstream("sqlite/SQLiteTest.java#testQuery")]
    public async Task TestQuery()
    {
        var list = await _db.Query<BlogEntity>().ToListAsync(Ct);
        Assert.All(list, b => Assert.Contains("\"title\":\"title1\"", System.Text.Json.JsonSerializer.Serialize(b, System.Text.Json.JsonSerializerOptions.Web)));
    }

    private static EasyDb UpperCamelCase() => new(o =>
    {
        o.NameConversion = Metadata.NameConversion.UpperCamelCase;
        o.UseSqlite(SqliteFactory.Instance.CreateDataSource("Data Source=eqs-sqlite-test;Mode=Memory;Cache=Shared"));
    });

    // 先删表再同步：表不存在，同步就是建表（Java 的 getSQL() 在语句前加一个换行）
    [Fact, Upstream("sqlite/SQLiteTest.java#testDDL")]
    public async Task TestDDL()
    {
        var db = UpperCamelCase();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(Topic)], Ct);
        var sync = await db.CodeFirst.SyncTablesAsync([typeof(Topic)], Ct);
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"t_topic\" ( \n" +
                     "\"Id\" VARCHAR(255) NOT NULL ,\n" +
                     "\"Stars\" integer NULL ,\n" +
                     "\"Title\" VARCHAR(255) NULL ,\n" +
                     "\"CreateTime\" TEXT NULL , \n" +
                     " PRIMARY KEY (\"Id\")\n" +
                     ");", sync.ToSql());
    }

    [Fact, Upstream("sqlite/SQLiteTest.java#testDDL2")]
    public async Task TestDDL2()
    {
        var db = UpperCamelCase();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(H2.MultiPrimaryEntity)], Ct);
        var sync = await db.CodeFirst.SyncTablesAsync([typeof(H2.MultiPrimaryEntity)], Ct);
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"t_multi_primary\" ( \n" +
                     "\"Id1\" VARCHAR(255) NOT NULL ,\n" +
                     "\"Id2\" VARCHAR(255) NOT NULL , \n" +
                     " PRIMARY KEY (\"Id1\", \"Id2\")\n" +
                     ");", sync.ToSql());
    }

    [Fact, Upstream("sqlite/SQLiteTest.java#testQuery1")]
    public async Task TestQuery1()
    {
        var list = await _db.Query<BlogEntity>().Where(b => b.CreateTime.RangeClosed(new DateTime(2020, 2, 1, 1, 0, 0), new DateTime(2021, 3, 1, 1, 0, 0))).ToListAsync(Ct);
        Assert.Equal("1", Assert.Single(list).Id);
    }
}
