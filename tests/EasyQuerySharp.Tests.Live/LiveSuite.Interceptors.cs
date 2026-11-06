using System.Linq.Expressions;
using EasyQuerySharp.Interceptors;

namespace EasyQuerySharp.Tests.Live;

// upsert 与拦截器：指定更新列与冲突列的 upsert、查询过滤作用于根表 / 连接 / 子查询 / 写入、写入前填值
public abstract partial class LiveSuite
{
    // 只更新指定的列：冲突时 Remark 不动；不指定更新列则整行（除主键）覆盖；忽略时保留已有的行
    [Fact]
    public async Task UpsertsOnlyTheChosenColumns()
    {
        var db = await SeededAsync();
        await db.Insert(new Setting { Code = "k", Value = "v1", Remark = "r1" }).ExecuteAsync(Ct);

        await db.Insert(new Setting { Code = "k", Value = "v2", Remark = "r2" }).OnConflictUpdate(s => s.Value, s => s.Code).ExecuteAsync(Ct);
        var row = await db.Query<Setting>().WhereById("k").SingleAsync(Ct);
        Assert.Equal(("v2", "r1"), (row.Value, row.Remark));

        await db.Insert(new Setting { Code = "k", Value = "v3", Remark = "r3" }).OnConflictUpdate().ExecuteAsync(Ct);
        row = await db.Query<Setting>().WhereById("k").SingleAsync(Ct);
        Assert.Equal(("v3", "r3"), (row.Value, row.Remark));

        await db.Insert(new Setting { Code = "k", Value = "ignored" }).OnConflictIgnore(s => s.Code).ExecuteAsync(Ct);
        Assert.Equal("v3", (await db.Query<Setting>().WhereById("k").SingleAsync(Ct)).Value);
    }

    // 只看成年人的查询过滤：根表、LEFT JOIN 的导航、子查询、对象树里的集合、写入都带上，DisableInterceptor 按名字关掉
    [Fact]
    public async Task AppliesFiltersToRootJoinsSubqueriesAndWrites()
    {
        var db = await SeededAsync(o => o.AddInterceptor(new AdultsFilter()));
        Assert.Equal(1, await db.Query<User>().CountAsync(Ct));
        Assert.Equal(3, await db.Query<User>().DisableInterceptor("adults").CountAsync(Ct));

        // LEFT JOIN 的条件进 ON：帖子都在，bob 的帖子作者读成 null
        var authors = await db.Query<Post>().OrderBy(p => p.Id).Select(p => new Draft<string?>(p.User!.Name)).ToListAsync(Ct);
        Assert.Equal(["alice", "alice", null, null], authors.Select(a => a.Value1));

        // 子查询（Count）与对象树里的集合（Users）都只算成年人
        var companies = await db.Query<Company>().OrderBy(c => c.Id)
            .Select(c => new CompanyUsers(c.Users!.Count(), c.Users!.Select(u => u.Name).ToList()))
            .ToListAsync(Ct);
        Assert.Equal([(1L, new[] { "alice" }), (0L, [])], companies.Select(c => (c.Count, c.Names.ToArray())));

        // 写入同样被过滤：只有 alice 被更新，bob 的年龄还是 NULL
        Assert.Equal(1, await db.Update<User>().Set(u => u.Age, 50).All().ExecuteAsync(Ct));
        var bob = await db.Query<User>().DisableInterceptor("adults").Where(u => u.Name == "bob").SingleAsync(Ct);
        Assert.Null(bob.Age);
    }

    private sealed record CompanyUsers(long Count, List<string> Names);

    private sealed class AdultsFilter : QueryFilter<User>
    {
        public override string Name => "adults";

        public override Expression<Func<User, bool>> Filter() => u => u.Age >= 18;
    }

    // 插入前给没填的年龄补默认值；按条件更新时追加 SET（TS 的 touch）
    [Fact]
    public async Task FillsValuesBeforeInsertAndUpdate()
    {
        var db = await SeededAsync(o => o.AddInterceptor(new DefaultAge()).AddInterceptor(new TouchTitle()));
        await db.Insert(new User { Name = "dave", Attrs = new UserAttrs([]), CreatedAt = DateTime.UtcNow }).ExecuteAsync(Ct);
        Assert.Equal(1, (await db.Query<User>().Where(u => u.Name == "dave").SingleAsync(Ct)).Age);

        await db.Update<Post>().Set(p => p.Amount, 9.00m).Where(p => p.Id == 1).ExecuteAsync(Ct);
        Assert.Equal("touched", (await db.Query<Post>().FindAsync(1L, Ct))!.Title);
    }

    private sealed class DefaultAge : EntityInterceptor<User>
    {
        public override void BeforeInsert(User entity) => entity.Age ??= 1;
    }

    private sealed class TouchTitle : EntityInterceptor<Post>
    {
        public override void BeforeConditionUpdate(UpdateSetBuilder<Post> sets) => sets.Set(p => p.Title, "touched");
    }
}
