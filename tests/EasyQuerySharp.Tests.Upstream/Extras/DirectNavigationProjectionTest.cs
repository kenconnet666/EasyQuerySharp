namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 按结构映射到 DTO（<c>Select&lt;TDto&gt;()</c>）时，直达导航（DirectMapping）要沿路径逐段带出，与 Include 一致；
/// 以前直接拿本表的键去查目标表，静默取错数据。SQL 与 Java QueryTestRelationTest.testRelation 里 selectAutoInclude(MySignUpDTO4) 一段相同。
/// </summary>
public sealed class DirectNavigationProjectionTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    /// <summary>报名与它经企业用户到达的企业（Java 的 MySignUpDTO4）。</summary>
    public sealed class SignUpWithCompany
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

    // 与 Java QueryTestRelationTest 的 before() 相同的数据
    private async Task SeedAsync()
    {
        await ClearAsync();
        await Db.Insert(new MyUser { Id = "u1", Name = "小明", Age = 12 }, new MyUser { Id = "u2", Name = "小红", Age = 12 }, new MyUser { Id = "u3", Name = "小蓝", Age = 12 }).ExecuteAsync(Ct);
        await Db.Insert(new MyCompany { Id = "c1", Name = "公司1" }, new MyCompany { Id = "c2", Name = "公司2" }).ExecuteAsync(Ct);
        await Db.Insert(new MyComUser { Id = "cu1", ComId = "c1", UserId = "u1", Gw = "JAVA" }, new MyComUser { Id = "cu2", ComId = "c2", UserId = "u1", Gw = "测试" },
            new MyComUser { Id = "cu3", ComId = "c1", UserId = "u2", Gw = "VUE" }, new MyComUser { Id = "cu4", ComId = "c1", UserId = "u3", Gw = "C#" }).ExecuteAsync(Ct);
        await Db.Insert(new MySignUp { Id = "s1", ComId = "c1", UserId = "u1", Time = new DateTime(2024, 1, 1), Content = "写了一些代码1" },
            new MySignUp { Id = "s2", ComId = "c1", UserId = "u1", Time = new DateTime(2024, 2, 1), Content = "写了一些代码2" },
            new MySignUp { Id = "s3", ComId = "c1", UserId = "u3", Time = new DateTime(2024, 1, 1), Content = "写了一些代码3" },
            new MySignUp { Id = "s4", ComId = "c2", UserId = "u2", Time = new DateTime(2024, 1, 1), Content = "写了一些代码5" }).ExecuteAsync(Ct);
    }

    private async Task ClearAsync()
    {
        await Db.Delete<MySignUp>().AllowDeleteStatement(true).All().ExecuteAsync(Ct);
        await Db.Delete<MyComUser>().AllowDeleteStatement(true).All().ExecuteAsync(Ct);
        await Db.Delete<MyCompany>().AllowDeleteStatement(true).All().ExecuteAsync(Ct);
        await Db.Delete<MyUser>().AllowDeleteStatement(true).All().ExecuteAsync(Ct);
    }

    [Fact]
    public async Task DirectNavigationInDtoGoesThroughEachStep()
    {
        await SeedAsync();
        try
        {
            using var listen = SqlCapture.Listen();
            var rows = (await Db.Query<MySignUp>().Where(m => m.Id != null).Select<SignUpWithCompany>().ToListAsync(Ct)).ToDictionary(r => r.Id!);
            Assert.Equal(3, listen.Events.Count);
            listen.AssertSql(0, "SELECT t.`id`,t.`user_id`,t.`time`,t.`content`,t.`com_id` AS `__relation__comId` FROM `my_sign_up` t WHERE t.`id` IS NOT NULL");
            listen.AssertSql(1, "SELECT `com_id`,`user_id` FROM `my_com_user` WHERE ((`com_id` =? AND `user_id` =?) OR (`com_id` =? AND `user_id` =?) OR (`com_id` =? AND `user_id` =?))");
            listen.AssertParams(1, "c1(String),u1(String),c1(String),u3(String),c2(String),u2(String)");
            listen.AssertSql(2, "SELECT t.`id`,t.`name` FROM `my_company_info` t WHERE t.`id` IN (?)");
            listen.AssertParams(2, "c1(String)");
            Assert.Equal("公司1", rows["s1"].ComUserCompany!.Name);
            Assert.Equal("公司1", rows["s3"].ComUserCompany!.Name);
            // (c2, u2) 没有企业用户，到不了企业
            Assert.Null(rows["s4"].ComUserCompany);
        }
        finally
        {
            await ClearAsync();
        }
    }
}
