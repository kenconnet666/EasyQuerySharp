using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：sqlite/SQLiteTest.java 的查询用例（SQLite 上整表查询、日期区间条件，读出后序列化成 JSON）。
/// Java 连一个现成的 SQLite 文件库；这里用共享的内存库，code-first 建表并写入一行。建表的 DDL 用例随 code-first（7.1）移植。
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

    [Fact, Upstream("sqlite/SQLiteTest.java#testQuery1")]
    public async Task TestQuery1()
    {
        var list = await _db.Query<BlogEntity>().Where(b => b.CreateTime.RangeClosed(new DateTime(2020, 2, 1, 1, 0, 0), new DateTime(2021, 3, 1, 1, 0, 0))).ToListAsync(Ct);
        Assert.Equal("1", Assert.Single(list).Id);
    }
}
