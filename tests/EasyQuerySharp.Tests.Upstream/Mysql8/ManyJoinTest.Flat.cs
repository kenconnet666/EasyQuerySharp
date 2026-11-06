using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

// 多对多导航上的平铺：经过菜单再到所属菜单（menus.menuOwner），菜单 DTO 上平铺所属菜单的列
public sealed partial class ManyJoinTest
{
    /// <summary>对应 Java：mysql8/dto/RoleSelectAutoExtra（角色上平铺各菜单的所属菜单，带出条件排除 /123）。</summary>
    public sealed class RoleSelectAutoExtra
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public DateTime? CreateTime { get; set; }

        [MapPath("Menus.MenuOwner")]
        public List<InternalMenuOwner>? MenuOwners { get; set; }

        /// <summary>M8MenuOwner 的投影。</summary>
        public sealed class InternalMenuOwner : IAutoIncludeConfigure<M8MenuOwner>
        {
            public static Query<M8MenuOwner> Configure(Query<M8MenuOwner> query) => query.Where(o => o.Name != "/123");

            public string? Id { get; set; }

            public string? Name { get; set; }
        }
    }

    // 多对多先查中间表，再查菜单表把菜单键换成所属菜单键，最后按所属菜单键查
    [Fact, Upstream("mysql8/ManyJoinTest.java#testFlatExtra")]
    public async Task TestFlatExtra()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<M8Role>().Select<RoleSelectAutoExtra>().ToListAsync(Ct);
        Assert.Equal(4, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`create_time` FROM `m8_role` t");
        listen.AssertSql(1, "SELECT `role_id`,`menu_id` FROM `m8_role_menu` WHERE `role_id` IN (?)");
        listen.AssertParams(1, "r123(String)");
        listen.AssertSql(2, "SELECT `owner_id` AS `__relation__ownerId`,`id` AS `__relation__id` FROM `m8_menu` WHERE `id` IN (?)");
        listen.AssertParams(2, "m123(String)");
        listen.AssertSql(3, "SELECT t.`id`,t.`name` FROM `m8_menu` t WHERE t.`name` <> ? AND t.`id` IN (?)");
        listen.AssertParams(3, "/123(String),o123(String)");
        Assert.Equal("o123", Assert.Single(Assert.Single(list).MenuOwners!).Id);
    }

    /// <summary>对应 Java：mysql8/dto/M8RoleDTO（菜单 DTO 上平铺所属菜单的 id、name）。</summary>
    public sealed class M8RoleDTO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public DateTime? CreateTime { get; set; }

        public List<InternalOwner>? Menus { get; set; }

        /// <summary>M8Menu 的投影。</summary>
        public sealed class InternalOwner
        {
            [MapPath("MenuOwner.Id")]
            public string? Id { get; set; }

            [MapPath("MenuOwner.Name")]
            public string? Name { get; set; }
        }
    }

    // Java 用例没有断言，只验证能执行
    [Fact, Upstream("mysql8/ManyJoinTest.java#testFlatMenuOwner")]
    public async Task TestFlatMenuOwner()
    {
        var list = await Db.Query<M8Role>().Select<M8RoleDTO>().ToListAsync(Ct);
        Assert.Equal("o123", Assert.Single(Assert.Single(list).Menus!).Id);
    }

    /// <summary>对应 Java：mysql8/dto/M8RoleDTO2（角色上平铺各菜单所属菜单的 id 列表）。</summary>
    public sealed class M8RoleDTO2
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public DateTime? CreateTime { get; set; }

        [MapPath("Menus.MenuOwner.Id")]
        public List<string?>? MenuOwnerIds { get; set; }
    }

    // Java 用例没有断言，只验证能执行
    [Fact, Upstream("mysql8/ManyJoinTest.java#testFlatMenuOwner1")]
    public async Task TestFlatMenuOwner1()
    {
        var list = await Db.Query<M8Role>().Select<M8RoleDTO2>().ToListAsync(Ct);
        Assert.Equal(["o123"], Assert.Single(list).MenuOwnerIds!);
    }

    /// <summary>读出后在末尾加 *（对应 Java 的 AddStartValueConverter）。</summary>
    public sealed class AddStartValueConverter : ValueConverter<string?, string?>
    {
        protected override string? ToProvider(string? value) => value;

        protected override string? FromProvider(string? value) => value + "*";
    }

    /// <summary>对应 Java：mysql8/dto/M8RoleFlatConverterDTO（平铺的 name 上带值转换器）。</summary>
    public sealed class M8RoleFlatConverterDTO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public DateTime? CreateTime { get; set; }

        public List<InternalOwner>? Menus { get; set; }

        /// <summary>M8Menu 的投影。</summary>
        public sealed class InternalOwner
        {
            [MapPath("MenuOwner.Id")]
            public string? Id { get; set; }

            [MapPath("MenuOwner.Name"), Column(Converter = typeof(AddStartValueConverter))]
            public string? Name { get; set; }
        }
    }

    [Fact, Upstream("mysql8/ManyJoinTest.java#testFlatMenuOwnerFlatValueConverter")]
    public async Task TestFlatMenuOwnerFlatValueConverter()
    {
        var list = await Db.Query<M8Role>().Select<M8RoleFlatConverterDTO>().ToListAsync(Ct);
        Assert.NotEmpty(list);
        Assert.All(list.SelectMany(r => r.Menus!), menu => Assert.EndsWith("*", menu.Name));
    }
}
