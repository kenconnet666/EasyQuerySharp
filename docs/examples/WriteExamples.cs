using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"写入"页的卡片：插入、按条件更新、按实体更新、删除与逻辑删除、事务。</summary>
public static class WriteExamples
{
    [Example("write", "插入一行")]
    public static Task<long> InsertOne(EasyDb db) =>
        db.Insert(new Tag { Name = "性能" }).ExecuteAsync();

    [Example("write", "插入多行：每行一条语句")]
    public static Task<long> InsertMany(EasyDb db) =>
        db.Insert(new Tag { Name = "测试" }, new Tag { Name = "运维" }).ExecuteAsync();

    [Example("write", "批量写入：同一 SQL 的多行一次发出，整批在一个事务里")]
    public static Task<long> InsertBatch(EasyDb db) =>
        db.Insert(new Tag { Name = "前端" }, new Tag { Name = "后端" }, new Tag { Name = "数据" }).Batch().ExecuteAsync();

    [Example("write", "按条件更新：值可以引用原列")]
    public static Task<long> UpdateWithExpression(EasyDb db) =>
        db.Update<Post>().Set(p => new Post { Views = p.Views + 100, Title = p.Title + "（修订）" }).Where(p => p.Id == 1).ExecuteAsync();

    [Example("write", "条件里用集合导航")]
    public static Task<long> UpdateThroughNavigation(EasyDb db) =>
        db.Update<User>().Set(u => u.Level, UserLevel.Senior).Where(u => u.Posts!.Any(p => p.Views >= 2000)).ExecuteAsync();

    [Example("write", "按实体更新：按主键写全部列")]
    public static async Task<long> UpdateEntity(EasyDb db)
    {
        var product = await db.Query<Product>().FindAsync(6L);
        product!.Price = 25.00m;
        return await db.Update(product).ExecuteAsync();
    }

    [Example("write", "只写指定的列")]
    public static async Task<long> UpdateColumns(EasyDb db)
    {
        var product = await db.Query<Product>().FindAsync(6L);
        product!.Stock -= 1;
        return await db.Update(product).SetColumns(p => new { p.Stock }).ExecuteAsync();
    }

    [Example("write", "删除")]
    public static Task<long> Delete(EasyDb db) =>
        db.Delete<Comment>().Where(c => c.Likes < 3).ExecuteAsync();

    [Example("write", "逻辑删除：改写成 UPDATE，版本号一起加一")]
    public static async Task<long> LogicDelete(EasyDb db)
    {
        var doc = await db.Query<Doc>().FindAsync(2L);
        return await db.Delete(doc!).ExecuteAsync();
    }

    [Example("write", "事务：出错时整体回滚")]
    public static async Task<int?> Transaction(EasyDb db)
    {
        try
        {
            await db.TransactionAsync(async () =>
            {
                await db.Update<Product>().Set(p => new Product { Stock = p.Stock - 1 }).Where(p => p.Id == 5).ExecuteAsync();
                throw new InvalidOperationException("库存不足，取消");
            });
        }
        catch (InvalidOperationException)
        {
        }
        return (await db.Query<Product>().FindAsync(5L))?.Stock;
    }
}
