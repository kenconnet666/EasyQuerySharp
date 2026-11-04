namespace EasyQuerySharp;

/// <summary>执行 SQL 失败（对应 Java 的 EasyQuerySQLStatementException）：带上出错的语句，原始驱动异常在 <see cref="Exception.InnerException"/>。</summary>
public sealed class EasyQueryException : Exception
{
    /// <summary>创建。</summary>
    public EasyQueryException(string message, SqlStatement? statement = null, Exception? inner = null) : base(message, inner)
    {
        Statement = statement;
    }

    /// <summary>出错的语句；不是执行阶段的错误时为 null。</summary>
    public SqlStatement? Statement { get; }

    /// <summary>出错的 SQL 文本（<c>?</c> 占位）。</summary>
    public string? Sql => Statement?.Text;
}
