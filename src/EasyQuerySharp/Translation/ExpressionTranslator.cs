using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>
/// 把查询 lambda 的表达式树翻译成 SQL AST。lambda 参数通过作用域绑定到表来源；不依赖参数的子树在客户端求值成参数。
/// 不支持的写法抛出 <see cref="NotSupportedException"/>，消息里带原始表达式。
/// </summary>
internal sealed partial class ExpressionTranslator
{
    private readonly Dictionary<ParameterExpression, SourceBinding> _scope;

    public ExpressionTranslator(Dictionary<ParameterExpression, SourceBinding> scope) => _scope = scope;

    /// <summary>作用域：lambda 参数 → 表来源。</summary>
    public IReadOnlyDictionary<ParameterExpression, SourceBinding> Scope => _scope;

    /// <summary>把 lambda 的各参数依次绑定到表来源。</summary>
    public void Bind(LambdaExpression lambda, IReadOnlyList<SourceBinding> sources)
    {
        // 分组之后的 lambda（Having / OrderBy / Select）只有一个 IGroup 参数，绑定到分组
        if (Group is not null && lambda.Parameters.Count == 1 && IsGroupType(lambda.Parameters[0].Type))
        {
            _scope[lambda.Parameters[0]] = Group;
            return;
        }
        if (lambda.Parameters.Count > sources.Count)
            throw new InvalidOperationException($"lambda 有 {lambda.Parameters.Count} 个参数，但只有 {sources.Count} 个表来源：{lambda}");
        for (var i = 0; i < lambda.Parameters.Count; i++) _scope[lambda.Parameters[i]] = sources[i];
    }

    /// <summary>翻译成条件（布尔值的列、方法等补成 <c>= true</c>）。</summary>
    public SqlExpr TranslatePredicate(Expression expression)
    {
        var sql = Translate(expression);
        return IsPredicate(sql) ? sql : new BinaryExpr(sql, SqlBinaryOperator.Equal, new ParameterExpr(true, typeof(bool)));
    }

    /// <summary>翻译成值表达式。</summary>
    public SqlExpr Translate(Expression expression)
    {
        if (!ClientEvaluator.MustTranslate(expression)) return Parameter(ClientEvaluator.Evaluate(expression), expression.Type);

        return expression switch
        {
            MemberExpression m => TranslateMember(m),
            UnaryExpression u => TranslateUnary(u),
            BinaryExpression b => TranslateBinary(b),
            ConditionalExpression c => TranslateConditional(c),
            MethodCallExpression call => TranslateCall(call),
            LambdaExpression l => Translate(l.Body),
            _ => throw Unsupported(expression),
        };
    }

    /// <summary>计算列：表达式的参数绑定到所在的表，就地展开。</summary>
    public SqlExpr TranslateComputed(LambdaExpression computed, SourceBinding owner)
    {
        var parameter = computed.Parameters[0];
        var previous = _scope.GetValueOrDefault(parameter);
        _scope[parameter] = owner;
        try
        {
            return Translate(computed.Body);
        }
        finally
        {
            if (previous is null) _scope.Remove(parameter);
            else _scope[parameter] = previous;
        }
    }

    /// <summary>参数绑定的表来源；表达式不是绑定参数时返回 null。</summary>
    public SourceBinding? BindingOf(Expression expression) => StripConvert(expression) switch
    {
        ParameterExpression p => _scope.GetValueOrDefault(p),
        // 多表元组的 e.Item2 → 第二个表来源
        MemberExpression { Expression: { } inner } m when BindingOf(inner) is TupleBinding tuple => tuple.Item(m.Member.Name),
        // 派生表里嵌着的整个实体 x.b → 只含它那些列的派生绑定
        MemberExpression { Expression: { } inner } m when BindingOf(inner) is DerivedBinding derived && derived.Nested(m.Member.Name) is { } nested => nested,
        // 对一导航 x.User → 隐式 JOIN 的目标表
        MemberExpression m => ToOneNavigation(m),
        // 集合导航里的某一行 x.Cards.First() → 派生表
        MethodCallExpression call => ElementBinding(call),
        _ => null,
    };

    /// <summary>构造参数：枚举转成底层整数，可空类型取底层类型。</summary>
    public static ParameterExpr Parameter(object? value, Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t.IsEnum)
        {
            t = Enum.GetUnderlyingType(t);
            if (value is not null) value = Convert.ChangeType(value, t, System.Globalization.CultureInfo.InvariantCulture);
        }
        return new ParameterExpr(value, value?.GetType() ?? t);
    }

    /// <summary>列的数据库值（已经过值转换器）作为参数，类型取列的数据库类型。</summary>
    internal static ParameterExpr Parameter(ColumnMetadata column, object? providerValue) =>
        Parameter(providerValue, column.Converter?.ProviderType ?? column.ClrType);

    /// <summary>属性类型的值（还没经过值转换器）作为这一列的参数。</summary>
    internal static ParameterExpr ParameterFromModel(ColumnMetadata column, object? value) =>
        Parameter(column, column.Converter is null ? value : column.Converter.ToProvider(value));

    /// <summary>直接的属性访问 <c>t =&gt; t.Title</c>（可以有类型转换）指向的成员；不是属性访问时抛出带上下文的异常。</summary>
    internal static System.Reflection.MemberInfo MemberOf(LambdaExpression selector) =>
        StripConvert(selector.Body) is MemberExpression m
            ? m.Member
            : throw new ArgumentException($"需要直接的属性访问，如 t => t.Title：{selector}", nameof(selector));

    /// <summary>列清单 <c>t =&gt; t.A</c> 或 <c>t =&gt; new { t.A, t.B }</c> 里的属性名，按书写顺序、去重。</summary>
    internal static List<string> MemberNames(LambdaExpression selector)
    {
        var body = StripConvert(selector.Body);
        IEnumerable<Expression> parts = body is NewExpression n ? n.Arguments : [body];
        return parts.Select(p => StripConvert(p) is MemberExpression m
                ? m.Member.Name
                : throw new ArgumentException($"列清单只能写属性访问，如 t => new {{ t.Id, t.Title }}：{selector}", nameof(selector)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    internal static Expression StripConvert(Expression e)
    {
        while (e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.Quote } u) e = u.Operand;
        return e;
    }

    internal static NotSupportedException Unsupported(Expression e, string? reason = null) =>
        new($"不支持翻译成 SQL 的表达式：{e}{(reason is null ? "" : "（" + reason + "）")}");

    private SqlExpr TranslateMember(MemberExpression m)
    {
        if (TryTranslateGroupKey(m) is { } key) return key;
        if (TryTranslateCollectionCount(m) is { } count) return count;
        if (m.Expression is not null && BindingOf(m.Expression) is EntityBinding entity && entity.Entity.FindColumn(m.Member.Name) is { Computed: { } computed })
            return TranslateComputed(computed, entity);
        if (m.Expression is not null && BindingOf(m.Expression) is { } binding and not GroupBinding and not TupleBinding)
            return binding.Member(m.Member) ?? throw Unsupported(m, $"{m.Member.DeclaringType?.Name}.{m.Member.Name} 不是映射到列的成员");

        // 可空类型：x.Value 即 x，x.HasValue 即 x IS NOT NULL
        if (m.Expression is not null && Nullable.GetUnderlyingType(m.Expression.Type) is not null)
        {
            if (m.Member.Name == "Value") return Translate(m.Expression);
            if (m.Member.Name == "HasValue") return new IsNullExpr(Translate(m.Expression), negated: true);
        }
        if (MethodTranslators.TryTranslateMember(this, m, out var member)) return member;

        throw Unsupported(m);
    }

    private SqlExpr TranslateUnary(UnaryExpression u)
    {
        switch (u.NodeType)
        {
            case ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.Quote or ExpressionType.TypeAs:
                return Translate(u.Operand);
            case ExpressionType.Not when u.Type == typeof(bool) || u.Type == typeof(bool?):
            {
                // 分组合并的 !nav.Any(…) 是单独的 None 列（与 Java 的 none() 一致）
                if (StripConvert(u.Operand) is MethodCallExpression call && TryTranslateCollection(call, out var none, negated: true)) return none;
                // 条件取反；布尔值（列等）取反是值 (NOT (x))，与 Java 的 not() 一致，用在 WHERE 里时再补 = true
                var operand = Translate(u.Operand);
                return IsPredicate(operand) ? Negate(operand) : new FragmentExpr("(NOT ({0}))", [operand]);
            }
            case ExpressionType.Negate or ExpressionType.NegateChecked:
                return new FragmentExpr("-{0}", [Translate(u.Operand)]);
            default:
                throw Unsupported(u);
        }
    }

    /// <summary>取反：能直接改成否定形式的（NOT IN、NOT LIKE、IS NOT NULL、NOT EXISTS、&lt;&gt;）就改，其余包一层 NOT。</summary>
    internal static SqlExpr Negate(SqlExpr predicate) => predicate switch
    {
        InExpr { SubQuery: { } sub } i => new InExpr(i.Operand, sub, !i.Negated),
        InExpr i => new InExpr(i.Operand, i.Values!, !i.Negated),
        LikeExpr l => l.Negate(),
        BlankCheckExpr b => b.Negate(),
        IsNullExpr n => new IsNullExpr(n.Operand, !n.Negated),
        ExistsExpr { FromNavigation: true } e => new NotExpr(e),
        ExistsExpr e => new ExistsExpr(e.Query, !e.Negated),
        NotExpr n => n.Operand,
        _ => new NotExpr(predicate),
    };

    private SqlExpr TranslateBinary(BinaryExpression b)
    {
        switch (b.NodeType)
        {
            case ExpressionType.AndAlso or ExpressionType.And when IsBool(b.Left.Type):
                return LogicalExpr.And([TranslatePredicate(b.Left), TranslatePredicate(b.Right)]);
            case ExpressionType.OrElse or ExpressionType.Or when IsBool(b.Left.Type):
                return LogicalExpr.Or([TranslatePredicate(b.Left), TranslatePredicate(b.Right)]);
            case ExpressionType.Coalesce:
                return new FunctionExpr(SqlFunctionNames.NullOrDefault, [Translate(b.Left), Translate(b.Right)]);
            case ExpressionType.Equal or ExpressionType.NotEqual:
                return TranslateEquality(b);
        }

        var op = b.NodeType switch
        {
            ExpressionType.LessThan => SqlBinaryOperator.LessThan,
            ExpressionType.LessThanOrEqual => SqlBinaryOperator.LessThanOrEqual,
            ExpressionType.GreaterThan => SqlBinaryOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => SqlBinaryOperator.GreaterThanOrEqual,
            ExpressionType.Add or ExpressionType.AddChecked => SqlBinaryOperator.Add,
            ExpressionType.Subtract or ExpressionType.SubtractChecked => SqlBinaryOperator.Subtract,
            ExpressionType.Multiply or ExpressionType.MultiplyChecked => SqlBinaryOperator.Multiply,
            ExpressionType.Divide => SqlBinaryOperator.Divide,
            ExpressionType.Modulo => SqlBinaryOperator.Modulo,
            _ => throw Unsupported(b),
        };

        // 字符串相加 → 方言的拼接函数
        if (op == SqlBinaryOperator.Add && b.Type == typeof(string))
            return new FunctionExpr(SqlFunctionNames.Concat, [.. Flatten(b, ExpressionType.Add).Select(Translate)]);

        // string.Compare(a, b) > 0 / a.CompareTo(b) <= 0 … → a > b（C# 里字符串只能这样比大小；与 1 / -1 比较时仍按函数翻译）
        if (op is SqlBinaryOperator.LessThan or SqlBinaryOperator.LessThanOrEqual or SqlBinaryOperator.GreaterThan or SqlBinaryOperator.GreaterThanOrEqual
            && StringComparison(b.Left) is var (x, y) && IsZero(b.Right))
        {
            var (l, r) = TranslatePair(x, y);
            return new BinaryExpr(l, op, r);
        }

        var (left, right) = TranslatePair(b.Left, b.Right);
        return new BinaryExpr(left, op, right);
    }

    // a ? x : b ? y : z 合并成一个多分支 CASE（与 Java 的 caseWhen(..).caseWhen(..) 一致）
    private CaseExpr TranslateConditional(ConditionalExpression c)
    {
        var branches = new List<(SqlExpr When, SqlExpr Then)> { (TranslatePredicate(c.Test), Translate(c.IfTrue)) };
        var otherwise = c.IfFalse;
        while (otherwise is ConditionalExpression next)
        {
            branches.Add((TranslatePredicate(next.Test), Translate(next.IfTrue)));
            otherwise = next.IfFalse;
        }
        // ELSE 分支是字面量 null 时写 ELSE NULL（与 Java 的 elseEnd(null) 一致），不做参数
        return new CaseExpr(branches, StripConvert(otherwise) is ConstantExpression { Value: null } ? LiteralExpr.Null : Translate(otherwise));
    }

    // string.Compare(a, b) 或 a.CompareTo(b) 的两个操作数
    private static (Expression, Expression)? StringComparison(Expression e) => StripConvert(e) switch
    {
        MethodCallExpression { Method.Name: nameof(string.Compare), Object: null, Arguments: [var a, var b] } c when c.Method.DeclaringType == typeof(string) => (a, b),
        MethodCallExpression { Method.Name: nameof(string.CompareTo), Object: { } a, Arguments: [var b] } c when c.Method.DeclaringType == typeof(string) => (a, b),
        _ => null,
    };

    private static bool IsZero(Expression e) => !ClientEvaluator.MustTranslate(e) && ClientEvaluator.Evaluate(e) is 0;

    private SqlExpr TranslateEquality(BinaryExpression b)
    {
        var negated = b.NodeType == ExpressionType.NotEqual;
        // x == null → IS NULL（字面量 null 或运行时为 null 的变量都算）；整个实体判空即主键判空（Java 的 t.isNull()）
        if (IsNullValue(b.Right)) return NullCheck(b.Left, negated);
        if (IsNullValue(b.Left)) return NullCheck(b.Right, negated);

        var (left, right) = TranslatePair(b.Left, b.Right);
        return new BinaryExpr(AsValue(left), negated ? SqlBinaryOperator.NotEqual : SqlBinaryOperator.Equal, AsValue(right));
    }

    private SqlExpr NullCheck(Expression operand, bool negated)
    {
        if (BindingOf(operand) is not EntityBinding { Entity.KeyColumns: { Count: > 0 } keys } entity) return new IsNullExpr(Translate(operand), negated);
        var checks = keys.Select(k => (SqlExpr)new IsNullExpr(entity.EntityTable.Column(k), negated)).ToList();
        return checks.Count == 1 ? checks[0] : LogicalExpr.Grouped(checks);
    }

    /// <summary>条件作为值使用（比较的一侧、投影的一列）时整体加括号：<c>(a &lt; ? AND b = ?)</c>（与 Java 的 valueOf 一致）。</summary>
    internal static SqlExpr AsValue(SqlExpr sql) => IsPredicate(sql) ? new FragmentExpr("({0})", [sql]) : sql;

    /// <summary>翻译一对操作数：一侧是带转换器的列时，另一侧的参数值先经过转换器。</summary>
    internal (SqlExpr Left, SqlExpr Right) TranslatePair(Expression leftExpr, Expression rightExpr)
    {
        var left = Translate(leftExpr);
        var right = Translate(rightExpr);
        return (ApplyConverter(left, right), ApplyConverter(right, left));
    }

    /// <summary>若 <paramref name="other"/> 是带转换器的列，把参数 <paramref name="value"/> 转成数据库值。</summary>
    internal static SqlExpr ApplyConverter(SqlExpr value, SqlExpr other) =>
        value is ParameterExpr p && other is ColumnExpr { Column.Converter: { } converter }
            ? new ParameterExpr(converter.ToProvider(p.Value), converter.ProviderType)
            : value;

    private static bool IsNullValue(Expression e) =>
        (!e.Type.IsValueType || Nullable.GetUnderlyingType(e.Type) is not null)
        && !ClientEvaluator.MustTranslate(e)
        && ClientEvaluator.Evaluate(e) is null;

    private static bool IsBool(Type t) => t == typeof(bool) || t == typeof(bool?);

    private static IEnumerable<Expression> Flatten(BinaryExpression b, ExpressionType type)
    {
        foreach (var side in new[] { b.Left, b.Right })
        {
            var s = StripConvert(side);
            if (s is BinaryExpression inner && inner.NodeType == type && inner.Type == b.Type)
                foreach (var x in Flatten(inner, type)) yield return x;
            else yield return side;
        }
    }

    /// <summary>是否已经是条件（比较、逻辑、IN、LIKE、IS NULL、EXISTS、NOT）。</summary>
    internal static bool IsPredicate(SqlExpr e) => e switch
    {
        BinaryExpr b => b.IsComparison,
        LogicalExpr or NotExpr or IsNullExpr or InExpr or LikeExpr or ExistsExpr or BlankCheckExpr => true,
        FragmentExpr { IsPredicate: true } => true,
        LiteralExpr l => l.Sql is "1 = 1" or "1 = 2",
        _ => false,
    };
}
