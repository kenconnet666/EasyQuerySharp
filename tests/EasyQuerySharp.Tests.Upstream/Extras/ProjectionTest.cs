using System.Linq.Expressions;
using EasyQuerySharp.Tests.Upstream.Mysql8;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 可复用投影（ROADMAP 4.6，没有对应的 Java 用例）：<c>Project(map)</c> 与 <c>[Projection]</c> 方法内联成方法体，
/// 生成的 SQL 与把投影写在原地完全相同；对多的投影照常走带出查询。
/// </summary>
public sealed class ProjectionTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    public sealed record BankVo(string? Id, string? Name);

    public sealed record CardVo(string? Code, BankVo Bank);

    public sealed record UserVo(string? Id, List<CardVo> Cards);

    [Fact]
    public void ProjectionMethodInlinesIntoTheSameSql()
    {
        var inline = Db.Query<SysBankCard>().Select(c => new CardVo(c.Code, new BankVo(c.Bank!.Id, c.Bank.Name)));
        Assert.Equal(inline.ToSql(), Db.Query<SysBankCard>().Select(c => c.ToVo()).ToSql());
    }

    [Fact]
    public void ProjectOnToOneNavigation()
    {
        var inline = Db.Query<SysBankCard>().Select(c => new BankVo(c.Bank!.Id, c.Bank.Name));
        Assert.Equal(inline.ToSql(), Db.Query<SysBankCard>().Select(c => c.Bank!.Project(Vos.BankMap)).ToSql());
    }

    // 条件里的投影同样内联
    [Fact]
    public void ProjectInPredicate()
    {
        var inline = Db.Query<SysBankCard>().Where(c => c.Bank!.Name == "工商银行");
        Assert.Equal(inline.ToSql(), Db.Query<SysBankCard>().Where(c => c.Bank!.ToVo().Name == "工商银行").ToSql());
    }

    // 对多：Project 等同于 Select，按父键批量带出；与原地写法结果相同
    [Fact]
    public async Task ProjectOnCollectionLoadsLikeInlineSelect()
    {
        var projected = await Db.Query<Mysql8.SysUser>().OrderBy(u => u.Id).Select(u => new UserVo(u.Id, u.BankCards!.Project(Vos.CardMap).ToList())).ToListAsync(Ct);
        var inline = await Db.Query<Mysql8.SysUser>().OrderBy(u => u.Id)
            .Select(u => new UserVo(u.Id, u.BankCards!.Select(c => new CardVo(c.Code, new BankVo(c.Bank!.Id, c.Bank.Name))).ToList())).ToListAsync(Ct);
        Assert.Equal(Format(inline), Format(projected));
        Assert.Contains(projected, u => u.Cards.Count > 0);

        static string Format(List<UserVo> users) =>
            string.Join("|", users.Select(u => u.Id + ":" + string.Join(",", u.Cards.OrderBy(c => c.Code).Select(c => c.Code + "@" + c.Bank.Name))));
    }


    /// <summary>[MapFrom] 的 DTO：Select&lt;TDto&gt;() 按结构映射并带出导航。</summary>
    [EasyQuerySharp.Metadata.MapFrom<Mysql8.SysUser>]
    public sealed class UserCardsVo
    {
        public string? Id { get; set; }

        public List<SysBankCard>? BankCards { get; set; }
    }

    [Fact]
    public async Task SelectOfMapFromDtoIncludesNavigationsAndRequiresItsEntity()
    {
        var list = await Db.Query<Mysql8.SysUser>().Select<UserCardsVo>().ToListAsync(Ct);
        Assert.Contains(list, u => u.BankCards is { Count: > 0 });
        Assert.Throws<InvalidOperationException>(() => Db.Query<SysBankCard>().Select<UserCardsVo>());
    }
    // 在内存里调用就是普通方法
    [Fact]
    public void ProjectionMethodRunsInMemory() =>
        Assert.Equal(new BankVo("1", "工商银行"), new SysBank { Id = "1", Name = "工商银行" }.ToVo());
}

/// <summary>测试用的可复用投影。</summary>
public static partial class Vos
{
    public static readonly Expression<Func<SysBank, ProjectionTest.BankVo>> BankMap = b => new ProjectionTest.BankVo(b.Id, b.Name);

    public static readonly Expression<Func<SysBankCard, ProjectionTest.CardVo>> CardMap = c => new ProjectionTest.CardVo(c.Code, c.Bank!.Project(BankMap));

    [Projection]
    public static ProjectionTest.BankVo ToVo(this SysBank b) => new(b.Id, b.Name);

    [Projection]
    public static ProjectionTest.CardVo ToVo(this SysBankCard c) => new(c.Code, c.Bank!.ToVo());
}
