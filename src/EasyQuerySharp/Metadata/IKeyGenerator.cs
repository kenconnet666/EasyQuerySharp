namespace EasyQuerySharp.Metadata;

/// <summary>
/// 主键生成器（对应 Java 的 PrimaryKeyGenerator）：插入时主键为 null 的实体由它生成主键并写回实体。
/// 实现类需要无参构造函数；用法：<c>[Column(PrimaryKey = true, KeyGenerator = typeof(GuidStringKey))]</c>。
/// </summary>
public interface IKeyGenerator
{
    /// <summary>为 <paramref name="column"/> 生成一个新主键值（类型须能赋给该属性）。</summary>
    object NewKey(ColumnMetadata column);
}
