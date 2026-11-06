using System.Reflection;

namespace EasyQuerySharp.Metadata;

/// <summary>实体上的一个值对象属性（见 <see cref="ValueObjectAttribute"/>）。</summary>
/// <param name="Path">从实体起的路径，如 <c>Address</c>、<c>Address.Geo</c>。</param>
/// <param name="Property">值对象属性本身。</param>
/// <param name="Columns">它在实体上占的列（含嵌套值对象的列），按声明顺序。</param>
/// <param name="Shape">值对象自己的形状：列相对值对象，单独投影 <c>s.Address</c> 时按它物化。</param>
internal sealed record ValueObjectMetadata(string Path, PropertyInfo Property, IReadOnlyList<ColumnMetadata> Columns, EntityMetadata Shape);
