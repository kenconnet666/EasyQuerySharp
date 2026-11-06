using System.Text;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>对应 Java fork：json/JsonValueConverterTest.java（JSON 列的转换器按声明的泛型类型解码，null 与空白读成 null）。</summary>
public sealed class JsonValueConverterTest
{
    private static readonly ColumnMetadata Addresses = SqlOnly.Db("mysql").Metadata.Get<ForkProfile>().Columns.Single(c => c.PropertyName == nameof(ForkProfile.Addresses));

    private static IValueConverter Converter => Addresses.Converter!;

    [Fact, Upstream("fork/json/JsonValueConverterTest.java#decodesWithTheDeclaredGenericType",
        Deviation = "Java 另外验证 JDBC 的 Clob；.NET 的驱动把文本列读成 string、二进制列读成 byte[]，这两种都验证")]
    public void DecodesWithTheDeclaredGenericType()
    {
        const string json = "[{\"city\":\"Hangzhou\",\"zip\":310000}]";
        foreach (var provider in new object[] { json, Encoding.UTF8.GetBytes(json) })
        {
            var list = Assert.IsType<List<ForkProfile.Address>>(Converter.FromProvider(provider));
            Assert.Equal("Hangzhou", list[0].City);
            Assert.Equal(310000, list[0].Zip);
        }
    }

    [Fact, Upstream("fork/json/JsonValueConverterTest.java#nullAndBlankStayNull")]
    public void NullAndBlankStayNull()
    {
        Assert.Null(Converter.ToProvider(null));
        Assert.Null(Converter.FromProvider(null));
        Assert.Null(Converter.FromProvider("  "));
    }

    [Fact, Upstream("fork/json/JsonValueConverterTest.java#columnIsBoundToTheConverter")]
    public void ColumnIsBoundToTheConverter() => Assert.IsType<JsonValueConverter<List<ForkProfile.Address>>>(Converter);
}
