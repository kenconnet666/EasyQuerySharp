namespace EasyQuerySharp;

/// <summary>一个绑定参数：值与它的 CLR 类型。</summary>
/// <param name="Value">参数值（已经过值转换器，null 表示 SQL NULL）。</param>
/// <param name="ClrType">值的类型，值为 null 时用于推断驱动类型。</param>
public sealed record SqlParameterValue(object? Value, Type ClrType);

/// <summary>
/// 渲染好的一条 SQL：<see cref="CommandText"/> 交给驱动执行（方言的命名占位符），
/// <see cref="Text"/> 用 <c>?</c> 占位，供日志、<c>ToSql()</c> 与跨语言（Java 版）比对。
/// </summary>
public sealed record SqlStatement(string CommandText, string Text, IReadOnlyList<SqlParameterValue> Parameters)
{
    /// <summary>语句带行锁（FOR UPDATE），只能在事务里执行。</summary>
    public bool RequiresTransaction { get; init; }

    /// <inheritdoc />
    public override string ToString() => Text;
}
