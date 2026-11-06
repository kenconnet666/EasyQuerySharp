using System.Globalization;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

internal sealed partial class ExpressionTranslator
{
    /// <summary>
    /// 集合导航子查询的构造状态：链上的 <c>Where</c> / <c>Select</c> / <c>Distinct</c> / 排序 / <c>Skip</c> / <c>Take</c> 依次作用上来，
    /// 最后由调用方加上 EXISTS、聚合或字符串聚合的列。
    /// 取了一段行（<c>Skip</c> / <c>Take</c>，Java 的 <c>elements(i, j)</c>）之后再加条件或聚合时，先把这段行包成派生表
    /// <c>(SELECT 列… FROM 目标 WHERE 关联 ORDER BY … LIMIT n) tN</c>，条件、导航和聚合作用在派生表上。
    /// Java 直接在这段行上聚合时不包，LIMIT 作用在聚合出的一行上、聚合的是全部行，这里按 C# 的语义先取段（ROADMAP 已定的取舍）。
    /// </summary>
    private sealed class CollectionSubQuery(ExpressionTranslator outer, SelectQuery query, ExpressionTranslator translator, EntityBinding element)
    {
        private readonly EntityMetadata _entity = element.EntityTable.Entity;
        private bool _limited;
        private readonly List<(LambdaExpression Key, bool Descending)> _orderKeys = [];

        /// <summary>当前的子查询（包过派生表后是外面那层）。</summary>
        public SelectQuery Query { get; private set; } = query;

        /// <summary>翻译子查询里的表达式用的翻译器（作用域里有外层的参数）。</summary>
        public ExpressionTranslator Translator { get; private set; } = translator;

        /// <summary>集合元素的绑定：目标表，或包过之后的派生表。</summary>
        public SourceBinding Element { get; private set; } = element;

        /// <summary>链上的 <c>Select</c>。</summary>
        public LambdaExpression? Selector { get; private set; }

        /// <summary>链上有 <c>Distinct</c>。</summary>
        public bool Distinct { get; private set; }

        /// <summary>
        /// 元素的顺序（字符串聚合按它拼接）：没包过时是子查询的 ORDER BY；包过派生表后是同样的排序键作用在派生表上
        /// （派生表里的 ORDER BY 只决定取哪段行，不保证外层的顺序）。
        /// </summary>
        public List<OrderByItem> ElementOrder { get; private set; } = query.OrderBy;

        /// <summary>作用链上的一个方法。</summary>
        public void Apply(MethodCallExpression op)
        {
            switch (op.Method.Name)
            {
                case nameof(Enumerable.Where):
                    // 先求条件：取过一段行时它会把 Query 换成外面包的那层
                    if (Predicate(Lambda(op.Arguments[1])) is { } condition) Query.Where.Add(condition);
                    break;
                case nameof(Enumerable.Select):
                    Selector = Lambda(op.Arguments[1]);
                    break;
                case nameof(Enumerable.Distinct):
                    Distinct = true;
                    break;
                case nameof(Enumerable.OrderBy) or nameof(Enumerable.ThenBy) or nameof(Enumerable.OrderByDescending) or nameof(Enumerable.ThenByDescending):
                    if (_limited) throw Unsupported(op, "取了一段行之后不能再排序");
                    var key = Lambda(op.Arguments[1]);
                    var descending = op.Method.Name.EndsWith("Descending", System.StringComparison.Ordinal);
                    Translator.Bind(key, [Element]);
                    Query.OrderBy.Add(Translator.OrderItem(key.Body, descending));
                    _orderKeys.Add((key, descending));
                    break;
                case nameof(Enumerable.Skip):
                    Query.Offset = Count(op);
                    _limited = true;
                    break;
                case nameof(Enumerable.Take):
                    Query.Limit = Count(op);
                    _limited = true;
                    break;
                default:
                    throw Unsupported(op, "集合导航上不支持这个方法");
            }
        }

        /// <summary>
        /// 元素上的条件；取过一段行时先包成派生表。<paramref name="filtered"/> 时按传播下来的值过滤改写，整个去掉时返回 null。
        /// </summary>
        public SqlExpr? Predicate(LambdaExpression lambda, bool filtered = true)
        {
            if (_limited) WrapLimitedRows();
            var kept = filtered ? Translator.NavigationPredicate(lambda) : lambda;
            return kept is null ? null : Translator.ElementPredicate(kept, Element);
        }

        /// <summary>要在元素上聚合：取过一段行时先包成派生表，聚合只算这段行。</summary>
        public void BeforeAggregate()
        {
            if (_limited) WrapLimitedRows();
        }

        // (SELECT 目标的列… FROM 目标 WHERE … ORDER BY … LIMIT n) tN：列不写 AS（与 Java 一致），外层按实体属性找列
        private void WrapLimitedRows()
        {
            var columns = _entity.Columns.Where(c => c.AutoSelect).ToList();
            var source = (EntityBinding)Element;
            foreach (var c in columns) Query.Columns.Add(new SelectColumn(source.EntityTable.Column(c), null));
            var derived = new DerivedTableSource(Query);
            Query = new SelectQuery(derived);
            Translator = new ExpressionTranslator(new Dictionary<ParameterExpression, SourceBinding>(outer._scope)) { Context = outer.Context!.ForSubQuery(Query) };
            Element = new DerivedBinding(derived, columns.ToDictionary(c => c.PropertyName, c => c.Name, System.StringComparer.Ordinal), _entity);
            _limited = false;
            ElementOrder = [.. _orderKeys.Select(k =>
            {
                Translator.Bind(k.Key, [Element]);
                return Translator.OrderItem(k.Key.Body, k.Descending);
            })];
        }

        private static long Count(MethodCallExpression op) =>
            Convert.ToInt64(ClientEvaluator.Evaluate(op.Arguments[1]), CultureInfo.InvariantCulture);
    }
}
