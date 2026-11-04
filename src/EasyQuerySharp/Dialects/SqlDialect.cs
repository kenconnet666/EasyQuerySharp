using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Rendering;

namespace EasyQuerySharp.Dialects;

/// <summary>
/// 方言：决定标识符引用、占位符、分页、函数等写法。只负责渲染，不依赖任何驱动。
/// 子类只覆盖与基类不同的部分，例如兼容库继承被兼容的方言（PolarDB 用 MySQL 方言）。
/// </summary>
public abstract class SqlDialect
{
    /// <summary>方言名，用于报错与能力判断，如 <c>mysql</c>、<c>pgsql</c>。</summary>
    public abstract string Name { get; }

    /// <summary>给标识符加引号。默认用双引号（标准 SQL）。</summary>
    public virtual string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    /// <summary>驱动执行时的第 <paramref name="index"/> 个参数占位符（从 0 开始）；默认 <c>@p0</c>。</summary>
    public virtual string ParameterPlaceholder(int index) => "@p" + index;

    /// <summary>驱动参数对象的名字，与 <see cref="ParameterPlaceholder"/> 对应。</summary>
    public virtual string ParameterName(int index) => "p" + index;

    /// <summary>
    /// 插入后取自增键的方式：返回 null 表示在 INSERT 后加 <c>RETURNING 列</c> 直接读出；
    /// 否则是同一连接上紧接着执行的查询（如 MySQL 的 <c>SELECT LAST_INSERT_ID()</c>）。
    /// </summary>
    public virtual string? LastInsertIdQuery => null;

    /// <summary>写分页子句（追加在 ORDER BY 之后）。默认 <c>LIMIT n OFFSET m</c>。</summary>
    public virtual void WritePaging(SqlRenderer renderer, long? offset, long? limit)
    {
        if (limit is not null) renderer.Append(" LIMIT ").Append(limit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (offset is > 0) renderer.Append(" OFFSET ").Append(offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>写一个函数调用。默认按 <c>NAME(a,b)</c> 输出；子类按函数名覆盖差异。</summary>
    public virtual void WriteFunction(SqlRenderer renderer, FunctionExpr functionCall)
    {
        renderer.Append(functionCall.Name).Append("(");
        renderer.WriteList(functionCall.Arguments, ",");
        renderer.Append(")");
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
