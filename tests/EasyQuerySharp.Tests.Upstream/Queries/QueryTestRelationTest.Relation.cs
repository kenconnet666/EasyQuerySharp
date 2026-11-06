using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Queries;

// QueryTestRelationTest.java#testRelation 的各段（共 24 段，按段移植；全部移植后再标 Upstream，见 ROADMAP 1.9）
public sealed partial class QueryTestRelationTest
{
    /// <summary>Java 的 MySignUpDTOx：企业用户上的三列平铺进来；ComUserName 没有对应的列，保持 null。</summary>
    public sealed class MySignUpDTOx
    {
        public string? Id { get; set; }

        public DateTime? Time { get; set; }

        public string? Content { get; set; }

        public string? ComUserName { get; set; }

        [MapPath("ComUser.Gw")]
        public string? Gw { get; set; }

        [MapPath("ComUser.ComId")]
        public string? ComId { get; set; }

        [MapPath("ComUser.UserId")]
        public string? UserId { get; set; }
    }

    /// <summary>Java 的 MySignUpDTO4：直达导航 ComUserCompany 整个带出。</summary>
    public sealed class MySignUpDTO4
    {
        public string? Id { get; set; }

        public string? UserId { get; set; }

        public DateTime? Time { get; set; }

        public string? Content { get; set; }

        public CompanyDto? ComUserCompany { get; set; }
    }

    /// <summary>企业。</summary>
    public sealed class CompanyDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    // 与 Java 的 before() / deleteRelation() 相同的数据
    private async Task RelationSeedAsync()
    {
        await RelationClearAsync();
        await Db.Insert(new MyUser { Id = "u1", Name = "小明", Age = 12 }, new MyUser { Id = "u2", Name = "小红", Age = 12 }, new MyUser { Id = "u3", Name = "小蓝", Age = 12 }).ExecuteAsync(Ct);
        await Db.Insert(new MyCompany { Id = "c1", Name = "公司1" }, new MyCompany { Id = "c2", Name = "公司2" }).ExecuteAsync(Ct);
        await Db.Insert(new MyComUser { Id = "cu1", ComId = "c1", UserId = "u1", Gw = "JAVA" }, new MyComUser { Id = "cu2", ComId = "c2", UserId = "u1", Gw = "测试" },
            new MyComUser { Id = "cu3", ComId = "c1", UserId = "u2", Gw = "VUE" }, new MyComUser { Id = "cu4", ComId = "c1", UserId = "u3", Gw = "C#" }).ExecuteAsync(Ct);
        await Db.Insert(new MySignUp { Id = "s1", ComId = "c1", UserId = "u1", Time = new DateTime(2024, 1, 1), Content = "写了一些代码1" },
            new MySignUp { Id = "s2", ComId = "c1", UserId = "u1", Time = new DateTime(2024, 2, 1), Content = "写了一些代码2" },
            new MySignUp { Id = "s3", ComId = "c1", UserId = "u3", Time = new DateTime(2024, 1, 1), Content = "写了一些代码3" },
            new MySignUp { Id = "s4", ComId = "c2", UserId = "u2", Time = new DateTime(2024, 1, 1), Content = "写了一些代码5" }).ExecuteAsync(Ct);
    }

    private async Task RelationClearAsync()
    {
        await Db.Delete<MySignUp>().AllowDeleteStatement(true).All().ExecuteAsync(Ct);
        await Db.Delete<MyComUser>().AllowDeleteStatement(true).All().ExecuteAsync(Ct);
        await Db.Delete<MyCompany>().AllowDeleteStatement(true).All().ExecuteAsync(Ct);
        await Db.Delete<MyUser>().AllowDeleteStatement(true).All().ExecuteAsync(Ct);
    }

    private async Task WithRelationDataAsync(Func<SqlCapture.Scope, Task> body)
    {
        await RelationSeedAsync();
        try
        {
            using var listen = SqlCapture.Listen();
            await body(listen);
        }
        finally
        {
            await RelationClearAsync();
        }
    }

    private const string ComUserKeys = "((`com_id` =? AND `user_id` =?) OR (`com_id` =? AND `user_id` =?) OR (`com_id` =? AND `user_id` =?))";

    // 第 1 段：带出复合键的集合导航，按导航上声明的排序
    [Fact]
    public Task TestRelation_IncludeOrderedCollection() => WithRelationDataAsync(async listen =>
    {
        await Db.Query<MyComUser1>().Include(s => s.MySignUps).ToListAsync(Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT `id`,`com_id`,`user_id`,`gw` FROM `my_com_user`");
        listen.AssertSql(1, "SELECT t.`id`,t.`com_id`,t.`user_id`,t.`time`,t.`content` FROM `my_sign_up` t WHERE ((t.`com_id` =? AND t.`user_id` =?) OR (t.`com_id` =? AND t.`user_id` =?) OR (t.`com_id` =? AND t.`user_id` =?) OR (t.`com_id` =? AND t.`user_id` =?)) ORDER BY t.`com_id` ASC,CASE WHEN t.`time` IS NULL THEN 0 ELSE 1 END ASC,t.`time` DESC");
        listen.AssertParams(1, "c1(String),u1(String),c2(String),u1(String),c1(String),u2(String),c1(String),u3(String)");
    });

    // 第 3 段：Include 的配置并入 DTO 里同名导航的平铺带出
    [Fact]
    public Task TestRelation_IncludeConfigMergesIntoDto() => WithRelationDataAsync(async listen =>
    {
        var rows = await Db.Query<MySignUp>().Include(m => m.ComUser, q => q.Where(x => x.Gw == "123")).Where(m => m.Id != null)
            .Select<MySignUpDTOx>().ToListAsync(Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`time`,t.`content`,t.`com_id` AS `__relation__comId`,t.`user_id` AS `__relation__userId` FROM `my_sign_up` t WHERE t.`id` IS NOT NULL");
        listen.AssertSql(1, "SELECT `gw`,`com_id`,`user_id` FROM `my_com_user` WHERE `gw` = ? AND " + ComUserKeys);
        listen.AssertParams(1, "123(String),c1(String),u1(String),c1(String),u3(String),c2(String),u2(String)");
        Assert.All(rows, r => Assert.Null(r.Gw));
    });

    // 第 2 段：先分页再投影到 DTO（Java 的 toPageSelectResult）：这一页包成派生表，Include 的配置照常并入
    [Fact]
    public Task TestRelation_PageThenProjectToDto() => WithRelationDataAsync(async listen =>
    {
        var page = await Db.Query<MySignUp>().Include(m => m.ComUser, q => q.Where(x => x.Gw == "123")).Where(m => m.Id != null)
            .ToPageAsync(1, 10, q => q.Select<MySignUpDTOx>(), Ct);
        Assert.Equal((4L, 4), (page.Total, page.Rows.Count));
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `my_sign_up` WHERE `id` IS NOT NULL");
        listen.AssertSql(1, "SELECT t1.`id`,t1.`time`,t1.`content`,t1.`com_id` AS `__relation__comId`,t1.`user_id` AS `__relation__userId` FROM (SELECT t.`id`,t.`com_id`,t.`user_id`,t.`time`,t.`content` FROM `my_sign_up` t WHERE t.`id` IS NOT NULL LIMIT 4) t1");
        listen.AssertSql(2, "SELECT `gw`,`com_id`,`user_id` FROM `my_com_user` WHERE `gw` = ? AND " + ComUserKeys);
        listen.AssertParams(2, "123(String),c1(String),u1(String),c1(String),u3(String),c2(String),u2(String)");
    });

    /// <summary>Java 的 MySignUpDTO2 / DTO3：报名的全部列，直达导航带出企业（DTO3 只要企业名）。</summary>
    public sealed class MySignUpDTO2<TCompany>
    {
        public string? Id { get; set; }

        public string? ComId { get; set; }

        public string? UserId { get; set; }

        public DateTime? Time { get; set; }

        public string? Content { get; set; }

        public TCompany? ComUserCompany { get; set; }
    }

    /// <summary>只有企业名（Java 的 MySignUpDTO3.InternalComUserCompany）。</summary>
    public sealed class CompanyName
    {
        public string? Name { get; set; }
    }

    /// <summary>Java 的 MySignUpDTO1：企业用户、再到企业，逐层带出。</summary>
    public sealed class MySignUpDTO1
    {
        public string? Id { get; set; }

        public string? ComId { get; set; }

        public string? UserId { get; set; }

        public DateTime? Time { get; set; }

        public string? Content { get; set; }

        public ComUserDto? ComUser { get; set; }
    }

    /// <summary>企业用户与它的企业。</summary>
    public sealed class ComUserDto
    {
        public string? Id { get; set; }

        public string? ComId { get; set; }

        public string? UserId { get; set; }

        public string? Gw { get; set; }

        public CompanyDto? MyCompany { get; set; }
    }

    private const string SignUpMain = "SELECT t.`id`,t.`com_id`,t.`user_id`,t.`time`,t.`content` FROM `my_sign_up` t WHERE t.`id` IS NOT NULL";
    private const string ComUserParams3 = "c1(String),u1(String),c1(String),u3(String),c2(String),u2(String)";

    // 第 5 段 / 第 7 段：DTO 带全部列时父键直接复用；只要企业名时企业的键另起 __relation__ 列
    [Theory]
    [InlineData(false, "SELECT t.`id`,t.`name` FROM `my_company_info` t WHERE t.`id` IN (?)")]
    [InlineData(true, "SELECT t.`name`,t.`id` AS `__relation__id` FROM `my_company_info` t WHERE t.`id` IN (?)")]
    public Task TestRelation_DirectNavigationInDtoWithKeys(bool nameOnly, string companySql) => WithRelationDataAsync(async listen =>
    {
        var query = Db.Query<MySignUp>().Where(m => m.Id != null);
        if (nameOnly) await query.Select<MySignUpDTO2<CompanyName>>().ToListAsync(Ct);
        else await query.Select<MySignUpDTO2<CompanyDto>>().ToListAsync(Ct);
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, SignUpMain);
        listen.AssertSql(1, "SELECT `com_id`,`user_id` FROM `my_com_user` WHERE " + ComUserKeys);
        listen.AssertParams(1, ComUserParams3);
        listen.AssertSql(2, companySql);
        listen.AssertParams(2, "c1(String)");
    });

    // 第 6 段：实体上 Include 直达导航
    [Fact]
    public Task TestRelation_IncludeDirectNavigation() => WithRelationDataAsync(async listen =>
    {
        var rows = await Db.Query<MySignUp>().Include(m => m.ComUserCompany).Where(m => m.Id != null).ToListAsync(Ct);
        Assert.All(rows, r => Assert.Equal(r.ComId == "c1" ? "c1" : null, r.ComUserCompany?.Id));
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT `id`,`com_id`,`user_id`,`time`,`content` FROM `my_sign_up` WHERE `id` IS NOT NULL");
        listen.AssertSql(1, "SELECT `com_id`,`user_id` FROM `my_com_user` WHERE " + ComUserKeys);
        listen.AssertParams(1, ComUserParams3);
        listen.AssertSql(2, "SELECT t.`id`,t.`name` FROM `my_company_info` t WHERE t.`id` IN (?)");
    });

    // 第 8 段：DTO 嵌套两层对一导航，逐层带出
    [Fact]
    public Task TestRelation_NestedToOneDtos() => WithRelationDataAsync(async listen =>
    {
        await Db.Query<MySignUp>().Where(m => m.Id != null).Select<MySignUpDTO1>().ToListAsync(Ct);
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, SignUpMain);
        listen.AssertSql(1, "SELECT t.`id`,t.`com_id`,t.`user_id`,t.`gw` FROM `my_com_user` t WHERE ((t.`com_id` =? AND t.`user_id` =?) OR (t.`com_id` =? AND t.`user_id` =?) OR (t.`com_id` =? AND t.`user_id` =?))");
        listen.AssertParams(1, ComUserParams3);
        listen.AssertSql(2, "SELECT t.`id`,t.`name` FROM `my_company_info` t WHERE t.`id` IN (?)");
        listen.AssertParams(2, "c1(String)");
    });

    /// <summary>Java 的 MyComUserDTO4：DTO 上的 [Navigate] 覆盖实体导航的排序（NULL 排最后）。</summary>
    public sealed class MyComUserDTO4
    {
        public string? Gw { get; set; }

        [Navigate(RelationType.OneToMany, OrderBy = "ComId, Time desc nulls last")]
        public List<SignUpItem>? MySignUps { get; set; }
    }

    /// <summary>Java 的 MyComUserDTO6：每个企业用户取两条报名。</summary>
    public sealed class MyComUserDTO6
    {
        public string? Gw { get; set; }

        [Navigate(RelationType.OneToMany, Limit = 2)]
        public List<SignUpItem>? MySignUps { get; set; }
    }

    /// <summary>Java 的 MyComUserDTO7：每个企业用户跳过一条、取两条。</summary>
    public sealed class MyComUserDTO7
    {
        public string? Gw { get; set; }

        [Navigate(RelationType.OneToMany, Offset = 1, Limit = 2)]
        public List<SignUpItem>? MySignUps { get; set; }
    }

    /// <summary>报名（DTO 里的元素）。</summary>
    public sealed class SignUpItem
    {
        public string? Id { get; set; }

        public DateTime? Time { get; set; }

        public string? Content { get; set; }
    }

    private const string ComUserMain = "SELECT t.`gw`,t.`com_id` AS `__relation__comId`,t.`user_id` AS `__relation__userId` FROM `my_com_user` t";
    private const string SignUpKeys4 = "((t.`com_id` =? AND t.`user_id` =?) OR (t.`com_id` =? AND t.`user_id` =?) OR (t.`com_id` =? AND t.`user_id` =?) OR (t.`com_id` =? AND t.`user_id` =?))";
    private const string SignUpParams4 = "c1(String),u1(String),c2(String),u1(String),c1(String),u2(String),c1(String),u3(String)";

    // DTO 上声明的排序覆盖实体导航的（MyComUser 没有声明，MyComUser1 声明了 NULL 排最前）
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TestRelation_DtoNavigateOrderOverridesEntity(bool entityHasOrder) => WithRelationDataAsync(async listen =>
    {
        if (entityHasOrder) await Db.Query<MyComUser1>().Select<MyComUserDTO4>().ToListAsync(Ct);
        else await Db.Query<MyComUser>().Select<MyComUserDTO4>().ToListAsync(Ct);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, ComUserMain);
        listen.AssertSql(1, "SELECT t.`id`,t.`time`,t.`content`,t.`com_id` AS `__relation__comId`,t.`user_id` AS `__relation__userId` FROM `my_sign_up` t WHERE " + SignUpKeys4
                            + " ORDER BY t.`com_id` ASC,CASE WHEN t.`time` IS NULL THEN 1 ELSE 0 END ASC,t.`time` DESC");
        listen.AssertParams(1, SignUpParams4);
    });

    private static string UnionAllSegments(string paging) =>
        "SELECT t1.`id`,t1.`time`,t1.`content`,t1.`com_id` AS `__relation__comId`,t1.`user_id` AS `__relation__userId` FROM ( "
        + string.Join("  UNION ALL  ", Enumerable.Repeat("(SELECT t.`id`,t.`com_id`,t.`user_id`,t.`time`,t.`content` FROM `my_sign_up` t WHERE ((t.`com_id` =? AND t.`user_id` =?)) ORDER BY t.`com_id` ASC,CASE WHEN t.`time` IS NULL THEN 0 ELSE 1 END ASC,t.`time` DESC " + paging + ")", 4))
        + " ) t1";

    // DTO 上的 Limit：每个父对象一段 UNION ALL，段里按实体导航的排序取两条
    [Fact]
    public Task TestRelation_DtoNavigateLimit() => WithRelationDataAsync(async listen =>
    {
        var rows = (await Db.Query<MyComUser1>().Select<MyComUserDTO6>().ToListAsync(Ct)).ToDictionary(r => r.Gw!);
        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, ComUserMain);
        listen.AssertSql(1, UnionAllSegments("LIMIT 2"));
        listen.AssertParams(1, SignUpParams4);
        Assert.Equal(["s2", "s1"], rows["JAVA"].MySignUps!.Select(s => s.Id));
        Assert.Empty(rows["测试"].MySignUps!);
        Assert.Equal(["s3"], rows["C#"].MySignUps!.Select(s => s.Id));
    });

    // DTO 上的 Offset 与 Limit：跳过一条再取两条
    [Fact]
    public Task TestRelation_DtoNavigateOffsetLimit() => WithRelationDataAsync(async listen =>
    {
        var rows = (await Db.Query<MyComUser1>().Select<MyComUserDTO7>().ToListAsync(Ct)).ToDictionary(r => r.Gw!);
        listen.AssertSql(1, UnionAllSegments("LIMIT 2 OFFSET 1"));
        Assert.Equal(["s1"], rows["JAVA"].MySignUps!.Select(s => s.Id));
        Assert.Empty(rows["C#"].MySignUps!);
    });

    // 第 4 段：DTO 里的直达导航沿路径逐段带出（以前拿本表的键去查目标表，静默取错数据）
    [Fact]
    public Task TestRelation_DirectNavigationInDto() => WithRelationDataAsync(async listen =>
    {
        var rows = (await Db.Query<MySignUp>().Where(m => m.Id != null).Select<MySignUpDTO4>().ToListAsync(Ct)).ToDictionary(r => r.Id!);
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`user_id`,t.`time`,t.`content`,t.`com_id` AS `__relation__comId` FROM `my_sign_up` t WHERE t.`id` IS NOT NULL");
        listen.AssertSql(1, "SELECT `com_id`,`user_id` FROM `my_com_user` WHERE " + ComUserKeys);
        listen.AssertParams(1, "c1(String),u1(String),c1(String),u3(String),c2(String),u2(String)");
        listen.AssertSql(2, "SELECT t.`id`,t.`name` FROM `my_company_info` t WHERE t.`id` IN (?)");
        listen.AssertParams(2, "c1(String)");
        Assert.Equal("公司1", rows["s1"].ComUserCompany!.Name);
        // (c2, u2) 没有企业用户，到不了企业
        Assert.Null(rows["s4"].ComUserCompany);
    });
}