using System.Linq.Expressions;
using System.Reflection;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 数一个表达式里有几处会让 Java 建表的导航：对一导航（<c>x.User.Name</c>，隐式 JOIN）、集合导航上取某一行
/// （<c>x.Cards.First()</c>，编号派生表），以及合并成分组表的集合导航；其余集合上的聚合是子查询，不算。
/// Java 在构造表达式时就为它们建了表，之后即使这部分没进 SQL（值过滤去掉了条件），主表也已经有了别名 t；C# 用它判断这种情况。
/// </summary>
internal static class NavigationUsage
{
    /// <summary>表达式里会建表的导航出现的次数；<paramref name="groupJoined"/> 判断一个集合导航是否合并成分组表。</summary>
    public static int Count(Expression e, Func<MemberInfo, bool>? groupJoined = null)
    {
        var counter = new Counter(groupJoined);
        counter.Visit(e);
        return counter.Accesses;
    }

    private sealed class Counter(Func<MemberInfo, bool>? groupJoined) : ExpressionVisitor
    {
        public int Accesses { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression
                && (IsEntity(node.Type) || (IsEntityCollection(node.Type) && groupJoined?.Invoke(node.Member) == true)))
                Accesses++;
            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Enumerable)
                && node.Method.Name is nameof(Enumerable.First) or nameof(Enumerable.FirstOrDefault) or nameof(Enumerable.ElementAt) or nameof(Enumerable.ElementAtOrDefault)
                && Root(node.Arguments[0]) is MemberExpression { Expression: ParameterExpression } collection && IsEntityCollection(collection.Type))
                Accesses++;
            return base.VisitMethodCall(node);
        }

        // 沿 Where / OrderBy … 链回到集合本身
        private static Expression Root(Expression e)
        {
            while (e is MethodCallExpression { Arguments.Count: > 0 } call && call.Method.DeclaringType == typeof(Enumerable)) e = call.Arguments[0];
            return e;
        }

        private static bool IsEntity(Type type) => type != typeof(string) && type.IsClass && !type.IsArray && !type.IsGenericType;

        private static bool IsEntityCollection(Type type) =>
            type.GetInterfaces().Append(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0] is { } element
            && element != typeof(string) && element.IsClass;
    }
}
