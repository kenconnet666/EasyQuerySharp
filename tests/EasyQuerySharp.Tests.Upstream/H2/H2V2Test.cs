using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/v2/H2V2Test.java（各类型写入后读回、冲突时更新全部列）。Java 的 H2V2BaseTest 另开一个库、删表重建，
/// 这里同样用单独的 SQLite 共享内存库（标识符照常加引号），每个用例前删表重建。
/// </summary>
public sealed class H2V2Test : IAsyncLifetime
{
    private const string ConnectionString = "Data Source=eqs-upstream-h2v2;Mode=Memory;Cache=Shared";
    private static readonly Type[] Tables = [typeof(ALLTYPE), typeof(Topic), typeof(BlogEntity)];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 共享内存库只要还有一个连接开着就一直在
    private readonly SqliteConnection _keepAlive = new(ConnectionString);
    private readonly EasyDb _db = new(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(ConnectionString)));

    public async ValueTask InitializeAsync()
    {
        await _keepAlive.OpenAsync(Ct);
        await _db.CodeFirst.DropTablesIfExistsAsync(Tables, Ct);
        await _db.CodeFirst.CreateTablesAsync(Tables, Ct);
    }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    [Fact, Upstream("h2/v2/H2V2Test.java#testAllType")]
    public async Task TestAllType()
    {
        var inserted = ALLTYPE.Sample("123456");
        Assert.Equal(1, await _db.Insert(inserted).ExecuteAsync(Ct));
        var read = await _db.Query<ALLTYPE>().WhereById("123456").FirstOrDefaultAsync(Ct);
        Assert.NotNull(read);
        Assert.Equivalent(inserted, read, strict: true);
    }

    // Java 的 onConflictThen(s -> s.FETCHER.allFields())：主键冲突时更新全部列；Java 只执行不断言，这里再写一次核对更新生效
    [Fact, Upstream("h2/v2/H2V2Test.java#testAllType1")]
    public async Task TestAllType1()
    {
        var row = ALLTYPE.Sample("123456123123");
        Assert.Equal(1, await _db.Insert(row).OnConflictUpdate().ExecuteAsync(Ct));
        row.Value = "4433";
        row.NumberIntegerBasic = 44;
        Assert.Equal(1, await _db.Insert(row).OnConflictUpdate().ExecuteAsync(Ct));
        var read = await _db.Query<ALLTYPE>().WhereById("123456123123").SingleAsync(Ct);
        Assert.Equivalent(row, read, strict: true);
    }
}
