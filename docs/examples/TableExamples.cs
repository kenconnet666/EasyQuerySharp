using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"换表"页的卡片：查询级的表名映射、schema、表前面的连接符。与 TS 版 scale/tables 的动态表名一节对应。</summary>
public static class TableExamples
{
    /// <summary>作者在归档表里的帖子数。</summary>
    public sealed record ArchivedPosts(string Name, int Posts);

    [Example("tables", "换表：条件与投影里的集合导航跟着读归档表")]
    public static async Task<List<ArchivedPosts>> MapTable(EasyDb db)
    {
        await db.ExecuteSqlRawAsync("CREATE TABLE t_post_2025 AS SELECT * FROM t_post WHERE id <= 2");
        try
        {
            return await db.Query<User>().MapTable<Post>("t_post_2025")
                .Where(u => u.Posts!.Any(p => p.Views >= 100))
                .OrderBy(u => u.Id)
                .Select(u => new ArchivedPosts(u.Name, u.Posts!.Count()))
                .ToListAsync();
        }
        finally
        {
            await db.ExecuteSqlRawAsync("DROP TABLE t_post_2025");
        }
    }

    /// <summary>帖子标题与作者。</summary>
    public sealed record PostByAuthor(string Title, string? Author);

    [Example("tables", "整条查询换 schema：主表与隐式 JOIN 都放到同一个 schema 下")]
    public static Task<List<PostByAuthor>> InSchema(EasyDb db) =>
        db.Query<Post>().InSchema("main").Where(p => p.Views >= 1000).OrderBy(p => p.Id).Select(p => new PostByAuthor(p.Title, p.Author!.Name)).ToListAsync();

    [Example("tables", "改写表前面的连接符：LEFT JOIN 写成 LEFT OUTER JOIN")]
    public static Task<List<PostByAuthor>> TableLink(EasyDb db) =>
        db.Query<Post>().LeftJoin<User>((p, u) => p.AuthorId == u.Id).WithTableLink("LEFT OUTER JOIN")
            .Where((p, _) => p.Views >= 1000).OrderBy((p, _) => p.Id).Select((p, u) => new PostByAuthor(p.Title, u.Name)).ToListAsync();
}
