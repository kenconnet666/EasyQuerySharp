using System.Data.Common;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.TestKit.Databases;

namespace EasyQuerySharp.Tests.Live;

/// <summary>
/// TDengine 实库冒烟（对应 TS 版 tdengine 的 live.test）：时序库没有自增、UPDATE、事务，表的第一列必须是时间戳，共用套件跑不了；
/// 这里用时间戳为主键的实体测建表、写入、查询、聚合、覆盖写与删除。库 <c>eqs</c> 由用例自己建（毫秒精度）；
/// 连接串追加 <c>timezone=UTC</c>，读回的时间戳与按 UTC 钟点写入的一致。
/// </summary>
public sealed class TDengineLiveSuite
{
    private static readonly Lazy<Task<DbDataSource>> DataSource = new(CreateDataSourceAsync);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateTime At(int minute) => new(2026, 1, 2, 3, minute, 5, 6);

    private static async Task<DbDataSource> CreateDataSourceAsync()
    {
        await using (var connection = await TestDatabases.TDengine.OpenAsync(CancellationToken.None))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE DATABASE IF NOT EXISTS eqs PRECISION 'ms'";
            await command.ExecuteNonQueryAsync();
        }
        return TestDatabases.TDengine.Factory.CreateDataSource(TestDatabases.TDengine.ConnectionString() + ";db=eqs;timezone=UTC");
    }

    private static async Task<EasyDb> SeededAsync()
    {
        _ = TestDatabases.TDengine.ConnectionString();   // 没有配置时在这里跳过
        var dataSource = await DataSource.Value;
        var db = new EasyDb(o => o.UseTDengine(dataSource).AddListener(SqlCapture.Instance));
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(TdReading)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(TdReading)], Ct);
        await db.Insert(
            new TdReading { Ts = At(0), Device = "a", Value = 1.5, Ok = true, Count = 10 },
            new TdReading { Ts = At(1), Device = "b'x", Value = 2.5, Ok = false, Count = 20 },
            new TdReading { Ts = At(2), Device = "a", Value = 4, Ok = true, Count = null }).ExecuteAsync(Ct);
        return db;
    }

    [Fact]
    public async Task CreatesTablesWithTheTimestampFirst()
    {
        var db = await SeededAsync();
        Assert.Equal("CREATE TABLE IF NOT EXISTS `t_td_reading` (`ts` TIMESTAMP, `device` NCHAR(20), `value` DOUBLE, `ok` BOOL, `count` BIGINT)",
            db.CodeFirst.CreateTableSql(typeof(TdReading)));
    }

    [Fact]
    public async Task FiltersOrdersPagesCountsAndGroups()
    {
        var db = await SeededAsync();
        var rows = await db.Query<TdReading>().Where(r => r.Ts >= At(1)).OrderByDescending(r => r.Ts).ToListAsync(Ct);
        Assert.Equivalent(new[]
        {
            new TdReading { Ts = At(2), Device = "a", Value = 4, Ok = true, Count = null },
            new TdReading { Ts = At(1), Device = "b'x", Value = 2.5, Ok = false, Count = 20 },
        }, rows, strict: true);
        Assert.Equal(3, await db.Query<TdReading>().CountAsync(Ct));
        Assert.Equal(["b'x"], await db.Query<TdReading>().OrderBy(r => r.Ts).Skip(1).Take(1).Select(r => r.Device).ToListAsync(Ct));
        Assert.Equal(["b'x"], await db.Query<TdReading>().Where(r => r.Device == "b'x").Select(r => r.Device).ToListAsync(Ct));

        var groups = await db.Query<TdReading>().GroupBy(r => r.Device).OrderBy(g => g.Key)
            .Select(g => new Draft<string?, long, double?>(g.Key, g.Count(), g.Sum(r => r.Value)))
            .ToListAsync(Ct);
        Assert.Equal([("a", 2L, (double?)5.5), ("b'x", 1L, 2.5)], groups.Select(g => (g.Value1, g.Value2, g.Value3)));
    }

    [Fact]
    public async Task RunsDateAndStringFunctions()
    {
        var db = await SeededAsync();
        var row = await db.Query<TdReading>().Where(r => r.Ts == At(1))
            .Select(r => new Draft<int, int, string, DateTime, string?>(r.Ts.Year, r.Ts.Minute, r.Ts.ToString("yyyy-MM-dd HH:mm"), r.Ts.DateTrunc(DateTruncUnit.Hour), r.Device!.ToUpper()))
            .SingleAsync(Ct);
        Assert.Equal((2026, 1, "2026-01-02 03:01", new DateTime(2026, 1, 2, 3, 0, 0), "B'X"), (row.Value1, row.Value2, row.Value3, row.Value4, row.Value5));
    }

    // 同一时间戳再插入就是覆盖；删除只按本表的列过滤；没有 UPDATE、事务
    [Fact]
    public async Task OverwritesBySameTimestampAndDeletesByCondition()
    {
        var db = await SeededAsync();
        await db.Insert(new TdReading { Ts = At(0), Device = "z" }).ExecuteAsync(Ct);
        Assert.Equal("z", await db.Query<TdReading>().Where(r => r.Ts == At(0)).Select(r => r.Device).SingleAsync(Ct));

        Assert.Equal(2, await db.Delete<TdReading>().Where(r => r.Ts < At(2)).ExecuteAsync(Ct));
        Assert.Equal([At(2)], await db.Query<TdReading>().Select(r => r.Ts).ToListAsync(Ct));

        Assert.Throws<NotSupportedException>(() => db.Update<TdReading>().Set(r => new TdReading { Device = "x" }).Where(r => r.Ts == At(2)).ToSql());
        await using (db.BeginTransaction())
            await Assert.ThrowsAsync<NotSupportedException>(() => db.Query<TdReading>().CountAsync(Ct));
    }
}

/// <summary>TDengine 冒烟的读数：时间戳是主键。</summary>
[Table("t_td_reading")]
public class TdReading
{
    [Column(PrimaryKey = true)]
    public DateTime Ts { get; set; }

    [Column(DbType = "NCHAR(20)")]
    public string? Device { get; set; }

    public double? Value { get; set; }

    public bool? Ok { get; set; }

    public long? Count { get; set; }
}
