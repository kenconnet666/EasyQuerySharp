namespace EasyQuerySharp.SqlAst;

/// <summary>查询的行锁（对应 Java 的 QueryLockEnum），只写在最外层查询的末尾。</summary>
public enum RowLock
{
    /// <summary>不加锁。</summary>
    None,

    /// <summary><c>FOR UPDATE</c>：锁住查到的行，别的事务要等本事务结束。</summary>
    ForUpdate,

    /// <summary><c>FOR UPDATE NOWAIT</c>：行已被别的事务锁住时立即报错，不等待。</summary>
    ForUpdateNoWait,

    /// <summary><c>FOR UPDATE SKIP LOCKED</c>：跳过已被别的事务锁住的行，常用于多个消费者抢任务。</summary>
    ForUpdateSkipLocked,
}
