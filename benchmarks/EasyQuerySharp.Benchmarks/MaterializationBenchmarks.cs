using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace EasyQuerySharp.Benchmarks;

/// <summary>
/// 物化：整条查询（翻译、执行、读取）读 <see cref="Rows"/> 行订单，分别读成实体（7 列）和 DTO（4 列）。
/// SQL 都是 <c>WHERE id &gt; @p ORDER BY id LIMIT n</c>；手写 ADO.NET（按列序号读）作为每组的基线。
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MaterializationBenchmarks : OrmBenchmark
{
    private const string EntitySql = "SELECT id, customer_id, title, amount, status, created_at, remark FROM bench_order WHERE id > @minId ORDER BY id LIMIT @rows";
    private const string DtoSql = "SELECT id, title, amount, created_at FROM bench_order WHERE id > @minId ORDER BY id LIMIT @rows";

    private static readonly Func<BenchContext, long, int, IAsyncEnumerable<Order>> EfCompiledEntities =
        EF.CompileAsyncQuery((BenchContext ctx, long minId, int rows) => ctx.Orders.AsNoTracking().Where(o => o.Id > minId).OrderBy(o => o.Id).Take(rows));

    private static readonly Func<BenchContext, long, int, IAsyncEnumerable<OrderDto>> EfCompiledDtos =
        EF.CompileAsyncQuery((BenchContext ctx, long minId, int rows) => ctx.Orders.Where(o => o.Id > minId).OrderBy(o => o.Id).Take(rows)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt }));

    // 条件里的值来自字段（闭包），与业务代码的写法一样
    private long _minId = 100;

    [Params(10, 1000)]
    public int Rows { get; set; }

    // ---------- 实体 ----------

    [Benchmark(Baseline = true, Description = "ADO.NET")]
    [BenchmarkCategory("Entity")]
    public async Task<List<Order>> Entities_AdoNet()
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = EntitySql;
        command.Parameters.AddWithValue("@minId", _minId);
        command.Parameters.AddWithValue("@rows", Rows);
        var list = new List<Order>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Order
            {
                Id = reader.GetInt64(0),
                CustomerId = reader.GetInt64(1),
                Title = reader.GetString(2),
                Amount = reader.GetDouble(3),
                Status = reader.GetInt32(4),
                CreatedAt = reader.GetDateTime(5),
                Remark = reader.IsDBNull(6) ? null : reader.GetString(6),
            });
        }
        return list;
    }

    [Benchmark(Description = "Dapper")]
    [BenchmarkCategory("Entity")]
    public async Task<List<Order>> Entities_Dapper()
    {
        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<Order>(EntitySql, new { minId = _minId, rows = Rows })).AsList();
    }

    [Benchmark(Description = "EasyQuerySharp")]
    [BenchmarkCategory("Entity")]
    public Task<List<Order>> Entities_EasyQuerySharp() =>
        Easy.Query<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(Rows).ToListAsync();

    [Benchmark(Description = "EF Core")]
    [BenchmarkCategory("Entity")]
    public Task<List<Order>> Entities_EfCore() =>
        Ef.Orders.AsNoTracking().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(Rows).ToListAsync();

    [Benchmark(Description = "EF Core (compiled)")]
    [BenchmarkCategory("Entity")]
    public Task<List<Order>> Entities_EfCoreCompiled() => ToListAsync(EfCompiledEntities(Ef, _minId, Rows));

    [Benchmark(Description = "FreeSql")]
    [BenchmarkCategory("Entity")]
    public Task<List<Order>> Entities_FreeSql() =>
        Free.Select<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(Rows).ToListAsync();

    [Benchmark(Description = "SqlSugar")]
    [BenchmarkCategory("Entity")]
    public Task<List<Order>> Entities_SqlSugar() =>
        Sugar.Queryable<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(Rows).ToListAsync();

    // ---------- DTO ----------

    [Benchmark(Baseline = true, Description = "ADO.NET")]
    [BenchmarkCategory("DTO")]
    public async Task<List<OrderDto>> Dtos_AdoNet()
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = DtoSql;
        command.Parameters.AddWithValue("@minId", _minId);
        command.Parameters.AddWithValue("@rows", Rows);
        var list = new List<OrderDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new OrderDto { Id = reader.GetInt64(0), Title = reader.GetString(1), Amount = reader.GetDouble(2), CreatedAt = reader.GetDateTime(3) });
        return list;
    }

    [Benchmark(Description = "Dapper")]
    [BenchmarkCategory("DTO")]
    public async Task<List<OrderDto>> Dtos_Dapper()
    {
        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<OrderDto>(DtoSql, new { minId = _minId, rows = Rows })).AsList();
    }

    [Benchmark(Description = "EasyQuerySharp")]
    [BenchmarkCategory("DTO")]
    public Task<List<OrderDto>> Dtos_EasyQuerySharp() =>
        Easy.Query<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(Rows)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt }).ToListAsync();

    [Benchmark(Description = "EF Core")]
    [BenchmarkCategory("DTO")]
    public Task<List<OrderDto>> Dtos_EfCore() =>
        Ef.Orders.Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(Rows)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt }).ToListAsync();

    [Benchmark(Description = "EF Core (compiled)")]
    [BenchmarkCategory("DTO")]
    public Task<List<OrderDto>> Dtos_EfCoreCompiled() => ToListAsync(EfCompiledDtos(Ef, _minId, Rows));

    [Benchmark(Description = "FreeSql")]
    [BenchmarkCategory("DTO")]
    public Task<List<OrderDto>> Dtos_FreeSql() =>
        Free.Select<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(Rows)
            .ToListAsync(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt });

    [Benchmark(Description = "SqlSugar")]
    [BenchmarkCategory("DTO")]
    public Task<List<OrderDto>> Dtos_SqlSugar() =>
        Sugar.Queryable<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(Rows)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt }).ToListAsync();

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source) list.Add(item);
        return list;
    }

    protected override async Task VerifyAsync()
    {
        var expected = await Entities_AdoNet();
        Check("ADO.NET 行数", expected.Count, Rows);
        var entities = new (string, Func<Task<List<Order>>>)[]
        {
            ("Dapper", Entities_Dapper), ("EasyQuerySharp", Entities_EasyQuerySharp), ("EF Core", Entities_EfCore),
            ("EF Core (compiled)", Entities_EfCoreCompiled), ("FreeSql", Entities_FreeSql), ("SqlSugar", Entities_SqlSugar),
        };
        foreach (var (name, read) in entities)
        {
            SqlLog?.Invoke($"-- {name}");
            var rows = await read();
            Check($"{name} 实体行数", rows.Count, expected.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i] is not { } r || r.Id != expected[i].Id || r.CustomerId != expected[i].CustomerId || r.Title != expected[i].Title
                    || r.Amount != expected[i].Amount || r.Status != expected[i].Status || r.CreatedAt != expected[i].CreatedAt || r.Remark != expected[i].Remark)
                    throw new InvalidOperationException($"{name} 第 {i} 行实体与 ADO.NET 不一致");
            }
        }

        var dtos = new (string, Func<Task<List<OrderDto>>>)[]
        {
            ("ADO.NET", Dtos_AdoNet), ("Dapper", Dtos_Dapper), ("EasyQuerySharp", Dtos_EasyQuerySharp), ("EF Core", Dtos_EfCore),
            ("EF Core (compiled)", Dtos_EfCoreCompiled), ("FreeSql", Dtos_FreeSql), ("SqlSugar", Dtos_SqlSugar),
        };
        foreach (var (name, read) in dtos)
        {
            SqlLog?.Invoke($"-- {name}");
            var rows = await read();
            Check($"{name} DTO 行数", rows.Count, expected.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i] is not { } r || r.Id != expected[i].Id || r.Title != expected[i].Title || r.Amount != expected[i].Amount || r.CreatedAt != expected[i].CreatedAt)
                    throw new InvalidOperationException($"{name} 第 {i} 行 DTO 与 ADO.NET 不一致");
            }
        }
    }
}
