using System.Diagnostics;

namespace EasyQuerySharp.Tests.Upstream.Infrastructure;

/// <summary>行锁用例共用的断言（MySQL 8 与 PostgreSQL 两套 ForUpdateTest 相同的部分）。</summary>
internal static class RowLockAssert
{
    /// <summary><paramref name="sql"/> 以 FOR UPDATE 结尾，且只出现一次（锁只写在最外层）。</summary>
    public static void EndsWithSingleForUpdate(string sql)
    {
        Assert.EndsWith(" FOR UPDATE", sql);
        Assert.Equal(1, sql.Split("FOR UPDATE").Length - 1);
    }

    /// <summary>
    /// 第一个事务用 <paramref name="lockRow"/> 锁住行后，第二个事务执行同样的查询要等第一个事务提交才返回
    /// （对应 Java 的 testForUpdateBlocksConcurrentTransaction）。<paramref name="lockRow"/> 在事务里调用。
    /// </summary>
    public static async Task SecondTransactionWaitsAsync(EasyDb db, Func<Task> lockRow)
    {
        var ct = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(5);
        var firstLocked = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();
        var first = Task.Run(async () =>
        {
            await using var tx = db.BeginTransaction();
            await lockRow();
            firstLocked.SetResult();
            await releaseFirst.Task.WaitAsync(timeout, ct);
            await tx.CommitAsync();
        }, ct);
        await firstLocked.Task.WaitAsync(timeout, ct);

        var waited = Stopwatch.StartNew();
        var second = Task.Run(async () =>
        {
            await using var tx = db.BeginTransaction();
            await lockRow();
            await tx.CommitAsync();
            waited.Stop();
        }, ct);
        await Task.Delay(300);
        Assert.False(second.IsCompleted, "第二个事务应当被行锁挡住");
        releaseFirst.SetResult();
        await second.WaitAsync(timeout);
        await first;
        Assert.True(waited.ElapsedMilliseconds >= 250, $"第二个事务只等了 {waited.ElapsedMilliseconds}ms");
    }
}
