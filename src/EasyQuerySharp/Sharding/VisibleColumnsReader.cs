using System.Collections;
using System.Data.Common;

namespace EasyQuerySharp.Sharding;

/// <summary>
/// 只露出前 <c>visible</c> 列的读取器：跨单元归并时排序列追加在结果的最后（隐藏列），读取函数（尤其按整行读的 Map 投影）看不到它们。
/// 其余成员都转给原读取器。
/// </summary>
internal sealed class VisibleColumnsReader(DbDataReader inner, int visible) : DbDataReader
{
    public override int FieldCount => visible;

    public override int VisibleFieldCount => visible;

    public override int Depth => inner.Depth;

    public override bool HasRows => inner.HasRows;

    public override bool IsClosed => inner.IsClosed;

    public override int RecordsAffected => inner.RecordsAffected;

    public override object this[int ordinal] => inner[ordinal];

    public override object this[string name] => inner[name];

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);

    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => inner.GetChar(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);

    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);

    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);

    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);

    [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);

    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);

    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);

    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);

    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);

    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);

    public override string GetName(int ordinal) => inner.GetName(ordinal);

    public override int GetOrdinal(string name) => inner.GetOrdinal(name);

    public override string GetString(int ordinal) => inner.GetString(ordinal);

    public override object GetValue(int ordinal) => inner.GetValue(ordinal);

    public override T GetFieldValue<T>(int ordinal) => inner.GetFieldValue<T>(ordinal);

    public override object GetProviderSpecificValue(int ordinal) => inner.GetProviderSpecificValue(ordinal);

    [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetProviderSpecificFieldType(int ordinal) => inner.GetProviderSpecificFieldType(ordinal);

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, visible);
        for (var i = 0; i < count; i++) values[i] = inner.GetValue(i);
        return count;
    }

    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);

    public override bool NextResult() => inner.NextResult();

    public override bool Read() => inner.Read();

    public override IEnumerator GetEnumerator() => inner.GetEnumerator();
}
