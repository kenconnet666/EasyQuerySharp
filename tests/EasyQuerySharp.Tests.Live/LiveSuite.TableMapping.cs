using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Live;

// 查询级换表（MapTable）：主表、条件里的集合导航、投影里的计数、Include 与 DTO 自动带出都读映射的表
public abstract partial class LiveSuite
{
    /// <summary>归档的用户：与 <see cref="User"/> 同结构的另一张表（只用来建表）。</summary>
    [Table("t_user_archive")]
    public sealed class UserArchive : User;

    /// <summary>归档的帖子：与 <see cref="Post"/> 同结构的另一张表（只用来建表）。</summary>
    [Table("t_post_archive")]
    public sealed class PostArchive : Post;

    /// <summary>归档用户的 DTO：帖子按导航自动带出。</summary>
    public sealed class ArchivedUser
    {
        public string Name { get; set; } = "";

        public List<ArchivedPost>? Posts { get; set; }
    }

    /// <summary>归档帖子的 DTO。</summary>
    public sealed class ArchivedPost
    {
        public string Title { get; set; } = "";
    }

    // dave 和他的两个帖子只在归档表里；映射到归档表后各条路径都读归档表，原表不受影响
    [Fact, Upstream("fork/live/TableNameMappingLiveTest.java#includeAndImplicitSubqueryReadArchiveTables")]
    public async Task IncludeAndImplicitSubqueryReadArchiveTables()
    {
        var db = await SeededAsync();
        Type[] archives = [typeof(PostArchive), typeof(UserArchive)];
        await db.CodeFirst.DropTablesIfExistsAsync(archives, Ct);
        await db.CodeFirst.CreateTablesAsync(archives.Reverse(), Ct);
        try
        {
            var dave = new User { Name = "dave", Age = 40, Attrs = new UserAttrs([]), CreatedAt = CreatedAt };
            await db.Insert(dave).AsTable("t_user_archive").FillGeneratedKeys().ExecuteAsync(Ct);
            await db.Insert(new Post { UserId = dave.Id, Title = "archived orm", Amount = 1m }, new Post { UserId = dave.Id, Title = "archived sql", Amount = 2m })
                .AsTable("t_post_archive").ExecuteAsync(Ct);
            var archive = db.Query<User>().MapTable<User>("t_user_archive").MapTable<Post>("t_post_archive");

            var counted = await archive.Where(u => u.Posts!.Any(p => p.Title.StartsWith("archived"))).Select(u => new { u.Name, Posts = u.Posts!.Count() }).ToListAsync(Ct);
            Assert.Equal([("dave", 2)], counted.Select(c => (c.Name, c.Posts)));

            var included = Assert.Single(await archive.Include(u => u.Posts).ToListAsync(Ct));
            Assert.Equal(["archived orm", "archived sql"], included.Posts!.Select(p => p.Title).Order(StringComparer.Ordinal));

            var projected = Assert.Single(await archive.Select<ArchivedUser>().ToListAsync(Ct));
            Assert.Equal(("dave", 2), (projected.Name, projected.Posts!.Count));

            // 只映射帖子：用户读原表，帖子读归档表；不映射时照旧读原表
            var tips = db.Query<User>().Where(u => u.Posts!.Any(p => p.Title == "orm tips"));
            Assert.Equal(0, await tips.MapTable<Post>("t_post_archive").CountAsync(Ct));
            Assert.Equal(1, await tips.CountAsync(Ct));
        }
        finally
        {
            await db.CodeFirst.DropTablesIfExistsAsync(archives, Ct);
        }
    }
}
