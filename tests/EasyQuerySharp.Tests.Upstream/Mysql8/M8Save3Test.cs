using EasyQuerySharp.Commands;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8Save3Test.java（A → B → C → D 一对一链上的归属变更、置空删除、多对多默认级联与 forEach）。</summary>
public sealed class M8Save3Test(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private async Task DeleteAll()
    {
        await Db.Delete<M8SaveA>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveB>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveC>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveD>().Where(o => o.Id != null).ExecuteAsync(Ct);
    }

    // a → b → c → d，主键与名字都取给定值
    private static M8SaveA Create(string a, string b, string c, string d) => new()
    {
        Id = a, Name = a,
        M8SaveB = new M8SaveB { Id = b, Name = b, M8SaveC = new M8SaveC { Id = c, Name = c, M8SaveD = new M8SaveD { Id = d, Name = d } } },
    };

    private async Task InsertOne()
    {
        using var tracking = Db.BeginTracking();
        await Save(s => s, Create("1", "2", "3", "4"), Create("5", "6", "7", "8"), Create("9", "10", "11", "12"));
    }

    private async Task Invoke(Func<SqlCapture.Scope, Task> action)
    {
        using var tracking = Db.BeginTracking();
        using var listen = SqlCapture.Listen();
        await action(listen);
    }

    private async Task Save(Func<SaveCommand<M8SaveA>, SaveCommand<M8SaveA>> configure, params M8SaveA[] roots)
    {
        await using var tx = Db.BeginTransaction();
        await configure(Db.Save(roots)).ExecuteAsync(Ct);
        await tx.CommitAsync(Ct);
    }

    private Query<M8SaveA> WithChain() =>
        Db.Query<M8SaveA>().Include(a => a.M8SaveB, b => b.Include(x => x.M8SaveC, c => c.Include(y => y.M8SaveD)));

    // 三条链交叉交换 b、c、d：只改外键（d4 另改了名字）
    private async Task SwapAcrossChains()
    {
        await DeleteAll();
        await InsertOne();
        await Invoke(async listen =>
        {
            var a1 = await WithChain().WhereById("1").SingleAsync(Ct);
            var a5 = await WithChain().WhereById("5").SingleAsync(Ct);
            var a9 = await WithChain().WhereById("9").SingleAsync(Ct);

            var (b2, b6) = (a1.M8SaveB!, a5.M8SaveB!);
            a1.M8SaveB = b6;
            a5.M8SaveB = b2;
            var c7 = b6.M8SaveC!;
            var b10 = a9.M8SaveB!;
            var c11 = b10.M8SaveC!;
            b6.M8SaveC = c11;
            b10.M8SaveC = c7;

            var c3 = b2.M8SaveC!;
            var d4 = c3.M8SaveD!;
            d4.Name = "myd4";
            var d12 = c11.M8SaveD!;
            c3.M8SaveD = d12;
            c11.M8SaveD = d4;

            await Save(s => s.With(SaveBehavior.AllowOwnershipChange), a1, a5, a9);
            Assert.Equal(18, listen.Events.Count);
            listen.AssertSql(12, "UPDATE `m8_save_b` SET `pid` = ? WHERE `id` = ?");
            listen.AssertParams(12, "1(String),6(String)");
            listen.AssertSql(13, "UPDATE `m8_save_b` SET `pid` = ? WHERE `id` = ?");
            listen.AssertParams(13, "5(String),2(String)");
            listen.AssertSql(14, "UPDATE `m8_save_c` SET `pid` = ? WHERE `id` = ?");
            listen.AssertParams(14, "6(String),11(String)");
            listen.AssertSql(15, "UPDATE `m8_save_c` SET `pid` = ? WHERE `id` = ?");
            listen.AssertParams(15, "10(String),7(String)");
            listen.AssertSql(16, "UPDATE `m8_save_d` SET `pid` = ?,`name` = ? WHERE `id` = ?");
            listen.AssertParams(16, "11(String),myd4(String),4(String)");
            listen.AssertSql(17, "UPDATE `m8_save_d` SET `pid` = ? WHERE `id` = ?");
            listen.AssertParams(17, "3(String),12(String)");
        });
    }

    // 写法与 Java 不同、SQL 一致：Java 的 include2 写成嵌套 Include(a => a.M8SaveB, b => b.Include(...))
    [Fact, Upstream("mysql8/M8Save3Test.java#testOwnershipChange1")]
    public Task TestOwnershipChange1() => SwapAcrossChains();

    // 写法与 Java 不同、SQL 一致：Java 的 include2 写成嵌套 Include(a => a.M8SaveB, b => b.Include(...))
    [Fact, Upstream("mysql8/M8Save3Test.java#testOwnershipChange1_1")]
    public Task TestOwnershipChange1_1() => SwapAcrossChains();

    // 一对一置空：整条链按级联删除，最深的先删
    // 写法与 Java 不同、SQL 一致：Java 的 include2 写成嵌套 Include(a => a.M8SaveB, b => b.Include(...))
    [Fact, Upstream("mysql8/M8Save3Test.java#testOwnershipChange2")]
    public async Task TestOwnershipChange2()
    {
        await DeleteAll();
        await InsertOne();
        await Invoke(async listen =>
        {
            var a1 = await WithChain().WhereById("1").SingleAsync(Ct);
            a1.M8SaveB = null;
            await Save(s => s.With(SaveBehavior.AllowOwnershipChange), a1);
            Assert.Equal(7, listen.Events.Count);
            listen.AssertSql(4, "DELETE FROM `m8_save_d` WHERE `id` = ?");
            listen.AssertParams(4, "4(String)");
            listen.AssertSql(5, "DELETE FROM `m8_save_c` WHERE `id` = ?");
            listen.AssertParams(5, "3(String)");
            listen.AssertSql(6, "DELETE FROM `m8_save_b` WHERE `id` = ?");
            listen.AssertParams(6, "2(String)");
        });
    }

    // b2 置空、c3 挪到 b6 下：b2 与 c3 原来的 d4、b6 原来的 c7 连同 d8 删除，c3 只改外键
    // 写法与 Java 不同、SQL 一致：Java 的 include2 写成嵌套 Include(a => a.M8SaveB, b => b.Include(...))
    [Fact, Upstream("mysql8/M8Save3Test.java#testOwnershipChange3")]
    public async Task TestOwnershipChange3()
    {
        await DeleteAll();
        await InsertOne();
        await Invoke(async listen =>
        {
            var list = await WithChain().ToListAsync(Ct);
            var a1 = list.Single(o => o.Id == "1");
            var a5 = list.Single(o => o.Id == "5");
            var b2 = a1.M8SaveB!;
            a1.M8SaveB = null;
            var c3 = b2.M8SaveC!;
            c3.M8SaveD = null;
            a5.M8SaveB!.M8SaveC = c3;

            await Save(s => s.With(SaveBehavior.AllowOwnershipChange), [.. list]);
            Assert.Equal(9, listen.Events.Count);
            listen.AssertSql(4, "DELETE FROM `m8_save_d` WHERE `id` = ?");
            listen.AssertParams(4, "4(String)");
            listen.AssertSql(5, "DELETE FROM `m8_save_d` WHERE `id` = ?");
            listen.AssertParams(5, "8(String)");
            listen.AssertSql(6, "DELETE FROM `m8_save_c` WHERE `id` = ?");
            listen.AssertParams(6, "7(String)");
            listen.AssertSql(7, "DELETE FROM `m8_save_b` WHERE `id` = ?");
            listen.AssertParams(7, "2(String)");
            listen.AssertSql(8, "UPDATE `m8_save_c` SET `pid` = ? WHERE `id` = ?");
            listen.AssertParams(8, "6(String),3(String)");
        });
    }

    // 多对多默认级联不维护中间表：只插入两边的实体
    [Fact, Upstream("mysql8/M8Save3Test.java#many2manySetNullInsert")]
    public async Task Many2manySetNullInsert()
    {
        await Db.Delete<M8User3>().Where(m => m.UserId != null).ExecuteAsync(Ct);
        await Db.Delete<M8Role3>().Where(m => m.RoleId != null).ExecuteAsync(Ct);
        await Db.Delete<M8UserRole3>().Where(m => m.UserRoleId != null).ExecuteAsync(Ct);
        await Invoke(async listen =>
        {
            var role = new M8Role3 { RoleId = "r1", RoleName = "r1" };
            var user = new M8User3 { UserId = "u1", UserAge = 20, UserName = "u1", CreateTime = new DateTime(2020, 1, 1, 1, 1, 0), Roles = [role] };
            await using (var tx = Db.BeginTransaction())
            {
                await Db.Save(role).ExecuteAsync(Ct);
                await Db.Save(user).ExecuteAsync(Ct);
                await tx.CommitAsync(Ct);
            }
            Assert.Equal(2, listen.Events.Count);
            listen.AssertSql(0, "INSERT INTO `m8_role3` (`role_id`,`role_name`) VALUES (?,?)");
            listen.AssertParams(0, "r1(String),r1(String)");
            listen.AssertSql(1, "INSERT INTO `m8_user3` (`user_id`,`user_name`,`user_age`,`create_time`) VALUES (?,?,?,?)");
            listen.AssertParams(1, "u1(String),u1(String),20(Integer),2020-01-01T01:01(LocalDateTime)");
        });
    }

    // 行回调抛异常：带出查询不再执行
    // 写法与 Java 不同、SQL 一致：Java 的 include2 写成嵌套 Include(a => a.M8SaveB, b => b.Include(...))
    [Fact, Upstream("mysql8/M8Save3Test.java#testForeach")]
    public async Task TestForeach()
    {
        await DeleteAll();
        await InsertOne();
        await Invoke(async listen =>
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => WithChain().WhereById("1")
                .ForEach(item =>
                {
                    if (item.Id == "1") throw new InvalidOperationException("123123");
                })
                .SingleAsync(Ct));
            Assert.Equal("123123", ex.Message);
            Assert.Single(listen.Events);
            listen.AssertSql(0, "SELECT `id`,`name` FROM `m8_save_a` WHERE `id` = ?");
            listen.AssertParams(0, "1(String)");
        });
    }
}
