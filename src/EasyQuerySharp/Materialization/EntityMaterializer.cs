using System.Data.Common;
using System.Reflection;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Materialization;

/// <summary>按元数据把连续的若干列读成一个实体实例。</summary>
public static class EntityMaterializer
{
    internal static readonly MethodInfo ReadMethod = typeof(EntityMaterializer).GetMethod(nameof(Read))!;

    /// <summary>
    /// 从第 <paramref name="start"/> 列起依次读 <paramref name="columns"/> 对应的值，写入新建的实体。
    /// <paramref name="nullWhenKeyNull"/> 为 true 时，主键列全为 NULL 就返回 null（LEFT JOIN 没连上的行）。
    /// </summary>
    public static object? Read(DbDataReader reader, int start, EntityMetadata entity, IReadOnlyList<ColumnMetadata> columns, bool nullWhenKeyNull)
    {
        if (nullWhenKeyNull && entity.KeyColumns.Count > 0 && AllKeysNull(reader, start, columns)) return null;

        var instance = entity.Factory();
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            if (column.Setter is null || reader.IsDBNull(start + i)) continue;
            var raw = reader.GetValue(start + i);
            var value = column.Converter is null
                ? ValueReader.ConvertTo(raw, column.ClrType)
                : column.Converter.FromProvider(ValueReader.ConvertTo(raw, column.Converter.ProviderType));
            column.Setter(instance, value);
        }
        return instance;
    }

    /// <summary>
    /// 按结果集的列名读成实体（原生 SQL 投影时列序号未知）：列名与实体的列名匹配（忽略大小写），匹配不上的列忽略。
    /// </summary>
    public static T ReadByName<T>(DbDataReader reader, EntityMetadata entity)
    {
        var instance = entity.Factory();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            // 先按列名、再按属性名（与 Java 一致：create_time AS createTime 也能映射）
            var column = entity.Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? entity.Columns.FirstOrDefault(c => string.Equals(c.PropertyName, name, StringComparison.OrdinalIgnoreCase));
            if (column?.Setter is null || reader.IsDBNull(i)) continue;
            var raw = reader.GetValue(i);
            var value = column.Converter is null
                ? ValueReader.ConvertTo(raw, column.ClrType)
                : column.Converter.FromProvider(ValueReader.ConvertTo(raw, column.Converter.ProviderType));
            column.Setter(instance, value);
        }
        return (T)instance;
    }

    /// <summary>
    /// 把当前行读成 列名 → 值 的字典（对应 Java 的 toMap / sqlQueryMap），列名忽略大小写，NULL 读成 null。
    /// 列名重复时抛出异常（与 Java 一致，避免后一列静默覆盖前一列）。
    /// </summary>
    public static Dictionary<string, object?> ReadDictionary(DbDataReader reader) => ReadDictionary(reader, 0, reader.FieldCount);

    /// <summary>只把 [<paramref name="start"/>, start + <paramref name="count"/>) 这几列读成字典（Map 投影里实体的全部列，后面还可能有别的键）。</summary>
    public static Dictionary<string, object?> ReadDictionary(DbDataReader reader, int start, int count)
    {
        var row = new Dictionary<string, object?>(count, StringComparer.OrdinalIgnoreCase);
        for (var i = start; i < start + count; i++)
        {
            var name = reader.GetName(i);
            if (!row.TryAdd(name, reader.IsDBNull(i) ? null : reader.GetValue(i)))
                throw new InvalidOperationException($"结果集里有重复的列名：{name}");
        }
        return row;
    }

    private static bool AllKeysNull(DbDataReader reader, int start, IReadOnlyList<ColumnMetadata> columns)
    {
        var sawKey = false;
        for (var i = 0; i < columns.Count; i++)
        {
            if (!columns[i].IsPrimaryKey) continue;
            sawKey = true;
            if (!reader.IsDBNull(start + i)) return false;
        }
        return sawKey;
    }
}
