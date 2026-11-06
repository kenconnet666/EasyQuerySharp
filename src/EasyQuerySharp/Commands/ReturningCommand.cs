using System.Linq.Expressions;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 带 <c>RETURNING</c> 的按条件更新 / 删除（对应 Java fork 的 returning、TS 版的 <c>returning()</c>），由 <c>Returning()</c> 得到：
/// 执行后返回受影响的行，更新是更新后的值，删除是删掉的行（逻辑删除时是被标记的行）。
/// PostgreSQL、SQLite 支持；其他库渲染时报不支持。
/// </summary>
public sealed class ReturningCommand<T, TResult> where T : class
{
    private readonly EasyDb _db;
    private readonly EntityMetadata _entity;
    private readonly Func<IReadOnlyList<ColumnMetadata>, SqlStatement> _render;
    private readonly IReadOnlyList<ColumnMetadata> _columns;
    private readonly Func<T, TResult> _project;

    internal ReturningCommand(EasyDb db, EntityMetadata entity, Func<IReadOnlyList<ColumnMetadata>, SqlStatement> render, LambdaExpression selector)
    {
        _db = db;
        _entity = entity;
        _render = render;
        _columns = ReturnedColumns.Of(entity, selector);
        _project = (Func<T, TResult>)selector.Compile();
    }

    /// <summary>渲染语句，不执行。</summary>
    public string ToSql() => ToStatement().Text;

    /// <summary>同 <see cref="ToSql"/>，连同参数。</summary>
    public SqlStatement ToStatement() => _render(_columns);

    /// <summary>执行，返回受影响的行。</summary>
    public Task<List<TResult>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        _db.Executor.QueryAsync(ToStatement(), r => _project(EntityMaterializer.ReadByName<T>(r, _entity)), cancellationToken);
}

/// <summary>RETURNING 要取的列：选择器用到的这张表的列；直接用到整个实体时为空（整行）。</summary>
internal sealed class ReturnedColumns : ExpressionVisitor
{
    private readonly EntityMetadata _entity;
    private readonly ParameterExpression _row;
    private readonly HashSet<ColumnMetadata> _columns = [];
    private bool _wholeRow;

    private ReturnedColumns(EntityMetadata entity, ParameterExpression row)
    {
        _entity = entity;
        _row = row;
    }

    public static IReadOnlyList<ColumnMetadata> Of(EntityMetadata entity, LambdaExpression selector)
    {
        var visitor = new ReturnedColumns(entity, selector.Parameters[0]);
        visitor.Visit(selector.Body);
        return visitor._wholeRow ? [] : [.. entity.Columns.Where(visitor._columns.Contains)];
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == _row) _wholeRow = true;
        return node;
    }

    // t.A.B.C：取最短的是列的前缀（之后的 .Value 之类是对值的运算）；整个值对象取它的全部列
    protected override Expression VisitMember(MemberExpression node)
    {
        var names = new List<string>();
        Expression? current = node;
        for (; current is MemberExpression member; current = member.Expression) names.Insert(0, member.Member.Name);
        if (current != _row) return base.VisitMember(node);
        for (var i = 1; i <= names.Count; i++)
        {
            var path = string.Join('.', names.Take(i));
            if (_entity.FindColumn(path) is { } column)
            {
                _columns.Add(column);
                return node;
            }
            if (!_entity.ValueObjects.TryGetValue(path, out var valueObject))
                throw new NotSupportedException($"Returning 只能取 {_entity.TableName} 自己的列：{path} 不是列");
            if (i == names.Count) _columns.UnionWith(valueObject.Columns);
        }
        return node;
    }
}
