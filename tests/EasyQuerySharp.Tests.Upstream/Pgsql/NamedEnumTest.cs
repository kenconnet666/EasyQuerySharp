using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Pgsql;

/// <summary>对应 Java：enums/NamedEnum（@Enumerated：按名字存）。成员名与 Java 一致，库里存的就是 USER / BOOK。</summary>
[StoreAsName]
public enum NamedEnum
{
    USER,
    TEACHER,
    BOOK,
}

/// <summary>对应 Java：pgsql/NamedEnumEntity（没有主键）。</summary>
[Table("t_named_enum")]
public class NamedEnumEntity
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public NamedEnum? Type { get; set; }
}

/// <summary>对应 Java：pgsql/NamedEnumTest.java（枚举按名字存：条件里的枚举值按名字传参，读出时按名字还原）。</summary>
public sealed class NamedEnumTest(PgFixture fixture) : PgTest(fixture)
{
    [Fact, Upstream("pgsql/NamedEnumTest.java#test1")]
    public async Task Test1()
    {
        using var listen = SqlCapture.Listen();
        var entity = await Db.Query<NamedEnumEntity>().Where(n => n.Type == NamedEnum.BOOK).SingleAsync(Ct);
        Assert.Equal(NamedEnum.BOOK, entity.Type);
        listen.AssertSql("SELECT \"id\",\"name\",\"type\" FROM \"t_named_enum\" WHERE \"type\" = ?");
        listen.AssertParams("BOOK(String)");
    }
}
