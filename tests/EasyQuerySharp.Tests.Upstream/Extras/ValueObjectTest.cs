using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 值对象 <c>[ValueObject]</c>（C# 与 TS 版独有，Java 的 @ValueObject 已弃用）：几列合成实体上的一个嵌套对象。
/// 只比对 SQL；读写的往返在 L3 跨方言套件里（LiveSuite.ValueObjects）。
/// </summary>
public sealed class ValueObjectTest
{
    private static EasyDb Db => SqlOnly.Db("pgsql");

    [Table("t_vo_shop")]
    public class VoShop
    {
        [Column(PrimaryKey = true)]
        public long Id { get; set; }

        public string? Name { get; set; }

        public long? MallId { get; set; }

        [ValueObject]
        public VoAddress Address { get; set; } = new();

        [ValueObject]
        public VoContact? Contact { get; set; }
    }

    public class VoAddress
    {
        public string? City { get; set; }

        public string? Street { get; set; }

        // 显式前缀是完整前缀，不接在外层前缀后面
        [ValueObject(Prefix = "geo_")]
        public VoGeo Geo { get; set; } = new();
    }

    public class VoGeo
    {
        public double Lat { get; set; }

        public double Lng { get; set; }
    }

    public class VoContact
    {
        // 子属性上的 [Column("…")] 是完整列名
        [Column("tel")]
        public string? Phone { get; set; }

        public int Level { get; set; }
    }

    [Fact]
    public void ColumnsAreFlattenedWithPrefixes() =>
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"t_vo_shop\" (\"id\" BIGINT NOT NULL, \"name\" VARCHAR(255) NULL, \"mall_id\" BIGINT NULL, \"address_city\" VARCHAR(255) NULL, \"address_street\" VARCHAR(255) NULL, "
            + "\"geo_lat\" DOUBLE PRECISION NOT NULL, \"geo_lng\" DOUBLE PRECISION NOT NULL, \"tel\" VARCHAR(255) NULL, \"contact_level\" INTEGER NULL, PRIMARY KEY (\"id\"))",
            Db.CodeFirst.CreateTableSql(typeof(VoShop)));

    [Fact]
    public void MembersOfValueObjectsAreColumns() =>
        SqlOnly.AssertStatement(
            Db.Query<VoShop>().Where(s => s.Address.City == "hz" && s.Address.Geo.Lat > 30).OrderBy(s => s.Contact!.Level).ToStatement(),
            "SELECT \"id\",\"name\",\"mall_id\",\"address_city\",\"address_street\",\"geo_lat\",\"geo_lng\",\"tel\",\"contact_level\" FROM \"t_vo_shop\" WHERE \"address_city\" = ? AND \"geo_lat\" > ? ORDER BY \"contact_level\" ASC",
            "hz(String),30(Double)");

    // 值对象判空：列全为 NULL；不为 null：至少一列有值
    [Fact]
    public void NullChecksOnValueObjectsCoverAllItsColumns() =>
        Assert.Equal(
            "SELECT t.\"id\" FROM \"t_vo_shop\" t WHERE ((t.\"tel\" IS NULL AND t.\"contact_level\" IS NULL) OR (t.\"tel\" IS NOT NULL OR t.\"contact_level\" IS NOT NULL))",
            Db.Query<VoShop>().Where(s => s.Contact == null || s.Contact != null).Select(s => s.Id).ToSql());

    [Fact]
    public void WholeValueObjectsAreProjected() =>
        Assert.Equal(
            "SELECT t.\"name\",t.\"address_city\",t.\"address_street\",t.\"geo_lat\",t.\"geo_lng\" FROM \"t_vo_shop\" t",
            Db.Query<VoShop>().Select(s => new { s.Name, s.Address }).ToSql());

    [Fact]
    public void UpdatesSetSomeOrAllColumnsOfAValueObject()
    {
        Assert.Equal("UPDATE \"t_vo_shop\" SET \"address_city\" = ?,\"tel\" = ?,\"contact_level\" = ? WHERE \"id\" = ?",
            Db.Update<VoShop>().Set(s => new VoShop { Address = new VoAddress { City = "sz" }, Contact = null }).Where(s => s.Id == 1).ToSql());
        Assert.Equal("UPDATE \"t_vo_shop\" SET \"geo_lat\" = ? WHERE \"id\" = ?",
            Db.Update<VoShop>().Set(s => s.Address.Geo.Lat, 31.5).Where(s => s.Id == 1).ToSql());
    }

    // 与其它列一样，值为 null 的列不写（Contact 为 null，Street 为 null）
    [Fact]
    public void InsertsWriteTheColumnsThatHaveValues() =>
        Assert.Equal("INSERT INTO \"t_vo_shop\" (\"id\",\"name\",\"address_city\",\"geo_lat\",\"geo_lng\") VALUES (?,?,?,?,?)",
            Db.Insert(new VoShop { Id = 1, Name = "a", Address = new VoAddress { City = "hz", Geo = new VoGeo { Lat = 30, Lng = 120 } } }).ToSql());

    [Table("t_vo_mall")]
    public class VoMall
    {
        [Column(PrimaryKey = true)]
        public long Id { get; set; }

        [Navigate(RelationType.OneToMany, TargetProperties = [nameof(VoShop.MallId)])]
        public List<VoShop>? Shops { get; set; }
    }

    public class VoShopDto
    {
        public string? Name { get; set; }

        public VoAddress? Address { get; set; }
    }

    // 隐式子查询里同样按列翻译
    [Fact]
    public void ValueObjectMembersWorkInImplicitSubqueries() =>
        Assert.Equal(
            "SELECT t.\"id\" FROM \"t_vo_mall\" t WHERE EXISTS(SELECT 1 FROM \"t_vo_shop\" t1 WHERE t1.\"mall_id\" = t.\"id\" AND t1.\"address_city\" = ? LIMIT 1)",
            Db.Query<VoMall>().Where(m => m.Shops!.Any(s => s.Address.City == "hz")).ToSql());

    // 按结构映射的 DTO：同名同类型的成员整个取值对象
    [Fact]
    public void DtoMembersMapWholeValueObjects() =>
        Assert.Equal(
            "SELECT t.\"name\",t.\"address_city\",t.\"address_street\",t.\"geo_lat\",t.\"geo_lng\" FROM \"t_vo_shop\" t",
            Db.Query<VoShop>().Select<VoShopDto>().ToSql());

    [Fact]
    public void ValueObjectsNeedAParameterlessClass()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Db.Metadata.Get<BadShop>());
        Assert.Contains("无参构造函数", ex.Message);
    }

    [Table("t_bad")]
    public class BadShop
    {
        [Column(PrimaryKey = true)]
        public long Id { get; set; }

        [ValueObject]
        public BadAddress? Address { get; set; }
    }

    public sealed class BadAddress(string city)
    {
        public string City { get; set; } = city;
    }
}
