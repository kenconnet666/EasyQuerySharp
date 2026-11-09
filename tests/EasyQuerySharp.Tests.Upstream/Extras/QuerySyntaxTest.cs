namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// LINQ 查询语法（<c>from … where … orderby … join … group … select</c>）的支持测试（没有对应的 Java 用例）：
/// 每种写法都与等价的方法链生成同样的 SQL，并在实库上能执行。
/// </summary>
public sealed class QuerySyntaxTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact]
    public async Task WhereOrderBySelect()
    {
        var syntax = from t in Db.Query<Topic>()
                     where t.Stars > 1 && t.Title != null
                     orderby t.Stars descending, t.Id
                     select new { t.Id, t.Title };
        var method = Db.Query<Topic>().Where(t => t.Stars > 1 && t.Title != null).OrderByDescending(t => t.Stars).ThenBy(t => t.Id).Select(t => new { t.Id, t.Title });
        Assert.Equal(method.ToSql(), syntax.ToSql());
        Assert.NotEmpty(await syntax.ToListAsync(Ct));
    }

    [Fact]
    public async Task Join()
    {
        var syntax = from t in Db.Query<Topic>()
                     join b in Db.Query<BlogEntity>() on t.Id equals b.Id
                     select new { t.Id, b.Title, t.Stars };
        var method = Db.Query<Topic>().InnerJoin<BlogEntity>((t, b) => t.Id == b.Id).Select((t, b) => new { t.Id, b.Title, t.Stars });
        Assert.Equal(method.ToSql(), syntax.ToSql());
        Assert.NotEmpty(await syntax.ToListAsync(Ct));
    }

    // 复合键：new { … } equals new { … } 按成员逐一相等
    [Fact]
    public void JoinOnCompositeKey()
    {
        var syntax = from t in Db.Query<Topic>()
                     join b in Db.Query<BlogEntity>() on new { t.Id, t.Title } equals new { b.Id, b.Title }
                     select new { t.Id, b.Url };
        var method = Db.Query<Topic>().InnerJoin<BlogEntity>((t, b) => t.Id == b.Id && t.Title == b.Title).Select((t, b) => new { t.Id, b.Url });
        Assert.Equal(method.ToSql(), syntax.ToSql());
    }

    // join 后再 where：结果是投影（透明标识符），条件加在外层
    [Fact]
    public async Task JoinThenWhere()
    {
        var rows = await (from t in Db.Query<Topic>()
                          join b in Db.Query<BlogEntity>() on t.Id equals b.Id
                          where t.Stars > 1
                          select new { t.Id, b.Title }).ToListAsync(Ct);
        Assert.NotEmpty(rows);
    }

    [Fact]
    public async Task GroupBy()
    {
        var syntax = from t in Db.Query<Topic>()
                     group t by t.Title into g
                     select new { Title = g.Key, Count = g.Count() };
        var method = Db.Query<Topic>().GroupBy(t => t.Title).Select(g => new { Title = g.Key, Count = g.Count() });
        Assert.Equal(method.ToSql(), syntax.ToSql());
        Assert.NotEmpty(await syntax.ToListAsync(Ct));
    }
}
