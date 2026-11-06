using EasyQuerySharp.Commands;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"写入"页（write 之外）：回填主键、写哪些列、字典写入、INSERT … SELECT、upsert、更新与删除的各种写法、经导航的更新删除。</summary>
public static class WritesExamples
{
    // ---- 插入

    [Example("writes", "插入后回填自增主键：FillGeneratedKeys", Unsupported = "oracle,dameng")]
    public static async Task<List<long>> FillGeneratedKeys(EasyDb db)
    {
        Tag[] tags = [new() { Name = "测试" }, new() { Name = "运维" }];
        await db.Insert(tags).FillGeneratedKeys().ExecuteAsync();
        return [.. tags.Select(t => t.Id)];
    }

    [Example("writes", "只写非 null 的列：Columns(ColumnStrategy.OnlyNotNull)")]
    public static Task<long> InsertOnlyNotNull(EasyDb db) =>
        db.Insert(new Profile { UserId = 5, Bio = "做数据的", Website = null }).Columns(ColumnStrategy.OnlyNotNull).ExecuteAsync();

    [Example("writes", "按字典写入：InsertMap / UpdateMap")]
    public static async Task<long> Maps(EasyDb db)
    {
        await db.InsertMap("t_tag", new Dictionary<string, object?> { ["name"] = "数据库" }).ExecuteAsync();
        return await db.UpdateMap("t_product", new Dictionary<string, object?> { ["id"] = 6L, ["stock"] = 150 }).WhereColumns("id").ExecuteAsync();
    }

    [Example("writes", "INSERT … SELECT：查询结果直接写进另一张表")]
    public static Task<long> InsertSelect(EasyDb db) =>
        db.Query<Category>().Where(c => c.ParentId == 1).Select(c => new Tag { Name = c.Name }).InsertInto().ExecuteAsync();

    // ---- upsert

    [Example("writes", "冲突时忽略：OnConflictIgnore（默认按主键判断冲突）")]
    public static Task<long> OnConflictIgnore(EasyDb db) =>
        db.Insert(new PostTag { PostId = 1, TagId = 1 }).OnConflictIgnore().ExecuteAsync();

    [Example("writes", "冲突时更新指定的列：OnConflictUpdate")]
    public static Task<long> OnConflictUpdate(EasyDb db) =>
        db.Insert(new OrderItem { OrderId = 1, ProductId = 6, Quantity = 3, Price = 29.90m })
            .OnConflictUpdate(i => new { i.Quantity, i.Price })
            .ExecuteAsync();

    // ---- 更新

    [Example("writes", "按条件更新：Set 单列、SetIf、Increment、SetNull")]
    public static Task<long> UpdateSets(EasyDb db)
    {
        var newName = (string?)null;
        return db.Update<Product>()
            .SetIf(newName is not null, p => p.Name, newName!)
            .Increment(p => p.Stock, 10)
            .Set(p => p.Price, p => p.Price * 0.9m)
            .Where(p => p.Stock < 30)
            .ExecuteAsync();
    }

    [Example("writes", "按实体更新：换定位列 WhereColumns、跳过几列 IgnoreColumns")]
    public static async Task<long> UpdateWhereColumns(EasyDb db)
    {
        var product = await db.Query<Product>().FindOrThrowAsync(6L);
        product.Price = 19.90m;
        product.Stock = 0;
        return await db.Update(product).WhereColumns(p => p.Name).IgnoreColumns(p => p.Stock).ExecuteAsync();
    }

    [Example("writes", "不追踪时只写改过的列：Changes(before)")]
    public static async Task<long> UpdateChanges(EasyDb db)
    {
        var before = await db.Query<Product>().FindOrThrowAsync(6L);
        var after = new Product { Id = before.Id, Name = before.Name, Price = before.Price, Stock = before.Stock - 5 };
        return await db.Update(after).Changes(before).ExecuteAsync();
    }

    [Example("writes", "批量更新：Batch()")]
    public static async Task<long> UpdateBatch(EasyDb db)
    {
        var products = await db.Query<Product>().Where(p => p.Stock >= 999).ToListAsync();
        foreach (var p in products) p.Stock = 500;
        return await db.Update(products).Batch().ExecuteAsync();
    }

    [Example("writes", "条件里经对一导航：生成带 JOIN 的 UPDATE")]
    public static Task<long> UpdateJoin(EasyDb db) =>
        db.Update<Post>().Set(p => p.Status, PostStatus.Draft).Where(p => p.Author!.Age < 18).ExecuteAsync();

    [Example("writes", "要求影响的行数：ExecuteAsync(期望行数, 消息)")]
    public static async Task<string> ExpectedRows(EasyDb db)
    {
        try
        {
            await db.Update<Product>().Set(p => p.Stock, 0).Where(p => p.Id == 999).ExecuteAsync(1, "商品不存在");
            return "updated";
        }
        catch (ConcurrencyException ex)
        {
            return ex.Message;
        }
    }

    // ---- 删除

    [Example("writes", "条件里经对一导航：生成带 JOIN 的 DELETE")]
    public static Task<long> DeleteJoin(EasyDb db) =>
        db.Delete<Comment>().Where(c => c.Post!.Status == PostStatus.Draft).ExecuteAsync();

    [Example("writes", "按实体删除与全表删除：All()")]
    public static async Task<long> DeleteEntitiesAndAll(EasyDb db)
    {
        var follows = await db.Query<Follow>().Where(f => f.FollowerId == 1).ToListAsync();
        await db.Delete(follows).ExecuteAsync();
        return await db.Delete<PostTag>().All().ExecuteAsync();
    }

    [Example("writes", "删除并返回被删的行：DELETE … RETURNING（PostgreSQL、SQLite）", Unsupported = "mysql,mssql,oracle,dameng")]
    public static Task<List<Comment>> DeleteReturning(EasyDb db) =>
        db.Delete<Comment>().Where(c => c.Likes <= 2).Returning().ExecuteAsync();
}
