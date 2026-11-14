using System.Linq.Expressions;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Translation;

/// <summary>
/// C# 的 NULL 语义（默认；<see cref="EasyDbOptions.UseRelationalNulls"/> 为 true 时按 SQL 原样，与 Java 一致）。
/// SQL 里与 NULL 比较得到 UNKNOWN，行被丢掉；C# 里 <c>null != 30</c> 成立，<c>null &gt; 30</c> 不成立、取反后成立，两个 null 相等，
/// 拼接时 null 是空串。只在结果会不同的地方补 IS NULL 判断：正向的 <c>=</c>、<c>&gt;</c> 等不用补（UNKNOWN 与 false 一样被丢掉）。
/// </summary>
internal sealed partial class ExpressionTranslator
{
    private bool CSharpNulls => Context?.Db?.Options.UseRelationalNulls != true;

    /// <summary>C# 的 <c>!e</c>：AND / OR 按德摩根律往里推，比较取反时可能为 NULL 的一边补上 IS NULL（取反后成立）。</summary>
    private SqlExpr TranslateNegated(Expression e)
    {
        if (CSharpNulls)
        {
            switch (e)
            {
                case BinaryExpression { NodeType: ExpressionType.AndAlso or ExpressionType.And } b when IsBool(b.Left.Type):
                    return LogicalExpr.Or([TranslateNegated(b.Left), TranslateNegated(b.Right)]);
                case BinaryExpression { NodeType: ExpressionType.OrElse or ExpressionType.Or } b when IsBool(b.Left.Type):
                    return LogicalExpr.And([TranslateNegated(b.Left), TranslateNegated(b.Right)]);
                case UnaryExpression { NodeType: ExpressionType.Not } u when IsBool(u.Type):
                    return TranslatePredicate(u.Operand);
                case BinaryExpression { NodeType: ExpressionType.Equal or ExpressionType.NotEqual } b:
                    return TranslateEquality(b, invert: true);
                case BinaryExpression { NodeType: ExpressionType.LessThan or ExpressionType.LessThanOrEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual } b:
                {
                    var negated = Negate(TranslatePredicate(b));
                    var nulls = new List<SqlExpr>();
                    foreach (var side in (Expression[])[b.Left, b.Right])
                        if (MayBeNull(side) && Translate(side) is var sql && MayBeNull(sql)) nulls.Add(new IsNullExpr(sql, negated: false));
                    return nulls.Count == 0 ? negated : LogicalExpr.Or([negated, .. nulls]);
                }
                case MethodCallExpression { Method.Name: "Contains" } call when TryTranslateCollectionContains(call, out var notIn, negated: true):
                    return notIn;
            }
        }
        var operand = Translate(e);
        return IsPredicate(operand) ? Negate(operand) : new FragmentExpr("(NOT ({0}))", [operand]);
    }

    /// <summary>
    /// <c>a == b</c> / <c>a != b</c>（两边都不是 null 常量）：只有一边可能为 NULL 时 <c>!=</c> 补上它的 IS NULL；
    /// 两边都可能为 NULL 时 <c>==</c> 补上"都是 NULL"，<c>!=</c> 写成"不等或恰有一边是 NULL"。
    /// </summary>
    private SqlExpr EqualityWithNulls(Expression leftExpression, SqlExpr left, Expression rightExpression, SqlExpr right, bool negated)
    {
        var comparison = new BinaryExpr(AsValue(left), negated ? SqlBinaryOperator.NotEqual : SqlBinaryOperator.Equal, AsValue(right));
        if (!CSharpNulls) return comparison;
        var leftNull = MayBeNull(leftExpression) && MayBeNull(left);
        var rightNull = MayBeNull(rightExpression) && MayBeNull(right);
        if (!leftNull && !rightNull) return comparison;
        if (!negated)
            return leftNull && rightNull ? LogicalExpr.Or([comparison, LogicalExpr.Grouped([IsNull(left), IsNull(right)])]) : comparison;
        if (!(leftNull && rightNull)) return LogicalExpr.Or([comparison, IsNull(leftNull ? left : right)]);
        return LogicalExpr.Grouped([
            LogicalExpr.Or([comparison, IsNull(left), IsNull(right)]),
            LogicalExpr.Or([new IsNullExpr(left, negated: true), new IsNullExpr(right, negated: true)]),
        ]);

        static IsNullExpr IsNull(SqlExpr e) => new(e, negated: false);
    }

    // x => !p(x)
    private static LambdaExpression NegatedLambda(LambdaExpression predicate) => Expression.Lambda(predicate.Type, Expression.Not(predicate.Body), predicate.Parameters);

    /// <summary>
    /// 拼接的一项：C# 的拼接把 null 当空串，可能为 NULL 的项包一层 <see cref="SqlFunctionNames.NullAsEmpty"/>（拼接遇到 NULL 得 NULL 的库补成空串）。
    /// 不是字符串的项先转成字符串：<c>COALESCE(数字, '')</c> 在 HANA、Db2 上会把 <c>''</c> 当数字。
    /// </summary>
    internal SqlExpr ConcatPart(Expression part)
    {
        var sql = Translate(part);
        if (!CSharpNulls || !MayBeNull(sql)) return sql;
        return new FunctionExpr(SqlFunctionNames.NullAsEmpty, [StripConvert(part).Type == typeof(string) ? sql : new CastExpr(sql, typeof(string))]);
    }

    /// <summary>C# 的 <c>x.ToString()</c> 在 x 是没有值的可空类型时是空串。</summary>
    private SqlExpr NullableToString(Expression value, SqlExpr text) =>
        CSharpNulls && Nullable.GetUnderlyingType(value.Type) is not null && MayBeNull(text)
            ? new FunctionExpr(SqlFunctionNames.NullOrDefault, [text, new LiteralExpr("''")])
            : text;

    // 按 C# 的类型：不可空的值类型不会是 null（是 null 时 C# 里也会抛异常）
    private static bool MayBeNull(Expression e) => !e.Type.IsValueType || Nullable.GetUnderlyingType(e.Type) is not null;

    /// <summary>翻译后的表达式在 SQL 里可能是 NULL：列看声明的可空，参数看值，函数与运算看参数；认不出的当作可能。</summary>
    internal static bool MayBeNull(SqlExpr sql) => sql switch
    {
        ColumnExpr { Column: { } column } => column.MayBeNull,
        ParameterExpr p => p.Value is null,
        LiteralExpr l => l.Sql == LiteralExpr.Null.Sql,
        ComputedExpr c => MayBeNull(c.Inner),
        IsNullExpr or ExistsExpr or InExpr or LikeExpr or BlankCheckExpr => false,
        FunctionExpr { Name: SqlFunctionNames.Concat or SqlFunctionNames.Count or SqlFunctionNames.CountDistinct or SqlFunctionNames.Now or SqlFunctionNames.UtcNow or SqlFunctionNames.Random } => false,
        FunctionExpr { Name: SqlFunctionNames.NullOrDefault } f => MayBeNull(f.Arguments[^1]),
        FunctionExpr { Name: SqlFunctionNames.NullAsEmpty } => false,
        FunctionExpr { Name: SqlFunctionNames.Length or SqlFunctionNames.Lower or SqlFunctionNames.Upper or SqlFunctionNames.Trim or SqlFunctionNames.TrimStart or SqlFunctionNames.TrimEnd
            or SqlFunctionNames.Replace or SqlFunctionNames.SubString or SqlFunctionNames.Abs or SqlFunctionNames.Round or SqlFunctionNames.Floor or SqlFunctionNames.Ceiling
            or SqlFunctionNames.Sign or SqlFunctionNames.Truncate or SqlFunctionNames.IntegerDivide } f => f.Arguments.Any(MayBeNull),
        BinaryExpr { IsComparison: false } b => MayBeNull(b.Left) || MayBeNull(b.Right),
        CastExpr c => MayBeNull(c.Operand),
        CaseExpr c => c.Branches.Any(b => MayBeNull(b.Then)) || c.Else is null || MayBeNull(c.Else),
        _ => true,
    };
}
