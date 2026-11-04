using System.Data.Common;
using System.Linq.Expressions;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Querying;

/// <summary>
/// 把投影 lambda 拆成两部分：SELECT 列表（交给 SQL），以及按列序号重建结果对象的读取函数（交给物化）。
/// <list type="bullet">
/// <item>投影到整个实体（<c>u => u</c>）：选出实体的全部默认列；</item>
/// <item>new 匿名类型 / record / 成员初始化：每个成员递归处理，标量成员一列；</item>
/// <item>标量：一列。</item>
/// </list>
/// 列别名规则与 Java 版的 DTO 映射一致：别名是成员名按命名约定转换后的列名，与源列同名时省略 AS。
/// </summary>
internal sealed class ProjectionCompiler(ExpressionTranslator translator, NameConversion naming)
{
    private readonly ParameterExpression _reader = Expression.Parameter(typeof(DbDataReader), "reader");
    private readonly List<SelectColumn> _columns = [];

    /// <summary>投影出的列。</summary>
    public IReadOnlyList<SelectColumn> Columns => _columns;

    /// <summary>成员名 → 输出列名。投影作为派生表时，外层按成员名找列。</summary>
    public Dictionary<string, string> MemberColumns { get; } = new(StringComparer.Ordinal);

    /// <summary>投影的是整个实体时的元数据（派生表据此按实体物化）；否则为 null。</summary>
    public EntityMetadata? Entity { get; private set; }

    private Expression? _shaped;

    /// <summary>拆解投影：登记 SELECT 列，并构造（尚未编译的）读取表达式。</summary>
    public void Prepare(Expression body) => _shaped = Shape(body, memberName: null, topLevel: true);

    /// <summary>编译读取函数；须先调用 <see cref="Prepare"/>。</summary>
    public Func<DbDataReader, T> Compile<T>()
    {
        var shaped = _shaped ?? throw new InvalidOperationException("投影还没有 Prepare");
        return Expression.Lambda<Func<DbDataReader, T>>(Coerce(shaped, typeof(T)), _reader).Compile();
    }

    private Expression Shape(Expression body, string? memberName, bool topLevel)
    {
        if (translator.BindingOf(body) is { } binding) return ShapeEntity(binding, body.Type, topLevel);

        switch (body)
        {
            case NewExpression n when n.Constructor is not null:
            {
                var names = MemberNames(n);
                var args = n.Arguments.Select((a, i) => Coerce(Shape(a, names[i], false), a.Type)).ToList();
                return n.Members is null ? Expression.New(n.Constructor, args) : Expression.New(n.Constructor, args, n.Members);
            }
            case MemberInitExpression mi:
            {
                var newExpr = (NewExpression)Shape(mi.NewExpression, memberName, false);
                var bindings = mi.Bindings.Cast<MemberAssignment>()
                    .Select(b => Expression.Bind(b.Member, Coerce(Shape(b.Expression, b.Member.Name, false), b.Expression.Type)));
                return Expression.MemberInit(newExpr, bindings);
            }
            default:
                return ShapeScalar(body, memberName);
        }
    }

    // 整个实体：顶层时（u => u）记下实体元数据与成员列；嵌在 DTO 里时（new { u, … }）LEFT JOIN 没连上的行读成 null
    private Expression ShapeEntity(SourceBinding binding, Type type, bool topLevel)
    {
        var start = _columns.Count;
        var (entity, columns) = binding switch
        {
            EntityBinding e => (e.Entity, (IReadOnlyList<ColumnMetadata>)e.Entity.Columns.Where(c => c.AutoSelect).ToList()),
            DerivedBinding { Entity: { } meta } d => (meta, meta.Columns.Where(c => d.MemberColumns.ContainsKey(c.PropertyName)).ToList()),
            _ => throw new NotSupportedException($"不能把 {type.Name} 作为整体投影"),
        };
        foreach (var c in columns)
        {
            var sql = binding is DerivedBinding db ? new ColumnExpr(db.Table, db.MemberColumns[c.PropertyName], c) : ((EntityBinding)binding).EntityTable.Column(c);
            AddColumn(sql, alias: null, topLevel ? c.PropertyName : null);
        }
        if (topLevel) Entity = entity;

        return Expression.Convert(
            Expression.Call(EntityMaterializer.ReadMethod, _reader, Expression.Constant(start), Expression.Constant(entity),
                Expression.Constant(columns), Expression.Constant(!topLevel)),
            type);
    }

    private Expression ShapeScalar(Expression body, string? memberName)
    {
        var sql = translator.Translate(body);
        var converter = (sql as ColumnExpr)?.Column?.Converter;
        var ordinal = AddColumn(sql, memberName is null ? null : naming.Convert(memberName), memberName);
        return Expression.Call(ValueReader.ReadMethod.MakeGenericMethod(body.Type), _reader, Expression.Constant(ordinal),
            Expression.Constant(converter, typeof(IValueConverter)));
    }

    // 返回列序号；源列与别名同名时不写 AS
    private int AddColumn(SqlExpr sql, string? alias, string? memberName)
    {
        var columnName = (sql as ColumnExpr)?.Name;
        if (alias == columnName) alias = null;
        _columns.Add(new SelectColumn(sql, alias));
        if (memberName is not null && (alias ?? columnName) is { } output) MemberColumns.TryAdd(memberName, output);
        return _columns.Count - 1;
    }

    private static string?[] MemberNames(NewExpression n)
    {
        if (n.Members is not null) return n.Members.Select(m => m.Name).ToArray<string?>();
        // record 主构造函数：参数名首字母大写即属性名
        return n.Constructor!.GetParameters().Select(p => p.Name is null ? null : char.ToUpperInvariant(p.Name[0]) + p.Name[1..]).ToArray();
    }

    private static Expression Coerce(Expression e, Type type) => e.Type == type ? e : Expression.Convert(e, type);
}
