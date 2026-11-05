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

    public WriteScope(EasyDb db, EntityTableSource table, InterceptorSelection interceptors, bool groupJoinAll = false)
    {
        Table = table;
        _scratch = new SelectQuery(table);
        _context = new TranslationContext(db.Metadata,
            (t, logicDelete) => QueryCompiler.TableFilters(db, t, logicDelete, interceptors),
            _scratch) { Db = db, GroupJoinAll = groupJoinAll };
    }

    /// <summary>写入的目标表。</summary>
    public EntityTableSource Table { get; }

    /// <summary>
    /// 翻译中连接的表：对一导航在写入里都是 INNER JOIN（与 Java 一致），集合导航合并成的分组表保持 LEFT JOIN。
    /// 在全部 SET / 条件翻译完之后取（分组表的列此时才确定）。
    /// </summary>
    public IEnumerable<JoinClause> Joins
    {
        get
        {
            _context.CompleteDerivedJoins();
            return _scratch.Joins.Select(j => j.Table is DerivedTableSource ? j : new JoinClause(JoinKind.Inner, j.Table, j.On));
        }
    }

    /// <summary>翻译参数为目标实体的 lambda（条件或值）。</summary>
    public SqlExpr Translate(LambdaExpression lambda, bool predicate)
    {
        var translator = new ExpressionTranslator([]) { Context = _context };
        translator.Bind(lambda, [new EntityBinding(Table)]);
        return predicate ? translator.TranslatePredicate(lambda.Body) : translator.Translate(lambda.Body);
    }
}
