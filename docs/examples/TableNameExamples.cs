using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"换表"页（tables 之外）：单张表的 AsTable、整个表引用的 AsTableSegment、写入时换表与 schema。</summary>
public static class TableNameExamples
{
    [Example("tables/as-table", "这条查询的主表换成另一张：AsTable")]
    public static async Task<List<string>> AsTable(EasyDb db)
    {
        await db.ExecuteSqlAsync("CREATE TABLE t_tag_2025 AS SELECT * FROM t_tag");
        try
        {
            return await db.Query<Tag>().AsTable("t_tag_2025").Where(t => t.Id <= 3).OrderBy(t => t.Id).Select(t => t.Name).ToListAsync();
        }
        finally
        {
            await db.ExecuteSqlAsync("DROP TABLE t_tag_2025");
        }
    }

    [Example("tables/as-table", "按原表名推出新表名；导航上也能换：Sql.AsTable")]
    public static async Task<List<string>> AsTableByName(EasyDb db)
    {
        await db.ExecuteSqlAsync("CREATE TABLE t_user_archive AS SELECT * FROM t_user");
        try
        {
            var archived = await db.Query<User>().AsTable(name => name + "_archive").Where(u => u.Age >= 35).Select(u => u.Name).ToListAsync();
            var posts = await db.Query<Post>().Where(p => Sql.AsTable(p.Author, "t_user_archive")!.Age >= 35).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();
            return [.. archived, .. posts];
        }
        finally
        {
            await db.ExecuteSqlAsync("DROP TABLE t_user_archive");
        }
    }

    [Example("tables/as-table", "整个表引用自己写：AsTableSegment")]
    public static Task<List<string>> AsTableSegment(EasyDb db) =>
        db.Query<Post>().AsTableSegment((table, alias) => $"(SELECT * FROM {table} WHERE status = 'published') {alias}")
            .Where(p => p.Views >= 2000).OrderBy(p => p.Id).Select(p => p.Title).ToListAsync();

    [Example("tables/as-table", "写入时换表、换 schema")]
    public static async Task<long> WriteToOtherTable(EasyDb db)
    {
        await db.ExecuteSqlAsync("CREATE TABLE t_tag_archive AS SELECT * FROM t_tag WHERE 1 = 0");
        try
        {
            await db.Insert(new Tag { Id = 100, Name = "归档" }).AsTable("t_tag_archive").ExecuteAsync();
            return await db.Update<Tag>().AsSchema("main").Set(t => t.Name, "ORM 框架").Where(t => t.Id == 1).ExecuteAsync();
        }
        finally
        {
            await db.ExecuteSqlAsync("DROP TABLE t_tag_archive");
        }
    }
}
