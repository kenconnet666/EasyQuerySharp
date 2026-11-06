using System.Linq.Expressions;

namespace EasyQuerySharp.Tests.Live;

// 视图（TS 的 defineView）：可复用的投影写成 [Projection] 方法或表达式（Project(map)），可以嵌套、可以放进别的投影里，
// 都内联成 SQL；按结构映射的 DTO 用 Select<TDto>()
public abstract partial class LiveSuite
{
    /// <summary>帖子卡片。</summary>
    public sealed record PostCard(string Title, string? Owner);

    /// <summary>用户卡片：嵌套第一篇帖子的卡片。</summary>
    public sealed record UserCard(string Name, string? Company, List<PostCard> Posts);

    private static readonly Expression<Func<Post, PostCard>> PostCardMap = p => new PostCard(p.Title, p.User!.Name);

    private static readonly Expression<Func<User, UserCard>> UserCardMap =
        u => new UserCard(u.Name, u.Company!.Name, u.Posts!.OrderBy(p => p.Id).Take(1).Project(PostCardMap).ToList());

    private static readonly (string, string?, (string, string?)[])[] ExpectedUserCards =
    [
        ("alice", "acme", [("orm tips", "alice")]),
        ("bob", "acme", [("orm_v2", "bob")]),
        ("carol", null, []),
    ];

    private static (string, string?, (string, string?)[]) Flatten(UserCard card) =>
        (card.Name, card.Company, card.Posts.Select(p => (p.Title, p.Owner)).ToArray());

    // 表达式写的视图：嵌套另一个视图（每个用户的第一篇帖子），也能和别的列一起放进投影
    [Fact]
    public async Task ReusesAndNestsProjectionExpressions()
    {
        var db = await SeededAsync();
        var cards = await db.Query<User>().OrderBy(u => u.Id).Select(UserCardMap).ToListAsync(Ct);
        Assert.Equal(ExpectedUserCards, cards.Select(Flatten));

        var withId = await db.Query<Post>().Where(p => p.Id == 4).Select(p => new { Card = p.Project(PostCardMap), p.Id }).ToListAsync(Ct);
        Assert.Equal([(new PostCard("hello", "bob"), 4L)], withId.Select(r => (r.Card, r.Id)));
    }

    // [Projection] 方法写的视图：查询里内联成方法体（嵌套调用也展开），在内存里调用就是普通方法
    [Fact]
    public async Task ReusesAndNestsProjectionMethods()
    {
        var db = await SeededAsync();
        var cards = await db.Query<User>().OrderBy(u => u.Id).Select(u => u.ToCard()).ToListAsync(Ct);
        Assert.Equal(ExpectedUserCards, cards.Select(Flatten));

        var titles = await db.Query<Post>().Where(p => p.User!.ToCard().Company == "acme" && p.Amount > 5).OrderBy(p => p.Id).Select(p => p.ToCard().Title).ToListAsync(Ct);
        Assert.Equal(["orm tips", "sql 100%"], titles);
        Assert.Equal(new PostCard("t", "x"), new Post { Title = "t", User = new User { Name = "x" } }.ToCard());
    }

    /// <summary>按结构映射的视图：同名属性取列，与导航同名的列表属性沿导航带出。</summary>
    public sealed class UserView
    {
        public string Name { get; set; } = "";

        public List<PostView>? Posts { get; set; }
    }

    /// <summary>帖子视图。</summary>
    public sealed class PostView
    {
        public string Title { get; set; } = "";

        public decimal Amount { get; set; }
    }

    [Fact]
    public async Task ProjectsToDtosByStructure()
    {
        var db = await SeededAsync();
        var users = await db.Query<User>().Where(u => u.CompanyId != null).OrderBy(u => u.Id).Select<UserView>().ToListAsync(Ct);
        Assert.Equal(["alice", "bob"], users.Select(u => u.Name));
        Assert.Equal([("orm tips", 10.50m), ("sql 100%", 2000.00m)], users[0].Posts!.OrderBy(p => p.Amount).Select(p => (p.Title, p.Amount)));
        Assert.Equal(2, users[1].Posts!.Count);
    }
}

/// <summary>视图一组的 [Projection] 方法（扩展方法要放在非嵌套的静态类里）。</summary>
public static partial class LiveViews
{
    /// <summary>帖子卡片。</summary>
    [Projection]
    public static LiveSuite.PostCard ToCard(this Post p) => new(p.Title, p.User!.Name);

    /// <summary>用户卡片：嵌套调用帖子卡片。</summary>
    [Projection]
    public static LiveSuite.UserCard ToCard(this User u) => new(u.Name, u.Company!.Name, u.Posts!.OrderBy(p => p.Id).Take(1).Select(p => p.ToCard()).ToList());
}
