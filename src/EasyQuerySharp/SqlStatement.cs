namespace EasyQuerySharp;

/// <summary>一个绑定参数：值与它的 CLR 类型。</summary>
/// <param name="Value">参数值（已经过值转换器，null 表示 SQL NULL）。</param>
/// <param name="ClrType">值的类型，值为 null 时用于推断驱动类型。</param>
public sealed record SqlParameterValue(object? Value, Type ClrType)
{
    /// <summary>输出参数（如 Oracle 的 <c>RETURNING id INTO :p</c>）：执行后从这里读回值，不传入值。</summary>
    public bool IsOutput { get; init; }
}

/// <summary>
/// 渲染好的一条 SQL：<see cref="CommandText"/> 交给驱动执行（方言的命名占位符），
/// <see cref="Text"/> 用 <c>?</c> 占位，供日志、<c>ToSql()</c> 与跨语言（Java 版）比对。
/// </summary>
public sealed record SqlStatement(string CommandText, string Text, IReadOnlyList<SqlParameterValue> Parameters)
{
    /// <summary>语句带行锁（FOR UPDATE），只能在事务里执行。</summary>
    public bool RequiresTransaction { get; init; }

    /// <summary>在哪个数据源上执行（分库分表路由后写入，见 <see cref="EasyDbOptions.AddDataSource"/>）；null 是默认数据源。</summary>
    public string? DataSource { get; init; }

    /// <summary>写入语句的标记（表、种类、取主键）：执行成功后据此通知写入监听器；查询与原生 SQL 为 null。</summary>
    internal Execution.WriteMark? Write { get; init; }

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>
/// 原生 SQL 插值里的标识符（表名、列名）：按方言加引号原样写入，不参数化。用 <see cref="Sql.Identifier"/> 构造。
/// </summary>
/// <example><code>await db.SqlQueryAsync&lt;long&gt;($"SELECT COUNT(*) FROM {Sql.Identifier("t_user")} WHERE {Sql.Identifier("age")} &gt; {20}");</code></example>
public sealed record SqlIdentifier(string Name);
