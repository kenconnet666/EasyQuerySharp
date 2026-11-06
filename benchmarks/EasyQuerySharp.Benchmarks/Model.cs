using System.Reflection;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Benchmarks;

// 各 ORM 共用同一组 POCO 和同一个库。表名、列名以这里的 EasyQuerySharp 特性为准，
// 其它 ORM 用各自的代码配置（EF Core 的 OnModelCreating、FreeSql 的 Aop、SqlSugar 的 EntityService）对齐到 Schema。
// 集合导航不给初值：SqlSugar 的 Includes 看到父对象上的集合已经不是 null，就不再加载。

[Table("bench_customer")]
public class Customer
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";

    public int Level { get; set; }

    public DateTime CreatedAt { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Order.CustomerId)])]
    public List<Order>? Orders { get; set; }
}

[Table("bench_order")]
public class Order
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public string Title { get; set; } = "";

    public double Amount { get; set; }

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Remark { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(OrderItem.OrderId)])]
    public List<OrderItem>? Items { get; set; }
}

[Table("bench_order_item")]
public class OrderItem
{
    [Column(PrimaryKey = true)]
    public long Id { get; set; }

    public long OrderId { get; set; }

    public string Product { get; set; } = "";

    public int Quantity { get; set; }

    public double Price { get; set; }
}

/// <summary>投影目标：用可写属性的类，五个 ORM 都支持 <c>new OrderDto { .. }</c> 的写法。</summary>
public sealed class OrderDto
{
    public long Id { get; set; }

    public string Title { get; set; } = "";

    public double Amount { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>表名、列名的唯一来源，供其它 ORM 的映射配置使用。</summary>
public static class Schema
{
    public static readonly Type[] Entities = [typeof(Customer), typeof(Order), typeof(OrderItem)];

    /// <summary>实体的表名；不是实体（如 DTO）时为 null。</summary>
    public static string? Table(Type type) => type.GetCustomAttribute<TableAttribute>()?.Name;

    public static string Column(string property) => NameConversion.Underlined.Convert(property);

    public static bool IsKey(string property) => property == nameof(Order.Id);
}
