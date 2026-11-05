using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

namespace EasyQuerySharp.Materialization;

/// <summary>
/// 读取函数（DbDataReader → 结果）的编译缓存。编译表达式树是一条查询里最贵的一步（约 1 ms，翻译渲染只要十几 µs），
/// 而同一个查询形状每次生成的读取树结构都一样，只是挂着的对象不同（带出计划、换算委托等）。
/// 做法：按树的结构算一个键，把引用类型的常量提成参数数组，同样形状只编译一次，之后每次只传入这次的常量。
/// 数值、字符串等常量（列序号等）留在键里、编进代码，读行时不多一次装箱拆箱。
/// </summary>
internal static class ShaperCache
{
    private static readonly ConcurrentDictionary<string, Delegate> Cache = new(StringComparer.Ordinal);

    /// <summary>已缓存的形状数（测试与诊断用）。</summary>
    public static int Count => Cache.Count;

    /// <summary>取（必要时编译）<paramref name="body"/> 的读取函数；树里有不认识的节点时不缓存，直接编译。</summary>
    public static Func<DbDataReader, T> Get<T>(Expression body, ParameterExpression reader)
    {
        var lifter = new ConstantLifter(reader);
        var rewritten = lifter.Visit(body)!;
        if (lifter.Unsupported) return Expression.Lambda<Func<DbDataReader, T>>(body, reader).Compile();

        var key = typeof(T).TypeHandle.Value + "|" + lifter.Key;
        var compiled = (Func<object?[], DbDataReader, T>)Cache.GetOrAdd(key,
            _ => Expression.Lambda<Func<object?[], DbDataReader, T>>(rewritten, lifter.ConstantsParameter, reader).Compile());
        var constants = lifter.Constants.ToArray();
        return r => compiled(constants, r);
    }

    /// <summary>边遍历边写结构键，并把引用类型常量换成 <c>constants[i]</c>。</summary>
    private sealed class ConstantLifter : ExpressionVisitor
    {
        private readonly StringBuilder _key = new();
        private readonly Dictionary<ParameterExpression, int> _parameters = [];

        public ConstantLifter(ParameterExpression reader) => _parameters[reader] = 0;

        public ParameterExpression ConstantsParameter { get; } = Expression.Parameter(typeof(object?[]), "constants");

        public List<object?> Constants { get; } = [];

        public string Key => _key.ToString();

        /// <summary>遇到了键里表达不了的节点（goto、try 等，读取树里不会出现），这棵树不缓存。</summary>
        public bool Unsupported { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                _key.Append('~');
                return null;
            }
            _key.Append('(').Append((int)node.NodeType).Append(':').Append(node.Type.TypeHandle.Value);
            var result = base.Visit(node);
            _key.Append(')');
            return result;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is null) return node;
            if (IsInlineConstant(node.Type))
            {
                // 带上长度，字符串值里的括号不会和结构混淆
                var text = Convert.ToString(node.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                _key.Append('=').Append(text.Length).Append(':').Append(text);
                return node;
            }
            _key.Append('#').Append(Constants.Count);
            var lifted = Expression.ArrayIndex(ConstantsParameter, Expression.Constant(Constants.Count));
            Constants.Add(node.Value);
            return Expression.Convert(lifted, node.Type);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_parameters.TryGetValue(node, out var index)) _parameters[node] = index = _parameters.Count;
            _key.Append('p').Append(index);
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            AppendMember(node.Member);
            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            AppendMethod(node.Method);
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitNew(NewExpression node)
        {
            if (node.Constructor is not null) AppendMethod(node.Constructor);
            foreach (var m in node.Members ?? []) AppendMember(m);
            return base.VisitNew(node);
        }

        protected override MemberBinding VisitMemberBinding(MemberBinding node)
        {
            _key.Append('b').Append((int)node.BindingType);
            AppendMember(node.Member);
            return base.VisitMemberBinding(node);
        }

        protected override ElementInit VisitElementInit(ElementInit node)
        {
            AppendMethod(node.AddMethod);
            return base.VisitElementInit(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.Method is not null) AppendMethod(node.Method);
            return base.VisitUnary(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.Method is not null) AppendMethod(node.Method);
            if (node.IsLiftedToNull) _key.Append('L');
            return base.VisitBinary(node);
        }

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            _key.Append('t').Append(node.TypeOperand.TypeHandle.Value);
            return base.VisitTypeBinary(node);
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            if (node.Indexer is not null) AppendMember(node.Indexer);
            return base.VisitIndex(node);
        }

        protected override Expression VisitGoto(GotoExpression node) => Reject(node);

        protected override Expression VisitLabel(LabelExpression node) => Reject(node);

        protected override Expression VisitLoop(LoopExpression node) => Reject(node);

        protected override Expression VisitSwitch(SwitchExpression node) => Reject(node);

        protected override Expression VisitTry(TryExpression node) => Reject(node);

        protected override Expression VisitExtension(Expression node) => Reject(node);

        private Expression Reject(Expression node)
        {
            Unsupported = true;
            return node;
        }

        private void AppendMember(System.Reflection.MemberInfo member) =>
            _key.Append('m').Append(member.Module.ModuleVersionId).Append(member.MetadataToken).Append('@').Append(member.DeclaringType?.TypeHandle.Value);

        private void AppendMethod(System.Reflection.MethodBase method) =>
            _key.Append('f').Append(method.MethodHandle.Value).Append('@').Append(method.DeclaringType?.TypeHandle.Value);

        // 可以原样编进代码、值本身就能写进键的常量
        private static bool IsInlineConstant(Type type) =>
            type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }
}
