using BenchmarkDotNet.Attributes;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Metadata;
using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Benchmarks;

/// <summary>基准用的订单表。</summary>
[Table("b_order")]
public class Order
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public string Title { get; set; } = "";

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>投影目标。</summary>
public sealed record OrderRow(long Id, string Title, decimal Amount);

/// <summary>
/// 一条查询的各段开销：只翻译渲染（不连库）、整条查询读实体、读投影，以及手写 ADO.NET 作为下限。
/// 条件里的值来自闭包变量，与业务代码里的写法一样。
/// </summary>
[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "连接在 GlobalCleanup 里释放，BenchmarkDotNet 管理生命周期")]
public class QueryBenchmarks
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"eqs_bench_{Environment.ProcessId}.db");
    private EasyDb _db = null!;
    private SqliteConnection _connection = null!;
    private long _customer = 3;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var cs = $"Data Source={_file}";
        _db = new EasyDb(o => o.Use(SqliteDialect.Instance, SqliteFactory.Instance.CreateDataSource(cs)));
        await _db.CodeFirst.DropTablesIfExistsAsync(typeof(Order));
        await _db.CodeFirst.CreateTablesAsync(typeof(Order));
        var start = new DateTime(2026, 1, 1);
        await _db.Insert(Enumerable.Range(1, 1000).Select(i => new Order
        {
            Id = i,
            CustomerId = i % 10,
            Title = "order " + i,
            Amount = i * 1.5m,
            CreatedAt = start.AddMinutes(i),
        })).ExecuteAsync();
        _connection = new SqliteConnection(cs);
        await _connection.OpenAsync();
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _connection.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(_file);
    }

    /// <summary>只翻译、渲染，不执行。</summary>
    [Benchmark]
    public string TranslateOnly() =>
        _db.Query<Order>().Where(o => o.CustomerId == _customer && o.Amount > 10).OrderByDescending(o => o.Id).Take(100).ToSql();

    /// <summary>读 100 个实体。</summary>
    [Benchmark]
    public Task<List<Order>> ReadEntities() =>
        _db.Query<Order>().Where(o => o.CustomerId == _customer && o.Amount > 10).OrderByDescending(o => o.Id).Take(100).ToListAsync();

    /// <summary>读 100 行投影。</summary>
    [Benchmark]
    public Task<List<OrderRow>> ReadProjection() =>
        _db.Query<Order>().Where(o => o.CustomerId == _customer && o.Amount > 10).OrderByDescending(o => o.Id).Take(100)
            .Select(o => new OrderRow(o.Id, o.Title, o.Amount)).ToListAsync();

    /// <summary>手写 ADO.NET：同样的 SQL，按列序号读，作为下限。</summary>
    [Benchmark(Baseline = true)]
    public async Task<List<OrderRow>> HandWrittenAdoNet()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT \"id\",\"title\",\"amount\" FROM \"b_order\" WHERE \"customer_id\" = @p0 AND \"amount\" > @p1 ORDER BY \"id\" DESC LIMIT 100";
        command.Parameters.AddWithValue("@p0", _customer);
        command.Parameters.AddWithValue("@p1", 10m);
        var rows = new List<OrderRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new OrderRow(reader.GetInt64(0), reader.GetString(1), reader.GetDecimal(2)));
        return rows;
    }
}
