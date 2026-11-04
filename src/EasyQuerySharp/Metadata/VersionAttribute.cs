namespace EasyQuerySharp.Metadata;

/// <summary>乐观锁版本号的取值方式（对应 Java 的 VersionStrategy）。</summary>
public enum VersionStrategy
{
    /// <summary>整数列：下一个版本是当前值 + 1（Java 的 VersionIntStrategy / VersionLongStrategy）。</summary>
    Increment,

    /// <summary>字符串或 Guid 列：下一个版本是新的 Guid，字符串列写成 32 位无连字符（Java 的 VersionUUIDStrategy）。</summary>
    NewGuid,
}

/// <summary>
/// 把属性标为乐观锁版本列（对应 Java 的 @Version）。更新、删除时条件里加 <c>`version` = 当前版本</c>，SET 里写下一个版本：
/// <list type="bullet">
/// <item>按实体写入：当前版本取实体上的值（为 null 时报错），执行成功后把新版本写回实体；</item>
/// <item>按条件写入：必须用 <c>WithVersion(v)</c> 给出当前版本，或 <c>IgnoreVersion()</c> 不做版本检查，否则报错。</item>
/// </list>
/// 版本冲突时影响行数为 0，可以用 <c>ExecuteAsync(1, "已被修改")</c> 抛出 <see cref="ConcurrencyException"/>。
/// </summary>
/// <example><code>[Version] public long? Version { get; set; }</code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class VersionAttribute(VersionStrategy strategy = VersionStrategy.Increment) : Attribute
{
    /// <summary>取值方式。</summary>
    public VersionStrategy Strategy { get; } = strategy;
}
