using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8PCABTest.java（省 → 市 → 区 → 楼：平铺路径经过中间实体的带出）。</summary>
public sealed class M8PCABTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    /// <summary>对应 Java：mysql8/dto/M8ProvinceDTO（市上平铺各区的楼）。</summary>
    public sealed class M8ProvinceDTO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<InternalCities>? Cities { get; set; }

        /// <summary>M8AreaBuild 的投影。</summary>
        public sealed class InternalBuilds
        {
            public string? Id { get; set; }

            public string? Aid { get; set; }

            public string? Name { get; set; }
        }

        /// <summary>M8City 的投影。</summary>
        public sealed class InternalCities
        {
            public string? Id { get; set; }

            public string? Pid { get; set; }

            public string? Name { get; set; }

            [MapPath("Areas.Builds")]
            public List<InternalBuilds>? Builds { get; set; }
        }
    }

    /// <summary>对应 Java：mysql8/dto/M8ProvinceDTO2（楼上再平铺许可证编号）。</summary>
    public sealed class M8ProvinceDTO2
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<InternalCities>? Cities { get; set; }

        /// <summary>M8AreaBuild 的投影。</summary>
        public sealed class InternalBuilds
        {
            public string? Id { get; set; }

            public string? Aid { get; set; }

            public string? Name { get; set; }

            [MapPath("License.Id")]
            public string? LicenseId { get; set; }
        }

        /// <summary>M8City 的投影。</summary>
        public sealed class InternalCities
        {
            public string? Id { get; set; }

            public string? Pid { get; set; }

            public string? Name { get; set; }

            [MapPath("Areas.Builds")]
            public List<InternalBuilds>? Builds { get; set; }
        }
    }

    /// <summary>对应 Java：mysql8/dto/M8ProvinceDTO3（楼上嵌套许可证）。</summary>
    public sealed class M8ProvinceDTO3
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<InternalCities>? Cities { get; set; }

        /// <summary>M8AreaBuild 的投影。</summary>
        public sealed class InternalBuilds
        {
            public string? Id { get; set; }

            public string? Aid { get; set; }

            public string? Name { get; set; }

            public InternalLicense? License { get; set; }
        }

        /// <summary>M8AreaBuildLicense 的投影。</summary>
        public sealed class InternalLicense
        {
            public string? Id { get; set; }

            public string? No { get; set; }
        }

        /// <summary>M8City 的投影。</summary>
        public sealed class InternalCities
        {
            public string? Id { get; set; }

            public string? Pid { get; set; }

            public string? Name { get; set; }

            [MapPath("Areas.Builds")]
            public List<InternalBuilds>? Builds { get; set; }
        }
    }

    private const string Province = "SELECT t.`id`,t.`name` FROM `m8_province` t";
    private const string Cities = "SELECT t.`id`,t.`pid`,t.`name` FROM `m8_city` t WHERE t.`pid` IN (?)";
    private const string AreaLinks = "SELECT `id` AS `__relation__id`,`cid` AS `__relation__cid` FROM `m8_area` WHERE `cid` IN (?,?)";
    private const string Builds = "SELECT t.`id`,t.`aid`,t.`name` FROM `m8_build` t WHERE t.`aid` IN (?,?,?,?)";

    // 市 → 区 → 楼：先查区表把市键换成区键，再按区键查楼
    private static void AssertBuildPath(SqlCapture.Scope listen)
    {
        listen.AssertSql(0, Province);
        listen.AssertSql(1, Cities);
        listen.AssertParams(1, "p1(String)");
        listen.AssertSql(2, AreaLinks);
        listen.AssertParams(2, "c1(String),c2(String)");
        listen.AssertSql(3, Builds);
        listen.AssertParams(3, "a1(String),a2(String),a3(String),a4(String)");
    }

    [Fact, Upstream("mysql8/M8PCABTest.java#testFlat")]
    public async Task TestFlat()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<M8Province>().SelectAutoInclude<M8ProvinceDTO>().ToListAsync(Ct);
        Assert.Equal(4, listen.Events.Count);
        AssertBuildPath(listen);
        var city = Assert.Single(list).Cities!.Single(c => c.Id == "c1");
        Assert.Equal(["b1", "b2", "b3"], city.Builds!.Select(b => b.Id).Order());
    }

    // 楼上的对一平铺再走一条带出查询（单表不写别名）
    [Fact, Upstream("mysql8/M8PCABTest.java#testFlat1")]
    public async Task TestFlat1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().SelectAutoInclude<M8ProvinceDTO2>().ToListAsync(Ct);
        Assert.Equal(5, listen.Events.Count);
        AssertBuildPath(listen);
        listen.AssertSql(4, "SELECT `id`,`build_id` AS `__relation__buildId` FROM `m8_build_license` WHERE `build_id` IN (?,?,?)");
        listen.AssertParams(4, "b1(String),b2(String),b3(String)");
    }

    // 楼上嵌套的对一 DTO 照常带出（带别名 t）
    [Fact, Upstream("mysql8/M8PCABTest.java#testFlat2")]
    public async Task TestFlat2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<M8Province>().SelectAutoInclude<M8ProvinceDTO3>().ToListAsync(Ct);
        Assert.Equal(5, listen.Events.Count);
        AssertBuildPath(listen);
        listen.AssertSql(4, "SELECT t.`id`,t.`no`,t.`build_id` AS `__relation__buildId` FROM `m8_build_license` t WHERE t.`build_id` IN (?,?,?)");
        listen.AssertParams(4, "b1(String),b2(String),b3(String)");
    }
}
