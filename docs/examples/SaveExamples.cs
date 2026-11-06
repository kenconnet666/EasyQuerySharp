using EasyQuerySharp.Metadata;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"追踪与聚合保存"页：追踪作用域里的差异更新、db.Save 按快照差异写出整个聚合。</summary>
public static class SaveExamples
{
    // ---- 追踪

    [Example("save", "追踪作用域里查出的实体，更新时只写改过的列")]
    public static async Task<long> TrackedUpdate(EasyDb db)
    {
        using var tracking = db.BeginTracking();
        var product = await db.Query<Product>().FindOrThrowAsync(5L);
        product.Stock -= 1;
        return await db.Update(product).ExecuteAsync();
    }

    [Example("save", "查看改了哪些属性：GetTrackedChanges")]
    public static async Task<IReadOnlyList<string>> TrackedChanges(EasyDb db)
    {
        using var tracking = db.BeginTracking();
        var user = await db.Query<User>().FindOrThrowAsync(1L);
        user.Age = 30;
        user.Level = UserLevel.Lead;
        return db.GetTrackedChanges(user);
    }

    [Example("save", "作用域里不追踪某条查询：AsNoTracking")]
    public static async Task<bool> NoTracking(EasyDb db)
    {
        using var tracking = db.BeginTracking();
        var user = await db.Query<User>().AsNoTracking().FindOrThrowAsync(1L);
        return db.RemoveTracking(user);
    }

    // ---- 聚合保存

    // <snippet:save-model>
    [Table("t_order")]
    public class SalesOrder
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public long UserId { get; set; }

        [Column(Converter = typeof(EnumNameConverter<OrderStatus>))]
        public OrderStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        // 明细是订单的一部分：新增插入、改动更新、从集合里拿掉的删除
        [Navigate(RelationType.OneToMany, TargetProperties = [nameof(SalesOrderLine.OrderId)], Cascade = Cascade.Delete)]
        public List<SalesOrderLine>? Lines { get; set; }
    }

    [Table("t_order_item")]
    public class SalesOrderLine
    {
        [Column(PrimaryKey = true)]
        public long OrderId { get; set; }

        [Column(PrimaryKey = true)]
        public long ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal Price { get; set; }
    }
    // </snippet:save-model>

    [Example("save", "保存聚合：改数量、删一行、加一行")]
    public static async Task<List<string>> SaveAggregate(EasyDb db)
    {
        using var tracking = db.BeginTracking();
        await using var tx = db.BeginTransaction();
        var order = await db.Query<SalesOrder>().Include(o => o.Lines, q => q.OrderBy(l => l.ProductId)).WhereById(1L).SingleAsync();
        order.Lines![0].Quantity = 2;                                                  // 年度会员改成 2 份
        order.Lines.RemoveAt(1);                                                        // 不要贴纸礼包了
        order.Lines.Add(new SalesOrderLine { OrderId = order.Id, ProductId = 3, Quantity = 1, Price = 199.00m }); // 加一门课
        await db.Save(order).ExecuteAsync();
        await tx.CommitAsync();
        return await db.Query<OrderItem>().Where(i => i.OrderId == 1).OrderBy(i => i.ProductId)
            .Select(i => i.Product!.Name + " × " + i.Quantity).ToListAsync();
    }

    [Example("save", "删除聚合根：RemoveRoot，拥有的子对象一起删")]
    public static async Task<long> RemoveRoot(EasyDb db)
    {
        using var tracking = db.BeginTracking();
        await using var tx = db.BeginTransaction();
        var order = await db.Query<SalesOrder>().Include(o => o.Lines).WhereById(5L).SingleAsync();
        await db.Save(order).RemoveRoot().ExecuteAsync();
        await tx.CommitAsync();
        return await db.Query<OrderItem>().Where(i => i.OrderId == 5).CountAsync();
    }
}
