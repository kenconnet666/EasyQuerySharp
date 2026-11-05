using EasyQuerySharp.Filtering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>
/// 对应 Java：QueryTest21.java（实体判空、CASE WHEN、分页计数与深分页反向排序、分组里的条件聚合、按路径动态取列与排序、COUNT(DISTINCT)）。
/// Java 的 anyColumn("user.age") 对应 C# 的 <c>Sql.Column&lt;T&gt;(c, "user.age")</c>；动态排序请求对应 <c>SortBy(ISortRequest)</c>。
/// </summary>
public sealed class QueryTest21(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact, Upstream("QueryTest21.java#test", NotApplicable = "直接读写 Java 的表达式构建器内部结构（SQLEntityAliasSegment、FilterContext），C# 没有对应的公开结构")]
    public async Task Test() =>
        Assert.NotNull(await Db.Query<Topic>().LeftJoin<BlogEntity>((t, b) => t.Id == t.Id).Select((t, b) => new { Mobile = t.Id, Email = b.Title }).FirstOrDefaultAsync(Ct));

    // Java 的 t_blog.isNull() / isNotNull()：实体整体判空即主键判空
    [Fact, Upstream("QueryTest21.java#typeTest")]
    public async Task TypeTest()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b == null && b != null && b.Id == null && (b.Score ?? 0m) > 0m && b.Score - b.Star > 15m).ToListAsync(Ct);
        listen.AssertSql("SELECT `id`,`create_time`,`update_time`,`create_by`,`update_by`,`deleted`,`title`,`content`,`url`,`star`,`publish_time`,`score`,`status`,`order`,`is_top`,`top` FROM `t_blog` WHERE `deleted` = ? AND `id` IS NULL AND `id` IS NOT NULL AND `id` IS NULL AND IFNULL(`score`,?) > ? AND (`score` - `star`) > ?");
        listen.AssertParams("false(Boolean),0(BigDecimal),0(BigDecimal),15(BigDecimal)");
    }

    // Java 的 caseWhen(..).then(..).elseEnd(..)：C# 的条件运算符
    [Fact, Upstream("QueryTest21.java#caseWhenTest1", Deviation = "Java 的 proxy set 同名列也写 AS（t.`id` AS `id`），C# 成员初始化同名列不写")]
    public async Task CaseWhenTest1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => t.Title == "someTitle").Select(t => new Topic { Title = t.Title == "123" ? "1" : "2", Id = t.Id }).ToListAsync(Ct);
        listen.AssertSql("SELECT (CASE WHEN t.`title` = ? THEN ? ELSE ? END) AS `title`,t.`id` FROM `t_topic` t WHERE t.`title` = ?");
        listen.AssertParams("123(String),1(String),2(String),someTitle(String)");
    }

    // 嵌套的条件运算符拉平成一个 CASE 的多个 WHEN
    [Fact, Upstream("QueryTest21.java#caseWhenTest2", Deviation = "同 caseWhenTest1")]
    public async Task CaseWhenTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().Where(t => (t.Title == "123" ? 1 : t.Title == "456" && t.Stars == 1 ? t.Stars : 3) == 4 && t.Title == "someTitle")
            .Select(t => new Topic { Title = t.Title == "123" ? "1" : "2", Id = t.Id }).ToListAsync(Ct);
        listen.AssertSql("SELECT (CASE WHEN t.`title` = ? THEN ? ELSE ? END) AS `title`,t.`id` FROM `t_topic` t WHERE (CASE WHEN t.`title` = ? THEN ? WHEN t.`title` = ? AND t.`stars` = ? THEN t.`stars` ELSE ? END) = ? AND t.`title` = ?");
        listen.AssertParams("123(String),1(String),2(String),123(String),1(Integer),456(String),1(Integer),3(Integer),4(Integer),someTitle(String)");
    }

    // Java 的 SearchCountPager 是测试里自定义的分页器：要计数时就是普通分页，不计数时直接 Limit
    [Fact, Upstream("QueryTest21.java#searchCountPageTest")]
    public async Task SearchCountPageTest()
    {
        using (var listen = SqlCapture.Listen())
        {
            await Db.Query<Topic>().Where(t => t.Title!.Contains("123")).ToPageAsync(1, 10, Ct);
            listen.AssertSql(0, "SELECT COUNT(*) FROM `t_topic` WHERE `title` LIKE ?");
            listen.AssertParams(0, "%123%(String)");
            // 匹配的行数取决于别的用例（UpdateTest 会把主题 7 的标题改成 test123…）：最后一页只取剩下的行，LIMIT 在 1~10 之间
            if (listen.Events.Count > 1)
                Assert.Matches(@"^SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `title` LIKE \? LIMIT ([1-9]|10)$", listen.Last.Statement.Text);
        }
        using (var listen = SqlCapture.Listen())
        {
            await Db.Query<Topic>().Where(t => t.Title!.Contains("123")).Limit(0, 10).ToListAsync(Ct);
            listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `title` LIKE ? LIMIT 10");
        }
        // 偏移量超过总数一半：反过来排序，从另一头取，读出后再倒序（OFFSET = 总数 - 偏移 - 本页条数）
        using (var listen = SqlCapture.Listen())
        {
            var total = await Db.Query<Topic>().Where(t => t.Id != null).CountAsync(Ct);
            var page = await Db.Query<Topic>().Where(t => t.Id != null).OrderBy(t => t.CreateTime).ToPageAsync(7, 10, Ct);
            listen.AssertSql(1, "SELECT COUNT(*) FROM `t_topic` WHERE `id` IS NOT NULL");
            listen.AssertSql($"SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` IS NOT NULL ORDER BY `create_time` DESC LIMIT 10 OFFSET {total - 70}");
            var expected = await Db.Query<Topic>().Where(t => t.Id != null).OrderBy(t => t.CreateTime).Limit(60, 10).ToListAsync(Ct);
            Assert.Equal(expected.Select(t => t.CreateTime), page.Rows.Select(t => t.CreateTime));
        }
    }

    // Java 的 configure(op -> op.setReverseOrder(false))：C# 的 DisableReversePaging()
    [Fact, Upstream("QueryTest21.java#TestReverse")]
    public async Task TestReverse()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<Topic>().DisableReversePaging().Where(t => t.Id != null).OrderBy(t => t.CreateTime).ToPageAsync(7, 10, Ct);
        listen.AssertSql(0, "SELECT COUNT(*) FROM `t_topic` WHERE `id` IS NOT NULL");
        listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` IS NOT NULL ORDER BY `create_time` ASC LIMIT 10 OFFSET 60");
    }

    private const string GroupTitle = "SELECT t.`title` AS `value1`,{0} AS `value2` FROM `t_blog` t WHERE t.`deleted` = ? AND t.`title` LIKE ? GROUP BY t.`title`";

    private async Task AssertGroupValue<TValue>(System.Linq.Expressions.Expression<Func<IGroup<string?, BlogEntity>, TValue>> value, string sql, string parameters)
    {
        using var listen = SqlCapture.Listen();
        var parameter = value.Parameters[0];
        var draft = System.Linq.Expressions.Expression.Lambda<Func<IGroup<string?, BlogEntity>, Draft<string?, TValue>>>(
            System.Linq.Expressions.Expression.New(typeof(Draft<string?, TValue>).GetConstructor([typeof(string), typeof(TValue)])!,
                System.Linq.Expressions.Expression.Property(parameter, nameof(IGroup<string, BlogEntity>.Key)), value.Body), parameter);
        await Db.Query<BlogEntity>().Where(b => b.Title!.Contains("123")).GroupBy(b => b.Title).Select(draft).ToListAsync(Ct);
        listen.AssertSql(GroupTitle.Replace("{0}", sql, StringComparison.Ordinal));
        listen.AssertParams(parameters);
    }

    // Java 的 count().filter(..)：C# 的 g.Where(..).Count(..)
    [Fact, Upstream("QueryTest21.java#testWhereGroup")]
    public Task TestWhereGroup() =>
        AssertGroupValue(g => g.Where(e => e.Star >= 123).Count(e => e.Id), "COUNT((CASE WHEN t.`star` >= ? THEN t.`id` ELSE NULL END))", "123(Integer),false(Boolean),%123%(String)");

    [Fact, Upstream("QueryTest21.java#testWhereGroup1")]
    public Task TestWhereGroup1() =>
        AssertGroupValue(g => g.Where(e => e.Star >= 123).Count(), "COUNT((CASE WHEN t.`star` >= ? THEN ? ELSE NULL END))", "123(Integer),1(Integer),false(Boolean),%123%(String)");

    [Fact, Upstream("QueryTest21.java#testWhereGroup2"), Upstream("QueryTest21.java#testWhereGroup4")]
    public Task TestWhereGroup2() =>
        AssertGroupValue(g => g.Where(e => e.Star >= 123).Distinct().Count(), "COUNT(DISTINCT (CASE WHEN t.`star` >= ? THEN ? ELSE NULL END))", "123(Integer),1(Integer),false(Boolean),%123%(String)");

    // 空条件的 where 再去重计数：Java 写成 COUNT(DISTINCT ?)
    [Fact, Upstream("QueryTest21.java#testWhereGroup3")]
    public Task TestWhereGroup3() =>
        AssertGroupValue(g => g.Distinct().Count(), "COUNT(DISTINCT ?)", "1(Integer),false(Boolean),%123%(String)");

    // SUM 的过滤用 ELSE 0
    [Fact, Upstream("QueryTest21.java#testWhereGroup5")]
    public Task TestWhereGroup5() =>
        AssertGroupValue(g => g.Where(e => e.Star >= 123).Sum(e => e.Score), "SUM((CASE WHEN t.`star` >= ? THEN t.`score` ELSE ? END))", "123(Integer),0(Long),false(Boolean),%123%(String)");

    [Fact, Upstream("QueryTest21.java#testWhereGroup6")]
    public Task TestWhereGroup6() =>
        AssertGroupValue(g => g.Where(e => e.Star >= 123).Distinct().Sum(e => e.Score), "SUM(DISTINCT (CASE WHEN t.`star` >= ? THEN t.`score` ELSE ? END))", "123(Integer),0(Long),false(Boolean),%123%(String)");

    private const string BankCardUser = "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id` FROM `doc_bank_card` t LEFT JOIN `doc_user` t1 ON t1.`id` = t.`uid`";

    // 按对一导航上的列排序：隐式 LEFT JOIN；Java 的 asc(NULLS_LAST) 是 C# 的 Sql.NullsLast
    [Fact, Upstream("QueryTest21.java#joinOrderTest"), Upstream("QueryTest21.java#testOrderBy3")]
    public async Task JoinOrderTest()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocBankCard>().OrderBy(c => Sql.NullsLast(c.User!.Age)).ToListAsync(Ct);
        listen.AssertSql(BankCardUser + " ORDER BY CASE WHEN t1.`age` IS NULL THEN 1 ELSE 0 END ASC,t1.`age` ASC");
    }

    [Fact, Upstream("QueryTest21.java#joinOrderTest2")]
    public async Task JoinOrderTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocBankCard>().OrderBy(c => c.User!.Age ?? 1).ToListAsync(Ct);
        listen.AssertSql(BankCardUser + " ORDER BY IFNULL(t1.`age`,?) ASC");
        listen.AssertParams("1(Integer)");
    }

    [Fact, Upstream("QueryTest21.java#joinOrderTest3")]
    public async Task JoinOrderTest3()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocBankCard>().OrderBy(c => Sql.NullsLast(c.User!.Age ?? 1)).ToListAsync(Ct);
        listen.AssertSql(BankCardUser + " ORDER BY CASE WHEN IFNULL(t1.`age`,?) IS NULL THEN 1 ELSE 0 END ASC,IFNULL(t1.`age`,?) ASC");
        listen.AssertParams("1(Integer),1(Integer)");
    }

    private const string UserCards = "SELECT t.`id`,t.`name`,t.`phone`,t.`age` FROM `doc_user` t WHERE ";

    private const string CardCount = "(SELECT COUNT(*) FROM `doc_bank_card` t1 WHERE t1.`uid` = t.`id`)";

    // 子查询计数做区间：同一个子查询在两侧各写一次
    [Fact, Upstream("QueryTest21.java#subNumberRangeClosed")]
    public async Task SubNumberRangeClosed()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>().Where(u => u.BankCards!.LongCount().RangeOpenClosed(1L, 2L)).ToListAsync(Ct);
        listen.AssertSql(UserCards + "(" + CardCount + " > ? AND " + CardCount + " <= ?)");
        listen.AssertParams("1(Long),2(Long)");
    }

    [Fact, Upstream("QueryTest21.java#subNumberRangeClosed1")]
    public async Task SubNumberRangeClosed1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocUser>().Where(u => u.BankCards!.LongCount().RangeClosed(1L, 2L)).ToListAsync(Ct);
        listen.AssertSql(UserCards + CardCount + " BETWEEN ? AND ?");
        listen.AssertParams("1(Long),2(Long)");
    }

    // Java 的 EasySQLUtil.dynamicOrderBy(.., "user.age", .., NULLS_LAST)：C# 的 Sql.Column 按路径取列
    [Fact, Upstream("QueryTest21.java#testOrderBy")]
    public async Task TestOrderBy()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocBankCard>().OrderBy(c => Sql.NullsLast(Sql.Column<int?>(c, "user.age"))).ToListAsync(Ct);
        listen.AssertSql(BankCardUser + " ORDER BY CASE WHEN t1.`age` IS NULL THEN 1 ELSE 0 END ASC,t1.`age` ASC");
    }

    /// <summary>对应 Java 用例里的 UISort：按属性路径、方向和 NULL 位置排序。</summary>
    private sealed class UiSort(params (string Property, bool Ascending, NullsOrder? Nulls)[] sorts) : ISortRequest
    {
        public void ConfigureSort(SortBuilder sort)
        {
            foreach (var (property, ascending, nulls) in sorts) sort.OrderBy(property, ascending, nulls: nulls);
        }
    }

    [Fact, Upstream("QueryTest21.java#testOrderBy2")]
    public async Task TestOrderBy2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocBankCard>().SortBy(new UiSort(("type", false, null), ("user.age", false, NullsOrder.Last))).ToListAsync(Ct);
        listen.AssertSql(BankCardUser + " ORDER BY t.`type` DESC,CASE WHEN t1.`age` IS NULL THEN 1 ELSE 0 END ASC,t1.`age` DESC");
    }

    [Fact, Upstream("QueryTest21.java#anyColumnTest1"), Upstream("QueryTest21.java#anyColumnOrderByTest1")]
    public async Task AnyColumnTest1()
    {
        using var listen = SqlCapture.Listen();
        (string Path, bool Ascending)[] sorts = [("user.age", false), ("type", true)];
        var query = Db.Query<DocBankCard>().Where(c => (Sql.Column<string>(c, "code") ?? "123") == "456" && (c.Code ?? "789") == "987" && (c.Code ?? "654") == "321");
        foreach (var (path, ascending) in sorts)
            query = ascending ? query.OrderBy(c => Sql.NullsLast(Sql.Column<object>(c, path))) : query.OrderByDescending(c => Sql.NullsLast(Sql.Column<object>(c, path)));
        await query.ToListAsync(Ct);
        listen.AssertSql(BankCardUser + " WHERE IFNULL(t.`code`,?) = ? AND IFNULL(t.`code`,?) = ? AND IFNULL(t.`code`,?) = ? ORDER BY CASE WHEN t1.`age` IS NULL THEN 1 ELSE 0 END ASC,t1.`age` DESC,CASE WHEN t.`type` IS NULL THEN 1 ELSE 0 END ASC,t.`type` ASC");
        listen.AssertParams("123(String),456(String),789(String),987(String),654(String),321(String)");
    }

    [Fact, Upstream("QueryTest21.java#anyColumnTest2")]
    public async Task AnyColumnTest2()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<DocBankCard>().Where(c => Sql.Column<int?>(c, "user.age") == 123 && c.User!.Age == 123 && c.Code == "321" && (Sql.Column<string>(c, "code") ?? "123") == "456").ToListAsync(Ct);
        listen.AssertSql(BankCardUser + " WHERE t1.`age` = ? AND t1.`age` = ? AND t.`code` = ? AND IFNULL(t.`code`,?) = ?");
        listen.AssertParams("123(Integer),123(Integer),321(String),123(String),456(String)");
    }

    // Java 的 id().count(true)：C# 的 Sql.CountDistinct
    [Fact, Upstream("QueryTest21.java#testDistinctCount")]
    public async Task TestDistinctCount()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Title!.Contains("123")).Select(b => Sql.CountDistinct(b.Id)).ToListAsync(Ct);
        listen.AssertSql("SELECT COUNT(DISTINCT t.`id`) FROM `t_blog` t WHERE t.`deleted` = ? AND t.`title` LIKE ?");
        listen.AssertParams("false(Boolean),%123%(String)");
    }

    // 去重后计数：COUNT(DISTINCT 列)，不包子查询
    [Fact, Upstream("QueryTest21.java#testDistinctCount1")]
    public async Task TestDistinctCount1()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<BlogEntity>().Where(b => b.Title!.Contains("123")).Select(b => b.Id).Distinct().CountAsync(Ct);
        listen.AssertSql("SELECT COUNT(DISTINCT t.`id`) FROM `t_blog` t WHERE t.`deleted` = ? AND t.`title` LIKE ?");
        listen.AssertParams("false(Boolean),%123%(String)");
    }
}
