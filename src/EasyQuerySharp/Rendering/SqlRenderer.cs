using System.Globalization;
using System.Text;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Rendering;

/// <summary>
/// 把 SQL AST 渲染成文本。同时写两份：交给驱动的（方言占位符）与展示用的（<c>?</c> 占位），参数按出现顺序收集。
/// 格式与 Java 版逐字一致：逗号后不留空格、OR 组加括号、函数参数间无空格。
/// 每条语句用一个新实例；方言通过 <see cref="Append"/>、<see cref="Write(SqlExpr)"/> 输出自己的写法。
/// </summary>
public sealed class SqlRenderer
{
    private readonly StringBuilder _command = new();
    private readonly StringBuilder _text = new();
    private readonly List<SqlParameterValue> _parameters = [];

    private SqlRenderer(SqlDialect dialect) => Dialect = dialect;

    /// <summary>当前方言。</summary>
    public SqlDialect Dialect { get; }

    /// <summary>渲染一条查询。</summary>
    public static SqlStatement Render(SqlDialect dialect, SelectQuery query)
    {
        AliasAllocator.Allocate(query);
        var r = new SqlRenderer(dialect);
        r.WriteSelect(query);
        return r.Build();
    }

    /// <summary>渲染一条插入。</summary>
    public static SqlStatement Render(SqlDialect dialect, InsertStatement insert)
    {
        insert.Table.Alias = null;
        var r = new SqlRenderer(dialect);
        r.Append("INSERT INTO ").WriteTableName(insert.Table).Append(" (");
        for (var i = 0; i < insert.Columns.Count; i++)
        {
            if (i > 0) r.Append(",");
            r.Append(dialect.QuoteIdentifier(insert.Columns[i].Name));
        }
        r.Append(") VALUES ");
        for (var row = 0; row < insert.Rows.Count; row++)
        {
            if (row > 0) r.Append(",");
            r.Append("(").WriteList(insert.Rows[row], ",").Append(")");
        }
        if (insert.Returning.Count > 0) r.Append(" RETURNING ").WriteList(insert.Returning, ",");
        return r.Build();
    }

    /// <summary>渲染一条更新。</summary>
    public static SqlStatement Render(SqlDialect dialect, UpdateStatement update)
    {
        AliasAllocator.Allocate(update.Table, update.Sets.Select(s => s.Value).Concat(update.Where));
        var r = new SqlRenderer(dialect);
        r.Append("UPDATE ").WriteTable(update.Table).Append(" SET ");
        for (var i = 0; i < update.Sets.Count; i++)
        {
            if (i > 0) r.Append(",");
            r.Write(update.Sets[i].Column).Append(" = ").Write(update.Sets[i].Value);
        }
        r.WriteWhere(update.Where);
        return r.Build();
    }

    /// <summary>渲染一条删除。</summary>
    public static SqlStatement Render(SqlDialect dialect, DeleteStatement delete)
    {
        AliasAllocator.Allocate(delete.Table, delete.Where);
        var r = new SqlRenderer(dialect);
        r.Append("DELETE FROM ").WriteTable(delete.Table);
        r.WriteWhere(delete.Where);
        return r.Build();
    }

    /// <summary>追加原样文本（两份输出都追加）。</summary>
    public SqlRenderer Append(string text)
    {
        _command.Append(text);
        _text.Append(text);
        return this;
    }

    /// <summary>用分隔符连接输出一组表达式。</summary>
    public SqlRenderer WriteList(IReadOnlyList<SqlExpr> items, string separator)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) Append(separator);
            Write(items[i]);
        }
        return this;
    }

    /// <summary>输出一个表达式。</summary>
    public SqlRenderer Write(SqlExpr expr)
    {
        switch (expr)
        {
            case ColumnExpr c:
                if (c.Table.Alias is not null) Append(c.Table.Alias).Append(".");
                Append(Dialect.QuoteIdentifier(c.Name));
                break;
            case ParameterExpr p: WriteParameter(p); break;
            case LiteralExpr l: Append(l.Sql); break;
            case BinaryExpr b: WriteBinary(b); break;
            case LogicalExpr l: WriteLogical(l); break;
            case NotExpr n: Append("NOT (").Write(n.Operand).Append(")"); break;
            case IsNullExpr n: Write(n.Operand).Append(n.Negated ? " IS NOT NULL" : " IS NULL"); break;
            case InExpr i: WriteIn(i); break;
            case LikeExpr l: Dialect.WriteLike(this, l); break;
            case FunctionExpr f: Dialect.WriteFunction(this, f); break;
            case FragmentExpr f: WriteFragment(f); break;
            case CastExpr c: WriteFragment(new FragmentExpr(Dialect.CastTemplate(c.TargetType), [c.Operand])); break;
            case CaseExpr c: WriteCase(c); break;
            case ExistsExpr e: Append(e.Negated ? "NOT EXISTS(" : "EXISTS(").WriteSelect(e.Query).Append(")"); break;
            case SubQueryExpr s: Append("(").WriteSelect(s.Query).Append(")"); break;
            default: throw new NotSupportedException($"无法渲染 SQL 节点 {expr.GetType().Name}");
        }
        return this;
    }

    /// <summary>输出一条 SELECT（不含外层括号）；有分页时交给方言决定是追加子句还是包一层。</summary>
    public SqlRenderer WriteSelect(SelectQuery q)
    {
        if (q.Offset is null && q.Limit is null) return WriteUnpagedSelect(q);
        Dialect.WritePagedSelect(this, q, () => WriteUnpagedSelect(q));
        return this;
    }

    private SqlRenderer WriteUnpagedSelect(SelectQuery q)
    {
        if (q.Columns.Count == 0) throw new InvalidOperationException("SELECT 列表为空");
        Append(q.Distinct ? "SELECT DISTINCT " : "SELECT ");
        for (var i = 0; i < q.Columns.Count; i++)
        {
            if (i > 0) Append(",");
            var c = q.Columns[i];
            Write(c.Expr);
            if (c.Alias is not null) Append(" AS ").Append(Dialect.QuoteIdentifier(c.Alias));
        }
        Append(" FROM ").WriteTable(q.From);
        foreach (var j in q.Joins)
        {
            Append(j.Kind switch
            {
                JoinKind.Inner => " INNER JOIN ",
                JoinKind.Left => " LEFT JOIN ",
                JoinKind.Right => " RIGHT JOIN ",
                JoinKind.Full => " FULL JOIN ",
                _ => " CROSS JOIN ",
            }).WriteTable(j.Table);
            if (j.On is not null) Append(" ON ").WritePredicate(j.On);
        }
        WriteWhere(q.Where);
        if (q.GroupBy.Count > 0) Append(" GROUP BY ").WriteList(q.GroupBy, ",");
        if (q.Having.Count > 0) Append(" HAVING ").WritePredicate(LogicalExpr.And(q.Having));
        if (q.OrderBy.Count > 0)
        {
            Append(" ORDER BY ");
            for (var i = 0; i < q.OrderBy.Count; i++)
            {
                if (i > 0) Append(",");
                Write(q.OrderBy[i].Expr).Append(q.OrderBy[i].Descending ? " DESC" : " ASC");
            }
        }
        return this;
    }

    private SqlStatement Build() => new(_command.ToString(), _text.ToString(), _parameters);

    private void WriteWhere(List<SqlExpr> where)
    {
        if (where.Count > 0) Append(" WHERE ").WritePredicate(LogicalExpr.And(where));
    }

    // 顶层 AND 平铺输出；其余交给 Write（OR 组自带括号）
    private SqlRenderer WritePredicate(SqlExpr predicate) => Write(predicate);

    private SqlRenderer WriteTable(TableSource table)
    {
        switch (table)
        {
            case EntityTableSource e: WriteTableName(e); break;
            case DerivedTableSource d: Append("(").WriteSelect(d.Query).Append(")"); break;
            case RawTableSource raw: Append("(").Append(raw.Sql).Append(")"); break;
            case UnionTableSource u:
                // 与 Java 逐字一致：( (q1)  UNION  (q2) )
                Append("( ");
                for (var i = 0; i < u.Parts.Count; i++)
                {
                    if (i > 0) Append(u.All ? "  UNION ALL  " : "  UNION  ");
                    Append("(").WriteSelect(u.Parts[i]).Append(")");
                }
                Append(" )");
                break;
        }
        if (table.Alias is not null) Append(" ").Append(table.Alias);
        return this;
    }

    private SqlRenderer WriteTableName(EntityTableSource e)
    {
        if (e.Schema is not null) Append(Dialect.QuoteIdentifier(e.Schema)).Append(".");
        return Append(Dialect.QuoteIdentifier(e.TableName));
    }

    private void WriteParameter(ParameterExpr p)
    {
        var index = _parameters.Count;
        _parameters.Add(new SqlParameterValue(p.Value, p.ClrType));
        _command.Append(Dialect.ParameterPlaceholder(index));
        _text.Append('?');
    }

    // 算术运算总是加括号（Java 版的写法）：(a + b)、((a + b) * c)；比较不加
    private void WriteBinary(BinaryExpr b)
    {
        if (!b.IsComparison) Append("(");
        Write(b.Left);
        Append(b.Operator switch
        {
            SqlBinaryOperator.Equal => " = ",
            SqlBinaryOperator.NotEqual => " <> ",
            SqlBinaryOperator.LessThan => " < ",
            SqlBinaryOperator.LessThanOrEqual => " <= ",
            SqlBinaryOperator.GreaterThan => " > ",
            SqlBinaryOperator.GreaterThanOrEqual => " >= ",
            SqlBinaryOperator.Add => " + ",
            SqlBinaryOperator.Subtract => " - ",
            SqlBinaryOperator.Multiply => " * ",
            SqlBinaryOperator.Divide => " / ",
            SqlBinaryOperator.Modulo => " % ",
            _ => throw new NotSupportedException(b.Operator.ToString()),
        });
        Write(b.Right);
        if (!b.IsComparison) Append(")");
    }

    private void WriteLogical(LogicalExpr l)
    {
        if (l.IsAnd)
        {
            for (var i = 0; i < l.Operands.Count; i++)
            {
                if (i > 0) Append(" AND ");
                Write(l.Operands[i]);
            }
            return;
        }
        Append("(");
        for (var i = 0; i < l.Operands.Count; i++)
        {
            if (i > 0) Append(" OR ");
            // OR 里的 AND 组要加括号，保持原有的结合关系
            if (l.Operands[i] is LogicalExpr { IsAnd: true }) Append("(").Write(l.Operands[i]).Append(")");
            else Write(l.Operands[i]);
        }
        Append(")");
    }

    private void WriteIn(InExpr i)
    {
        if (i.SubQuery is not null)
        {
            Write(i.Operand).Append(i.Negated ? " NOT IN (" : " IN (").WriteSelect(i.SubQuery).Append(")");
            return;
        }
        // 空列表：IN 恒假、NOT IN 恒真（与 Java 版一致）
        if (i.Values!.Count == 0)
        {
            Append(i.Negated ? "1 = 1" : "1 = 2");
            return;
        }
        Write(i.Operand).Append(i.Negated ? " NOT IN (" : " IN (").WriteList(i.Values, ",").Append(")");
    }

    private void WriteFragment(FragmentExpr f)
    {
        var t = f.Template;
        var start = 0;
        for (var i = 0; i < t.Length; i++)
        {
            if (t[i] != '{') continue;
            var close = t.IndexOf('}', i);
            if (close < 0 || !int.TryParse(t.AsSpan(i + 1, close - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index)) continue;
            Append(t[start..i]);
            Write(f.Arguments[index]);
            start = close + 1;
            i = close;
        }
        Append(t[start..]);
    }

    private void WriteCase(CaseExpr c)
    {
        Append("(CASE");
        foreach (var (when, then) in c.Branches) Append(" WHEN ").Write(when).Append(" THEN ").Write(then);
        if (c.Else is not null) Append(" ELSE ").Write(c.Else);
        Append(" END)");
    }
}
