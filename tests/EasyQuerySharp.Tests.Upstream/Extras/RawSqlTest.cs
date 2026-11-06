using System.Reflection;
using EasyQuerySharp.Metadata;
using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 原生 SQL 的两组方法：不带 Raw 的只收插值字符串、插值处一定参数化（不带 CancellationToken 时也一样，原来会落到拼文本的
/// string 重载），带 Raw 的收普通字符串与 ? 位置参数。C# 独有，在进程内 SQLite 上跑。
/// </summary>
public sealed class RawSqlTest : IAsyncLifetime
{
    private const string Evil = "x'; DROP TABLE raw_item; --";

    private readonly string _cs = $"Data Source=eqs-raw-sql-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;
    private readonly EasyDb _db;

    public RawSqlTest()
    {
        _keepAlive = new SqliteConnection(_cs);
        _db = new EasyDb(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(_cs)).AddListener(SqlCapture.Instance));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _keepAlive.OpenAsync(Ct);
        await _db.CodeFirst.CreateTablesAsync([typeof(RawItem)], Ct);
        await _db.ExecuteSqlRawAsync("INSERT INTO raw_item (id, name) VALUES (1, 'a?'), (2, 'b'), (3, 'c')", cancellationToken: Ct);
    }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    // 这两个用例故意不传 CancellationToken：原来只有不带它的调用会落到拼文本的重载
#pragma warning disable xUnit1051
    [Fact]
    public async Task InterpolatedQueryWithoutTokenBindsParameters()
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal([0L], await _db.SqlQueryAsync<long>($"SELECT COUNT(*) FROM raw_item WHERE name = {Evil}"));
        listen.AssertSql("SELECT COUNT(*) FROM raw_item WHERE name = ?");
        listen.AssertParams(Evil + "(String)");
        Assert.Equal(3, await _db.Query<RawItem>().CountAsync(Ct));
    }

    [Fact]
    public async Task InterpolatedExecuteWithoutTokenBindsParameters()
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal(1, await _db.ExecuteSqlAsync($"UPDATE raw_item SET name = {Evil} WHERE id = {2}"));
        listen.AssertSql("UPDATE raw_item SET name = ? WHERE id = ?");
        listen.AssertParams(Evil + "(String),2(Integer)");
        Assert.Equal(Evil, (await _db.Query<RawItem>().FindAsync(2L, Ct))!.Name);
    }
#pragma warning restore xUnit1051

    [Fact]
    public async Task InterpolatedSourceNumbersItsParametersWithTheOuterQuery()
    {
        long[] ids = [1, 2, 3];
        using var listen = SqlCapture.Listen();
        var names = await _db.FromSql<RawItem>($"SELECT * FROM {Sql.Identifier("raw_item")} WHERE id IN ({ids}) AND name <> {Evil}")
            .Where(i => i.Id > 1).OrderBy(i => i.Id).Select(i => i.Name).ToListAsync(Ct);
        Assert.Equal(["b", "c"], names);
        listen.AssertSql("SELECT t.\"name\" FROM (SELECT * FROM \"raw_item\" WHERE id IN (?,?,?) AND name <> ?) t WHERE t.\"id\" > ? ORDER BY t.\"id\" ASC");
        listen.AssertParams($"1(Long),2(Long),3(Long),{Evil}(String),1(Long)");
    }

    // 位置参数：引号里的 ? 不算占位符，整条执行与作为来源时一致
    [Fact]
    public async Task RawMethodsSkipQuestionMarksInsideQuotes()
    {
        Assert.Equal(["a?"], await _db.SqlQueryRawAsync<string>("SELECT name FROM raw_item WHERE name = 'a?' AND id = ?", [1L], Ct));
        Assert.Equal(["a?"], await _db.FromSqlRaw<RawItem>("SELECT * FROM raw_item WHERE name = 'a?' AND id = ?", 1L).Select(i => i.Name).ToListAsync(Ct));
        Assert.Throws<ArgumentException>(() => _db.FromSqlRaw<RawItem>("SELECT * FROM raw_item WHERE id = ?"));
    }

    // 插值字符串能传给的方法都只收 FormattableString：收 string 的方法名字都带 Raw，重载决议不会把 $"…" 交给拼文本的一组
    [Fact]
    public void OnlyRawMethodsAcceptPlainStrings()
    {
        string[] family = ["FromSql", "SqlQueryAsync", "ExecuteSqlAsync"];
        var methods = typeof(EasyDb).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => family.Contains(m.Name) || family.Contains(m.Name.Replace("Raw", "", StringComparison.Ordinal))).ToList();
        Assert.Equal(6, methods.Count);
        Assert.All(methods, m => Assert.Equal(m.Name.Contains("Raw", StringComparison.Ordinal) ? typeof(string) : typeof(FormattableString), m.GetParameters()[0].ParameterType));
    }
}

[Table("raw_item")]
public class RawItem
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public string? Name { get; set; }
}
