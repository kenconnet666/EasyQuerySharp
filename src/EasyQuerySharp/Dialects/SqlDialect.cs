using System.Globalization;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

/// <summary>写一个函数调用的委托：参数已翻译好，用 <paramref name="r"/> 输出。</summary>
public delegate void FunctionWriter(SqlRenderer r, IReadOnlyList<SqlExpr> args);

/// <summary>
/// 方言：决定标识符引用、占位符、分页、函数等写法。只负责渲染，不依赖任何驱动。
/// 子类只覆盖与基类不同的部分，例如兼容库继承被兼容的方言（PolarDB 用 MySQL 方言）。
/// 函数写法集中在 <see cref="RegisterFunctions"/>：基类登记标准 SQL 的写法，子类覆盖差异。
/// </summary>
public abstract class SqlDialect
{
    private Dictionary<string, FunctionWriter>? _functions;

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
    public virtual void WritePaging(SqlRenderer renderer, SelectQuery query)
    {
        if (query.Limit is { } limit) renderer.Append(" LIMIT ").Append(limit.ToString(CultureInfo.InvariantCulture));
        if (query.Offset is > 0) renderer.Append(" OFFSET ").Append(query.Offset.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 写一条带分页的查询：<paramref name="writeUnpaged"/> 输出不含分页的完整 SELECT。
    /// 默认在其后追加 <see cref="WritePaging"/>；需要包一层的方言（Oracle 的 ROWNUM、SQL Server 2008 的 ROW_NUMBER）覆盖它。
    /// </summary>
    public virtual void WritePagedSelect(SqlRenderer renderer, SelectQuery query, Action writeUnpaged)
    {
        writeUnpaged();
        WritePaging(renderer, query);
    }

    /// <summary>写 LIKE。默认 <c>x LIKE ?</c>（模式里已拼好 %）；方言可按 <see cref="LikeExpr.Mode"/> 改写。</summary>
    public virtual void WriteLike(SqlRenderer renderer, LikeExpr predicate) =>
        renderer.Write(predicate.Operand).Append(predicate.Negated ? " NOT LIKE " : " LIKE ").Write(predicate.Pattern);

    /// <summary>写一个函数调用：查函数表，没有登记的按 <c>NAME(a,b)</c> 输出。</summary>
    public void WriteFunction(SqlRenderer renderer, FunctionExpr functionCall)
    {
        _functions ??= BuildFunctions();
        if (_functions.TryGetValue(functionCall.Name, out var writer)) writer(renderer, functionCall.Arguments);
        else Call(functionCall.Name)(renderer, functionCall.Arguments);
    }

    /// <summary>
    /// 登记函数写法。子类先调用 <c>base.RegisterFunctions(f)</c>，再覆盖与标准写法不同的部分。
    /// 名字见 <see cref="SqlFunctionNames"/>。
    /// </summary>
    protected virtual void RegisterFunctions(IDictionary<string, FunctionWriter> f)
    {
        f[SqlFunctionNames.NullOrDefault] = Call("COALESCE");
        f[SqlFunctionNames.EqualsWith] = (r, a) => r.Append("(CASE ").Write(a[0]).Append(" WHEN ").Write(a[1]).Append(" THEN 1 ELSE 0 END)");
        f[SqlFunctionNames.Concat] = Call("CONCAT");
        f[SqlFunctionNames.Length] = Call("CHAR_LENGTH");
        f[SqlFunctionNames.Trim] = Call("TRIM");
        f[SqlFunctionNames.SubString] = Call("SUBSTR");
        f[SqlFunctionNames.PadLeft] = Pad("LPAD", null);
        f[SqlFunctionNames.PadRight] = Pad("RPAD", null);
        f[SqlFunctionNames.CompareTo] = (r, a) => r.Append("(CASE WHEN ").Write(a[0]).Append(" = ").Write(a[1])
            .Append(" THEN 0 WHEN ").Write(a[0]).Append(" > ").Write(a[1]).Append(" THEN 1 ELSE -1 END)");
        f[SqlFunctionNames.IndexOf] = Call("INSTR");
        f[SqlFunctionNames.Ceiling] = Call("CEIL");
        f[SqlFunctionNames.Ln] = Call("LN");
        f[SqlFunctionNames.Truncate] = Call("TRUNC");
        f[SqlFunctionNames.CountDistinct] = Distinct("COUNT");
        f[SqlFunctionNames.SumDistinct] = Distinct("SUM");
        f[SqlFunctionNames.AvgDistinct] = Distinct("AVG");
        f[SqlFunctionNames.StringJoin] = (r, a) => r.Append("STRING_AGG(").Write(a[0]).Append(", ").Write(a[1]).Append(")");
        f[SqlFunctionNames.StringJoinDistinct] = (r, a) => r.Append("STRING_AGG(DISTINCT ").Write(a[0]).Append(", ").Write(a[1]).Append(")");
    }

    /// <summary><c>NAME(a,b)</c>。</summary>
    protected static FunctionWriter Call(string name) => (r, a) => r.Append(name).Append("(").WriteList(a, ",").Append(")");

    /// <summary><c>NAME(DISTINCT x)</c>。</summary>
    protected static FunctionWriter Distinct(string name) => (r, a) => r.Append(name).Append("(DISTINCT ").Write(a[0]).Append(")");

    /// <summary>参数顺序与 C# 不同的调用：<paramref name="order"/> 给出输出时依次取第几个参数。</summary>
    protected static FunctionWriter Reordered(string name, params int[] order) =>
        (r, a) => r.Append(name).Append("(").WriteList(order.Where(i => i < a.Count).Select(i => a[i]).ToList(), ",").Append(")");

    /// <summary><c>LPAD(s, n)</c> / <c>LPAD(s, n, pad)</c>：Java 版在长度前留一个空格；<paramref name="defaultPad"/> 不为 null 时没给填充字符也写出它。</summary>
    protected static FunctionWriter Pad(string name, string? defaultPad) => (r, a) =>
    {
        r.Append(name).Append("(").Write(a[0]).Append(", ").Write(a[1]);
        if (a.Count > 2) r.Append(", ").Write(a[2]);
        else if (defaultPad is not null) r.Append(", ").Append(defaultPad);
        r.Append(")");
    };

    /// <summary>用分隔符连接参数（不加括号），如 Oracle 的 <c>a || b</c>。</summary>
    protected static FunctionWriter Infix(string separator) => (r, a) => r.WriteList(a, separator);

    private Dictionary<string, FunctionWriter> BuildFunctions()
    {
        var functions = new Dictionary<string, FunctionWriter>(StringComparer.Ordinal);
        RegisterFunctions(functions);
        return functions;
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
