namespace EasyQuerySharp.Execution;

/// <summary>一次 SQL 执行的信息，交给 <see cref="IExecutionListener"/>。</summary>
public sealed class SqlExecutionEvent
{
    internal SqlExecutionEvent(SqlStatement statement, EasyDb db)
    {
        Statement = statement;
        Db = db;
    }

    /// <summary>执行的语句。</summary>
    public SqlStatement Statement { get; }

    /// <summary>所属的 <see cref="EasyDb"/>。</summary>
    public EasyDb Db { get; }

    /// <summary>耗时；执行前为 <see cref="TimeSpan.Zero"/>。</summary>
    public TimeSpan Elapsed { get; internal set; }

    /// <summary>写入语句的受影响行数，查询的读取行数；执行前或失败时为 null。</summary>
    public long? Rows { get; internal set; }

    /// <summary>失败时的异常。</summary>
    public Exception? Exception { get; internal set; }
}

/// <summary>
/// SQL 执行监听（对应 Java 的 JdbcExecutorListener）：执行前后各调用一次，用于日志、监控、测试里捕获 SQL。
/// 实现要线程安全且尽量轻，异常会中断执行。
/// </summary>
public interface IExecutionListener
{
    /// <summary>执行前调用。</summary>
    void OnExecuting(SqlExecutionEvent e)
    {
    }

    /// <summary>执行后调用（成功或失败都会调用）。</summary>
    void OnExecuted(SqlExecutionEvent e)
    {
    }
}
