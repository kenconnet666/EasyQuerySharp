using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Metadata;

/// <summary>
/// 逻辑删除策略（对应 Java 的 LogicDeleteStrategy）：决定"未删除"的条件，和删除时要写哪些列。
/// 内置布尔与删除时间两种；其余实现后用 <see cref="EasyDbOptions.AddLogicDeleteStrategy"/> 按名字注册，
/// 实体上写 <c>[LogicDelete(LogicDeleteStrategy.Custom, StrategyName = "名字")]</c>。
/// </summary>
/// <example><code>
/// // 未删除：deleted_at IS NULL；删除时写删除时间与删除人
/// sealed class DeletedByStrategy : ILogicDeleteStrategy
/// {
///     public SqlExpr NotDeleted(ColumnExpr column) =&gt; new IsNullExpr(column, negated: false);
///     public IEnumerable&lt;(ColumnMetadata, object?)&gt; DeletedValues(EntityMetadata entity, ColumnMetadata column) =&gt;
///         [(column, DateTime.Now), (entity.GetColumn("DeletedUser"), CurrentUser.Id)];
/// }
/// </code></example>
public interface ILogicDeleteStrategy
{
    /// <summary>"未删除"的条件；<paramref name="column"/> 是带表别名的逻辑删除列。</summary>
    SqlExpr NotDeleted(ColumnExpr column);

    /// <summary>删除时 SET 的列和值。每次删除时调用，可以取当前时间、当前用户等动态值。</summary>
    IEnumerable<(ColumnMetadata Column, object? Value)> DeletedValues(EntityMetadata entity, ColumnMetadata column);
}

/// <summary>内置策略与按实体解析策略。</summary>
internal static class LogicDeleteStrategies
{
    /// <summary>实体用的策略：内置的直接取，<see cref="LogicDeleteStrategy.Custom"/> 按名字从注册表取。</summary>
    public static ILogicDeleteStrategy Resolve(EasyDbOptions options, EntityMetadata entity) => entity.LogicDelete!.Strategy switch
    {
        LogicDeleteStrategy.Boolean => BooleanStrategy.Instance,
        LogicDeleteStrategy.DeletedTime => DeletedTimeStrategy.Instance,
        _ => entity.LogicDelete.StrategyName is { } name && options.LogicDeleteStrategies.TryGetValue(name, out var custom)
            ? custom
            : throw new InvalidOperationException(
                $"{entity.Type.Name} 的逻辑删除策略 '{entity.LogicDelete.StrategyName}' 没有注册，请调用 EasyDbOptions.AddLogicDeleteStrategy"),
    };

    // 布尔列：未删除为 false，删除时置为 true
    private sealed class BooleanStrategy : ILogicDeleteStrategy
    {
        public static readonly BooleanStrategy Instance = new();

        public SqlExpr NotDeleted(ColumnExpr column) => new BinaryExpr(column, SqlBinaryOperator.Equal, new ParameterExpr(false, typeof(bool)));

        public IEnumerable<(ColumnMetadata Column, object? Value)> DeletedValues(EntityMetadata entity, ColumnMetadata column) => [(column, true)];
    }

    // 删除时间：未删除为 NULL，删除时写当前时间
    private sealed class DeletedTimeStrategy : ILogicDeleteStrategy
    {
        public static readonly DeletedTimeStrategy Instance = new();

        public SqlExpr NotDeleted(ColumnExpr column) => new IsNullExpr(column, negated: false);

        public IEnumerable<(ColumnMetadata Column, object? Value)> DeletedValues(EntityMetadata entity, ColumnMetadata column) => [(column, DateTime.Now)];
    }
}
