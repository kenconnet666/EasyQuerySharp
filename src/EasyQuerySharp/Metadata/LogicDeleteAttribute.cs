namespace EasyQuerySharp.Metadata;

/// <summary>逻辑删除的取值方式。</summary>
public enum LogicDeleteStrategy
{
    /// <summary>布尔列：未删除为 false，删除时置为 true。</summary>
    Boolean,

    /// <summary>删除时间：未删除为 NULL，删除时写当前时间。</summary>
    DeletedTime,

    /// <summary>删除时间戳：未删除为 0，删除时写当前的 Unix 毫秒数（对应 Java 的 DELETE_LONG_TIMESTAMP，列是整数）。</summary>
    DeletedTimestamp,

    /// <summary>自定义：由 <see cref="LogicDeleteAttribute.StrategyName"/> 指定的已注册策略决定。</summary>
    Custom,
}

/// <summary>
/// 把属性标为逻辑删除列：查询自动加"未删除"条件，删除改写成更新。可在单条查询上用 <c>DisableLogicDelete()</c> 关闭。
/// </summary>
/// <example><code>[LogicDelete(LogicDeleteStrategy.Boolean)] public bool? Deleted { get; set; }</code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LogicDeleteAttribute(LogicDeleteStrategy strategy = LogicDeleteStrategy.Boolean) : Attribute
{
    /// <summary>取值方式。</summary>
    public LogicDeleteStrategy Strategy { get; } = strategy;

    /// <summary><see cref="LogicDeleteStrategy.Custom"/> 时使用的策略名（见 <c>EasyDbOptions.LogicDeleteStrategies</c>）。</summary>
    public string? StrategyName { get; init; }
}
