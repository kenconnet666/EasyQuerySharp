using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"原生 SQL"页：整条原生 SQL、可以继续组合的原生来源、查询里的原生片段、直接读 DbDataReader。</summary>
public static class RawSqlExamples
{
    /// <summary>按列名读出的行。</summary>
    public sealed class NameAge
    {
        public string Name { get; set; } = "";

        public int? Age { get; set; }
    }

    [Example("raw", "插值字符串：插值处都是参数，集合展开成多个参数")]
    public static Task<List<NameAge>> SqlQuery(EasyDb db)
    {
        var minAge = 30;
        long[] companies = [1, 2];
        return db.SqlQueryAsync<NameAge>($"SELECT name, age FROM t_user WHERE age >= {minAge} AND company_id IN ({companies}) ORDER BY id", CancellationToken.None);
    }

    [Example("raw", "读单列与标识符：Sql.Identifier 按方言加引号")]
    public static Task<List<long>> ScalarAndIdentifier(EasyDb db) =>
        db.SqlQueryAsync<long>($"SELECT COUNT(*) FROM {Sql.Identifier("t_post")} WHERE {Sql.Identifier("views")} >= {1000}", CancellationToken.None);

    [Example("raw", "执行写入：ExecuteSqlAsync 返回影响的行数")]
    public static Task<long> ExecuteSql(EasyDb db) =>
        db.ExecuteSqlAsync($"UPDATE t_product SET stock = stock - {1} WHERE id = {5}", CancellationToken.None);

    [Example("raw", "? 占位的位置参数")]
    public static Task<List<string>> Positional(EasyDb db) =>
        db.SqlQueryAsync<string>("SELECT name FROM t_tag WHERE id > ? ORDER BY id", [4L]);

    [Example("raw", "原生 SQL 当作查询来源：FromSql 之后继续 Where / Select")]
    public static Task<List<string>> FromSql(EasyDb db) =>
        db.FromSql<User>("SELECT * FROM t_user WHERE age >= ?", 30).Where(u => u.CompanyId == 1).OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();

    [Example("raw", "原生排序与选列：OrderByRaw、SelectRaw")]
    public static Task<List<Post>> RawOrderAndSelect(EasyDb db) =>
        db.Query<Post>().Where(p => p.Views > 0).OrderByRaw("views % 7").SelectRaw("id, title, views").Take(3).ToListAsync();

    [Example("raw", "直接读 DbDataReader：ReadAsync")]
    public static Task<List<string>> ReadAsync(EasyDb db) =>
        db.Query<Company>().OrderBy(c => c.Id).Select(c => c.Name).ReadAsync(async (reader, ct) =>
        {
            var names = new List<string>();
            while (await reader.ReadAsync(ct)) names.Add(reader.GetString(0));
            return names;
        });

    [Example("raw", "只生成 SQL 不执行：ToSql / ToStatement")]
    public static Task<string> ToSql(EasyDb db) =>
        Task.FromResult(db.Query<User>().Where(u => u.Age >= 30).Select(u => u.Name).ToSql());
}
