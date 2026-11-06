using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using SqlSugar;

namespace EasyQuerySharp.Benchmarks;

/// <summary>
/// 查询翻译：从写出查询到得到 SQL 文本，不连库。三个条件、两级排序、分页、投影到 DTO；条件里的值来自字段（闭包），各 ORM 都生成命令参数。
/// EF Core 有查询缓存，这里测的是命中缓存后的开销（建表达式树、取参数、查缓存、生成命令文本）；其余三个每次都完整翻译。
/// Dapper 手写 SQL，没有翻译这一步。
/// </summary>
[MemoryDiagnoser]
public class TranslationBenchmarks : OrmBenchmark
{
    private long _customerId = 3;
    private double _minAmount = 10;
    private int _status = 2;

    [Benchmark(Baseline = true, Description = "EasyQuerySharp")]
    public string Translate_EasyQuerySharp() =>
        Easy.Query<Order>()
            .Where(o => o.CustomerId == _customerId && o.Amount > _minAmount && o.Status != _status)
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip(20).Take(10)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt })
            .ToSql();

    [Benchmark(Description = "EF Core")]
    public string Translate_EfCore() =>
        Ef.Orders.AsNoTracking()
            .Where(o => o.CustomerId == _customerId && o.Amount > _minAmount && o.Status != _status)
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip(20).Take(10)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt })
            .ToQueryString();

    [Benchmark(Description = "FreeSql")]
    public string Translate_FreeSql() =>
        Free.Select<Order>()
            .Where(o => o.CustomerId == _customerId && o.Amount > _minAmount && o.Status != _status)
            .OrderByDescending(o => o.CreatedAt).OrderBy(o => o.Id)
            .Skip(20).Take(10)
            .ToSql(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt });

    [Benchmark(Description = "SqlSugar")]
    public string Translate_SqlSugar() =>
        Sugar.Queryable<Order>()
            .Where(o => o.CustomerId == _customerId && o.Amount > _minAmount && o.Status != _status)
            .OrderBy(o => o.CreatedAt, OrderByType.Desc).OrderBy(o => o.Id)
            .Skip(20).Take(10)
            .Select(o => new OrderDto { Id = o.Id, Title = o.Title, Amount = o.Amount, CreatedAt = o.CreatedAt })
            .ToSql().Key;

    protected override Task VerifyAsync()
    {
        foreach (var (name, sql) in new[] { ("EasyQuerySharp", Translate_EasyQuerySharp()), ("EF Core", Translate_EfCore()), ("FreeSql", Translate_FreeSql()), ("SqlSugar", Translate_SqlSugar()) })
        {
            if (!sql.Contains("bench_order", StringComparison.OrdinalIgnoreCase) || !sql.Contains("created_at", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{name} 的 SQL 不对：{sql}");
            SqlLog?.Invoke($"-- {name}\n{sql}");
        }
        return Task.CompletedTask;
    }
}
