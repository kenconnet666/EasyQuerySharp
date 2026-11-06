using System.Data;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"事务与行锁"页：显式事务、回调式事务、隔离级别、不能嵌套、行锁。</summary>
public static class TransactionExamples
{
    [Example("transaction", "显式事务：同一异步流程里这个库的语句自动加入")]
    public static async Task<int?> BeginTransaction(EasyDb db)
    {
        await using (var tx = db.BeginTransaction())
        {
            await db.Update<Product>().Set(p => p.Stock, p => p.Stock - 1).Where(p => p.Id == 3).ExecuteAsync();
            await db.Insert(new Order { UserId = 2, Status = OrderStatus.Pending, CreatedAt = new DateTime(2025, 9, 1) }).ExecuteAsync();
            await tx.CommitAsync();
        }
        return (await db.Query<Product>().FindAsync(3L))?.Stock;
    }

    [Example("transaction", "没有提交就释放：回滚")]
    public static async Task<int?> DisposeRollsBack(EasyDb db)
    {
        await using (var tx = db.BeginTransaction(IsolationLevel.Serializable))
        {
            await db.Update<Product>().Set(p => p.Stock, 0).Where(p => p.Id == 3).ExecuteAsync();
        }
        return (await db.Query<Product>().FindAsync(3L))?.Stock;
    }

    [Example("transaction", "已经在事务里再开事务：抛异常")]
    public static async Task<string> Nested(EasyDb db)
    {
        await using var tx = db.BeginTransaction();
        try
        {
            await using var inner = db.BeginTransaction();
            return "开启了嵌套事务";
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>（不是卡片）行锁：SQLite 没有行锁，示例库上跑不了，这里保证代码能编译。</summary>
    public static async Task<long> RowLock(EasyDb db)
    {
        // <snippet:transaction-lock>
        await using var tx = db.BeginTransaction();
        var product = await db.Query<Product>().Where(p => p.Id == 5).ForUpdate().SingleAsync();
        // 其它事务读这一行的 FOR UPDATE 会等到这里提交；不想等用 ForUpdateNoWait()，跳过被锁的行用 ForUpdateSkipLocked()
        var updated = await db.Update<Product>().Set(p => p.Stock, product.Stock - 1).Where(p => p.Id == 5).ExecuteAsync();
        await tx.CommitAsync();
        // </snippet:transaction-lock>
        return updated;
    }
}
