using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace EasyQuerySharp.Benchmarks;

/// <summary>
/// 对象树：带出对多集合导航。
/// 一层：50 个订单，各带 5 条明细（共 250 条）；两层：10 个客户，各带 20 个订单、每个订单 5 条明细（共 200 个订单、1000 条明细）。
/// EasyQuerySharp、FreeSql、SqlSugar 都是每层一条 <c>WHERE 外键 IN (父键)</c>；EF Core 默认一条 LEFT JOIN 查询，
/// <c>AsSplitQuery()</c> 是每层一条 JOIN 父查询的语句，两种都列出。手写 Dapper（每层一条 IN 查询，按字典挂到父对象上）作为基线。
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ObjectTreeBenchmarks : OrmBenchmark
{
    private const int OrderTake = 50;
    private const int CustomerTake = 10;

    private const string OrderSql = "SELECT id, customer_id, title, amount, status, created_at, remark FROM bench_order WHERE id > @minId ORDER BY id LIMIT @take";
    private const string OrdersOfCustomersSql = "SELECT id, customer_id, title, amount, status, created_at, remark FROM bench_order WHERE customer_id IN @ids";
    private const string ItemsOfOrdersSql = "SELECT id, order_id, product, quantity, price FROM bench_order_item WHERE order_id IN @ids";
    private const string CustomerSql = "SELECT id, name, level, created_at FROM bench_customer WHERE id > @minId ORDER BY id LIMIT @take";

    // 条件里的值来自字段（闭包），与业务代码的写法一样
    private long _minId = 2;

    // ---------- 一层：订单 + 明细 ----------

    [Benchmark(Baseline = true, Description = "Dapper")]
    [BenchmarkCategory("OneLevel")]
    public async Task<List<Order>> OneLevel_Dapper()
    {
        await using var connection = await OpenAsync();
        var orders = (await connection.QueryAsync<Order>(OrderSql, new { minId = _minId, take = OrderTake })).AsList();
        await AttachItemsAsync(connection, orders);
        return orders;
    }

    [Benchmark(Description = "EasyQuerySharp")]
    [BenchmarkCategory("OneLevel")]
    public Task<List<Order>> OneLevel_EasyQuerySharp() =>
        Easy.Query<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(OrderTake).Include(o => o.Items).ToListAsync();

    [Benchmark(Description = "EF Core")]
    [BenchmarkCategory("OneLevel")]
    public Task<List<Order>> OneLevel_EfCore() =>
        Ef.Orders.AsNoTracking().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(OrderTake).Include(o => o.Items).ToListAsync();

    [Benchmark(Description = "EF Core (split)")]
    [BenchmarkCategory("OneLevel")]
    public Task<List<Order>> OneLevel_EfCoreSplit() =>
        Ef.Orders.AsNoTracking().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(OrderTake).Include(o => o.Items).AsSplitQuery().ToListAsync();

    [Benchmark(Description = "FreeSql")]
    [BenchmarkCategory("OneLevel")]
    public Task<List<Order>> OneLevel_FreeSql() =>
        Free.Select<Order>().Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(OrderTake).IncludeMany(o => o.Items!).ToListAsync();

    [Benchmark(Description = "SqlSugar")]
    [BenchmarkCategory("OneLevel")]
    public Task<List<Order>> OneLevel_SqlSugar() =>
        Sugar.Queryable<Order>().Includes(o => o.Items!).Where(o => o.Id > _minId).OrderBy(o => o.Id).Take(OrderTake).ToListAsync();

    // ---------- 两层：客户 + 订单 + 明细 ----------

    [Benchmark(Baseline = true, Description = "Dapper")]
    [BenchmarkCategory("TwoLevel")]
    public async Task<List<Customer>> TwoLevel_Dapper()
    {
        await using var connection = await OpenAsync();
        var customers = (await connection.QueryAsync<Customer>(CustomerSql, new { minId = _minId, take = CustomerTake })).AsList();
        var byId = customers.ToDictionary(c => c.Id);
        var orders = (await connection.QueryAsync<Order>(OrdersOfCustomersSql, new { ids = byId.Keys })).AsList();
        foreach (var order in orders) (byId[order.CustomerId].Orders ??= []).Add(order);
        await AttachItemsAsync(connection, orders);
        return customers;
    }

    [Benchmark(Description = "EasyQuerySharp")]
    [BenchmarkCategory("TwoLevel")]
    public Task<List<Customer>> TwoLevel_EasyQuerySharp() =>
        Easy.Query<Customer>().Where(c => c.Id > _minId).OrderBy(c => c.Id).Take(CustomerTake)
            .Include(c => c.Orders, q => q.Include(o => o.Items)).ToListAsync();

    [Benchmark(Description = "EF Core")]
    [BenchmarkCategory("TwoLevel")]
    public Task<List<Customer>> TwoLevel_EfCore() =>
        Ef.Customers.AsNoTracking().Where(c => c.Id > _minId).OrderBy(c => c.Id).Take(CustomerTake)
            .Include(c => c.Orders!).ThenInclude(o => o.Items).ToListAsync();

    [Benchmark(Description = "EF Core (split)")]
    [BenchmarkCategory("TwoLevel")]
    public Task<List<Customer>> TwoLevel_EfCoreSplit() =>
        Ef.Customers.AsNoTracking().Where(c => c.Id > _minId).OrderBy(c => c.Id).Take(CustomerTake)
            .Include(c => c.Orders!).ThenInclude(o => o.Items).AsSplitQuery().ToListAsync();

    [Benchmark(Description = "FreeSql")]
    [BenchmarkCategory("TwoLevel")]
    public Task<List<Customer>> TwoLevel_FreeSql() =>
        Free.Select<Customer>().Where(c => c.Id > _minId).OrderBy(c => c.Id).Take(CustomerTake)
            .IncludeMany(c => c.Orders!, then => then.IncludeMany(o => o.Items!)).ToListAsync();

    [Benchmark(Description = "SqlSugar")]
    [BenchmarkCategory("TwoLevel")]
    public Task<List<Customer>> TwoLevel_SqlSugar() =>
        Sugar.Queryable<Customer>().Includes(c => c.Orders!, o => o.Items!).Where(c => c.Id > _minId).OrderBy(c => c.Id).Take(CustomerTake).ToListAsync();

    private static async Task AttachItemsAsync(System.Data.Common.DbConnection connection, List<Order> orders)
    {
        var byId = orders.ToDictionary(o => o.Id);
        foreach (var item in await connection.QueryAsync<OrderItem>(ItemsOfOrdersSql, new { ids = byId.Keys }))
            (byId[item.OrderId].Items ??= []).Add(item);
    }

    protected override async Task VerifyAsync()
    {
        var oneLevel = new (string, Func<Task<List<Order>>>)[]
        {
            ("Dapper", OneLevel_Dapper), ("EasyQuerySharp", OneLevel_EasyQuerySharp), ("EF Core", OneLevel_EfCore),
            ("EF Core (split)", OneLevel_EfCoreSplit), ("FreeSql", OneLevel_FreeSql), ("SqlSugar", OneLevel_SqlSugar),
        };
        foreach (var (name, read) in oneLevel)
        {
            SqlLog?.Invoke($"-- {name}");
            var orders = await read();
            Check($"{name} 一层的订单数", orders.Count, OrderTake);
            CheckItems(name, orders);
        }

        var twoLevel = new (string, Func<Task<List<Customer>>>)[]
        {
            ("Dapper", TwoLevel_Dapper), ("EasyQuerySharp", TwoLevel_EasyQuerySharp), ("EF Core", TwoLevel_EfCore),
            ("EF Core (split)", TwoLevel_EfCoreSplit), ("FreeSql", TwoLevel_FreeSql), ("SqlSugar", TwoLevel_SqlSugar),
        };
        foreach (var (name, read) in twoLevel)
        {
            SqlLog?.Invoke($"-- {name}");
            var customers = await read();
            Check($"{name} 两层的客户数", customers.Count, CustomerTake);
            var orders = customers.SelectMany(c => (c.Orders ?? []).Where(o => o.CustomerId == c.Id)).ToList();
            Check($"{name} 两层的订单数", orders.Count, CustomerTake * OrdersPerCustomer);
            CheckItems(name, orders);
        }
    }

    private static void CheckItems(string name, List<Order> orders)
    {
        var items = orders.Sum(o => (o.Items ?? []).Count(i => i.OrderId == o.Id));
        Check($"{name} 的明细数", items, orders.Count * ItemsPerOrder);
    }
}
