using System.Linq.Expressions;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Querying;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 一条写入语句的翻译作用域：SET 与条件共用一个翻译上下文，集合导航翻译成关联子查询，
/// 对一导航补出的 JOIN 收集起来（别名按出现顺序编号，所以先翻译 SET 再翻译条件，与 Java 一致）。
/// </summary>
internal sealed class WriteScope
{
    private readonly SelectQuery _scratch;
    private readonly TranslationContext _context;

    public WriteScope(EasyDb db, EntityTableSource table, InterceptorSelection interceptors)
    {
        Table = table;
        _scratch = new SelectQuery(table);
        _context = new TranslationContext(db.Metadata,
            (t, logicDelete) => [.. (logicDelete && QueryCompiler.LogicDeleteFilter(db, t) is { } filter ? [filter] : Array.Empty<SqlExpr>()), .. db.Interceptors.QueryFilters(t, interceptors)],
            _scratch) { Db = db };
    }

    /// <summary>写入的目标表。</summary>
    public EntityTableSource Table { get; }

    /// <summary>对一导航连接的表；写入里都是 INNER JOIN（与 Java 一致）。</summary>
    public IEnumerable<JoinClause> Joins => _scratch.Joins.Select(j => new JoinClause(JoinKind.Inner, j.Table, j.On));

    /// <summary>翻译参数为目标实体的 lambda（条件或值）。</summary>
    public SqlExpr Translate(LambdaExpression lambda, bool predicate)
    {
        var translator = new ExpressionTranslator([]) { Context = _context };
        translator.Bind(lambda, [new EntityBinding(Table)]);
        return predicate ? translator.TranslatePredicate(lambda.Body) : translator.Translate(lambda.Body);
    }
}
