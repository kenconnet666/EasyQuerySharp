using System.Linq.Expressions;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 数一个表达式里用到了几次导航（参数上实体类型的成员，如 <c>x.User.Name</c>、<c>x.Cards.Count()</c>）。
/// Java 在构造表达式时就为导航建了表，之后即使这部分没进 SQL（值过滤去掉了条件、计数查询去掉了排序），
/// 主表也已经有了别名 t；C# 用它判断这种情况。
/// </summary>
internal static class NavigationUsage
{
    /// <summary>表达式里"参数.实体成员"出现的次数。</summary>
    public static int Count(Expression e)
    {
        var counter = new Counter();
        counter.Visit(e);
        return counter.Accesses;
    }

    // 成员的类型是实体（非字符串的类），或元素是实体的集合
    private sealed class Counter : ExpressionVisitor
    {
        public int Accesses { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression && IsEntityLike(node.Type)) Accesses++;
            return base.VisitMember(node);
        }

        private static bool IsEntityLike(Type type)
        {
            if (type == typeof(string) || !type.IsClass) return false;
            if (!type.IsGenericType) return !type.IsArray;
            var element = type.GetInterfaces().Append(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
            return element is not null && element != typeof(string) && element.IsClass;
        }
    }
}
