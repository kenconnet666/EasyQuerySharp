using System.Data.Common;
using System.Globalization;
using System.Reflection;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Materialization;

/// <summary>
/// 从 <see cref="DbDataReader"/> 读一个值并转成目标类型。各驱动返回的类型不尽相同
/// （SQLite 的整数布尔、文本日期，MySQL 的 tinyint、PG 的 numeric 等），统一在这里抹平。
/// </summary>
public static class ValueReader
{
    internal static readonly MethodInfo ReadMethod = typeof(ValueReader).GetMethod(nameof(Read))!;

    /// <summary>读第 <paramref name="ordinal"/> 列；NULL 返回 <c>default</c>。有转换器时先读数据库类型再转回属性类型。</summary>
    public static T Read<T>(DbDataReader reader, int ordinal, IValueConverter? converter)
    {
        if (reader.IsDBNull(ordinal)) return default!;
        object raw;
        try
        {
            raw = reader.GetValue(ordinal);
        }
        // Oracle 的 NUMBER 运算结果（LOG、LN 等）可能超过 decimal 的 28 位精度，按 decimal 读会溢出：浮点目标改读驱动自己的数值类型，
        // 按文本转成 double（ODP.NET 的 GetDouble 在这里会丢精度）
        catch (OverflowException) when (converter is null && (Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)) is var t && (t == typeof(double) || t == typeof(float)))
        {
            raw = reader.GetProviderSpecificValue(ordinal);
        }
        if (converter is not null) return (T)converter.FromProvider(ConvertTo(raw, converter.ProviderType))!;
        return (T)ConvertTo(raw, typeof(T))!;
    }

    /// <summary>把驱动返回的值转成 <paramref name="type"/>（可空类型按底层类型转）。</summary>
    public static object? ConvertTo(object? value, Type type)
    {
        if (value is null || value is DBNull) return null;
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsInstanceOfType(value)) return value;
        var ci = CultureInfo.InvariantCulture;
        // 驱动自己的数值类型（如 ODP.NET 输出参数的 OracleDecimal）没有实现 IConvertible：先转成不变区域的文本
        if ((target.IsPrimitive || target == typeof(decimal)) && value is not IConvertible) value = Convert.ToString(value, ci)!;

        if (target.IsEnum)
            return value is string s ? Enum.Parse(target, s, ignoreCase: true) : Enum.ToObject(target, Convert.ToInt64(value, ci));
        if (target == typeof(bool))
            return value switch
            {
                string s => s is "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase),
                _ => Convert.ToInt64(value, ci) != 0,
            };
        if (target == typeof(Guid))
            return value switch
            {
                string s => Guid.Parse(s),
                byte[] b => new Guid(b),
                _ => throw Mismatch(value, target),
            };
        if (target == typeof(DateTime))
            return value switch
            {
                string s => DateTime.Parse(s, ci, DateTimeStyles.RoundtripKind),
                DateTimeOffset o => o.DateTime,
                DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                _ => Convert.ToDateTime(value, ci),
            };
        if (target == typeof(DateTimeOffset))
            return value switch
            {
                string s => DateTimeOffset.Parse(s, ci, DateTimeStyles.RoundtripKind),
                DateTime d => new DateTimeOffset(DateTime.SpecifyKind(d, d.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : d.Kind)),
                _ => throw Mismatch(value, target),
            };
        if (target == typeof(DateOnly))
            return value switch
            {
                DateTime d => DateOnly.FromDateTime(d),
                string s => DateOnly.Parse(s, ci),
                _ => throw Mismatch(value, target),
            };
        if (target == typeof(TimeOnly))
            return value switch
            {
                TimeSpan t => TimeOnly.FromTimeSpan(t),
                DateTime d => TimeOnly.FromDateTime(d),
                string s => TimeOnly.Parse(s, ci),
                _ => throw Mismatch(value, target),
            };
        if (target == typeof(TimeSpan))
            return value switch
            {
                TimeOnly t => t.ToTimeSpan(),
                string s => TimeSpan.Parse(s, ci),
                _ => throw Mismatch(value, target),
            };
        // DuckDB.NET 把 BLOB 读成流
        if (target == typeof(byte[]) && value is Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        if (target == typeof(string))
            return value switch
            {
                byte[] b => System.Text.Encoding.UTF8.GetString(b),
                IFormattable f => f.ToString(null, ci),
                _ => value.ToString(),
            };
        if (value is IConvertible && Type.GetTypeCode(target) is >= TypeCode.Boolean and <= TypeCode.String)
            return Convert.ChangeType(value, target, ci);
        throw Mismatch(value, target);
    }

    private static InvalidCastException Mismatch(object value, Type target) =>
        new($"无法把数据库返回的 {value.GetType().Name} 值 '{value}' 转成 {target.Name}");
}
