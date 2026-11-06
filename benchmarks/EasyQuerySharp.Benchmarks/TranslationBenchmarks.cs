using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.EntityFrameworkCore;
using SqlSugar;

namespace EasyQuerySharp.Benchmarks;

/// <summary>
/// 查询翻译：从写出查询到得到 SQL 文本，不连库。条件里的值来自字段（闭包），各 ORM 都生成命令参数。
/// <list type="bullet">
/// <item>Simple：与物化基准相同的查询（一个条件、排序、取前 10 行，读实体），用来从整条查询的耗时里拆出翻译占多少；</item>
/// <item>Complex：三个条件、两级排序、分页、投影到 DTO。</item>
/// </list>
/// EF Core 有查询缓存，这里测的是命中缓存后的开销（建表达式树、取参数、查缓存、生成命令文本）；其余三个每次都完整翻译。
/// EasyQuerySharp 的 ToSql 不编译读取委托（执行查询时才编译，按形状缓存）。Dapper 手写 SQL，没有翻译这一步。
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TranslationBenchmarks : OrmBenchmark
{
    private long _minId = 100;
    private long _customerId = 3;
    private double _minAmount = 10;
    private int _status = 2;

    // ---------- Simple ----------

    [Benchmark(Baseline = true, Description = "EasyQuerySharp")]
    [BenchmarkCategory("Simple")]
    public string Simple_EasyQuerySharp() =>
        Easy.Query<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(10).ToSql();

    [Benchmark(Description = "EF Core")]
    [BenchmarkCategory("Simple")]
    public string Simple_EfCore() =>
        Ef.Orders.AsNoTracking().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(10).ToQueryString();

    [Benchmark(Description = "FreeSql")]
    [BenchmarkCategory("Simple")]
    public string Simple_FreeSql() =>
        Free.Select<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(10).ToSql();

    [Benchmark(Description = "SqlSugar")]
    [BenchmarkCategory("Simple")]
    public string Simple_SqlSugar() =>
        Sugar.Queryable<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(10).ToSql().Key;

    // ---------- Complex ----------

    [Benchmark(Baseline = true, Description = "EasyQuerySharp")]
    [BenchmarkCategory("Complex")]
    public string Complex_EasyQuerySharp() =>
        Easy.Query<Order>()
            .Where(o => o.CustomerId == _customerId && o.Amount > _minAmount && o.Status != _status)
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip(20).Take(10)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt })
            .ToSql();

    [Benchmark(Description = "EF Core")]
    [BenchmarkCategory("Complex")]
    public string Complex_EfCore() =>
        Ef.Orders.AsNoTracking()
            .Where(o => o.CustomerId == _customerId && o.Amount > _minAmount && o.Status != _status)
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip(20).Take(10)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt })
            .ToQueryString();

    [Benchmark(Description = "FreeSql")]
    [BenchmarkCategory("Complex")]
    public string Complex_FreeSql() =>
        Free.Select<Order>()
            .Where(o => o.CustomerId == _customerId && o.Amount > _minAmount && o.Status != _status)
            .OrderByDescending(o => o.CreatedAt).OrderBy(o => o.Id)
            .Skip(20).Take(10)
            .ToSql(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt });

    [Benchmark(Description = "SqlSugar")]
    [BenchmarkCategory("Complex")]
    public string Complex_SqlSugar() =>
        Sugar.Queryable<Order>()
            .Where(o => o.CustomerId == _customerId && o.Amount > _minAmount && o.Status != _status)
            .OrderBy(o => o.CreatedAt, OrderByType.Desc).OrderBy(o => o.Id)
            .Skip(20).Take(10)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt })
            .ToSql().Key;

    protected override Task VerifyAsync()
    {
        var all = new (string, Func<string>, string)[]
        {
            ("EasyQuerySharp", Simple_EasyQuerySharp, "remark"), ("EF Core", Simple_EfCore, "remark"),
            ("FreeSql", Simple_FreeSql, "remark"), ("SqlSugar", Simple_SqlSugar, "remark"),
            ("EasyQuerySharp", Complex_EasyQuerySharp, "created_at"), ("EF Core", Complex_EfCore, "created_at"),
            ("FreeSql", Complex_FreeSql, "created_at"), ("SqlSugar", Complex_SqlSugar, "created_at"),
        };
        foreach (var (name, translate, column) in all)
        {
            var sql = translate();
            if (!sql.Contains("bench_order", StringComparison.OrdinalIgnoreCase) || !sql.Contains(column, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{name} 的 SQL 不对：{sql}");
            SqlLog?.Invoke($"-- {name}\n{sql}");
        }
        return Task.CompletedTask;
    }
}
