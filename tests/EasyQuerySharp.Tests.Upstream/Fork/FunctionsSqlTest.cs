namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>对应 Java fork：functions/DateTruncSqlTest.java（日期截断，各方言写法不同，不支持的方言渲染期报错）。</summary>
public sealed class DateTruncSqlTest
{
    private static string Sql(string dialect, DateTruncUnit unit) =>
        SqlOnly.Db(dialect).Query<ForkUser>().Select(u => new Draft<DateTime?>(u.CreatedAt.DateTrunc(unit))).ToSql();

    [Fact, Upstream("fork/functions/DateTruncSqlTest.java#postgresUsesDateTrunc")]
    public void PostgresUsesDateTrunc() =>
        Assert.Equal("SELECT DATE_TRUNC('quarter', t.\"created_at\") AS \"value1\" FROM \"t_fork_user\" t", Sql("pgsql", DateTruncUnit.Quarter));

    [Fact, Upstream("fork/functions/DateTruncSqlTest.java#mysqlRepeatsTheColumnForWeeks")]
    public void MysqlRepeatsTheColumnForWeeks() =>
        Assert.Equal("SELECT DATE_SUB(CAST(DATE_FORMAT(t.`created_at`,'%Y-%m-%d 00:00:00') AS DATETIME), INTERVAL WEEKDAY(t.`created_at`) DAY) AS `value1` FROM `t_fork_user` t",
            Sql("mysql", DateTruncUnit.Week));

    // 没有登记 DateTrunc 的方言在渲染期报错，消息里带上粒度
    [Fact, Upstream("fork/functions/DateTruncSqlTest.java#unsupportedDialectSaysSo",
         Deviation = "Java 用 SQL Server 演示；C# 的 SQL Server 用 2022 的 DATETRUNC，内置方言都支持，这里用一个没登记函数的方言演示")]
    public void UnsupportedDialectSaysSo() =>
        Assert.Contains("DateTrunc(Day)", Assert.Throws<NotSupportedException>(() =>
            new EasyDb(o => o.Use(new BareDialect())).Query<ForkUser>().Select(u => new Draft<DateTime?>(u.CreatedAt.DateTrunc(DateTruncUnit.Day))).ToSql()).Message);

    // 只用基类默认写法的方言
    private sealed class BareDialect : EasyQuerySharp.Dialects.SqlDialect
    {
        public override string Name => "bare";
    }
}

/// <summary>对应 Java fork：functions/NullSafeSqlTest.java（null 安全相等、NULLIF）。</summary>
public sealed class NullSafeSqlTest
{
    [Fact, Upstream("fork/functions/NullSafeSqlTest.java#nullSafeEqualsBetweenColumns")]
    public void NullSafeEqualsBetweenColumns() =>
        Assert.Equal("SELECT t.`id` FROM `t_fork_post` t INNER JOIN `t_fork_user` t1 ON (t1.`id` <=> t.`user_id`)",
            SqlOnly.Db("mysql").Query<ForkPost>().InnerJoin<ForkUser>((p, u) => u.Id.EqualsNullSafe(p.UserId)).Select((p, u) => p.Id).ToSql());

    // fork 的方法体不在本机，快照里只有 MySQL 的原文；其余方言按各自的写法：PG 家族与 Db2 IS NOT DISTINCT FROM、SQLite IS，没有专门运算符的库展开成 OR
    [Theory, Upstream("fork/functions/NullSafeSqlTest.java#nullSafeEqualsPerDialect")]
    [InlineData("mysql", "SELECT t.`id` FROM `t_fork_user` t WHERE (t.`name` <=> ?)")]
    [InlineData("pgsql", "SELECT t.\"id\" FROM \"t_fork_user\" t WHERE (t.\"name\" IS NOT DISTINCT FROM ?)")]
    [InlineData("duckdb", "SELECT t.\"id\" FROM \"t_fork_user\" t WHERE (t.\"name\" IS NOT DISTINCT FROM ?)")]
    [InlineData("db2", "SELECT t.\"id\" FROM \"t_fork_user\" t WHERE (t.\"name\" IS NOT DISTINCT FROM ?)")]
    [InlineData("sqlite", "SELECT t.\"id\" FROM \"t_fork_user\" t WHERE (t.\"name\" IS ?)")]
    [InlineData("mssql", "SELECT t.[id] FROM [t_fork_user] t WHERE ((t.[name] = ?) OR (t.[name] IS NULL AND ? IS NULL))")]
    [InlineData("oracle", "SELECT t.\"id\" FROM \"t_fork_user\" t WHERE ((t.\"name\" = ?) OR (t.\"name\" IS NULL AND ? IS NULL))")]
    [InlineData("hana", "SELECT t.\"id\" FROM \"t_fork_user\" t WHERE ((t.\"name\" = ?) OR (t.\"name\" IS NULL AND ? IS NULL))")]
    public void NullSafeEqualsPerDialect(string dialect, string sql) =>
        Assert.Equal(sql, SqlOnly.Db(dialect).Query<ForkUser>().Where(u => u.Name.EqualsNullSafe("a")).Select(u => u.Id).ToSql());

    [Fact, Upstream("fork/functions/NullSafeSqlTest.java#nullIf")]
    public void NullIf()
    {
        Assert.Equal("SELECT NULLIF(t.`name`, ?) AS `value1` FROM `t_fork_user` t",
            SqlOnly.Db("mysql").Query<ForkUser>().Select(u => new Draft<string?>(u.Name.NullIf(""))).ToSql());
        Assert.Equal("SELECT t.\"id\" FROM \"t_fork_user\" t WHERE COALESCE(NULLIF(t.\"name\", ?),?) = ?",
            SqlOnly.Db("pgsql").Query<ForkUser>().Where(u => (u.Name.NullIf("") ?? "-") == "-").Select(u => u.Id).ToSql());
    }
}

/// <summary>对应 Java fork：functions/SmallFixesSqlTest.java。</summary>
public sealed class SmallFixesSqlTest
{
    [Fact, Upstream("fork/functions/SmallFixesSqlTest.java#jsonArraySize")]
    public void JsonArraySize()
    {
        Assert.Equal("SELECT `id`,`name`,`age`,`company_id`,`attrs`,`created_at` FROM `t_fork_user` WHERE JSON_LENGTH(`attrs`) > ?",
            SqlOnly.Db("mysql").Query<ForkUser>().Where(u => Sql.Json(u.Attrs).Length() > 1).ToSql());
        Assert.Equal("SELECT \"id\",\"name\",\"age\",\"company_id\",\"attrs\",\"created_at\" FROM \"t_fork_user\" WHERE JSONB_ARRAY_LENGTH(\"attrs\") > ?",
            SqlOnly.Db("pgsql").Query<ForkUser>().Where(u => Sql.Json(u.Attrs).Length() > 1).ToSql());
    }

    [Fact, Upstream("fork/functions/SmallFixesSqlTest.java#offsetWindowHasSpaceBeforeOrderBy")]
    public void OffsetWindowHasSpaceBeforeOrderBy()
    {
        var sql = SqlOnly.Db("mysql").Query<ForkPost>()
            .Select(p => new Draft<string?, decimal?>(p.Id, Sql.Over.Lag(p.Amount, 1, w => w.PartitionBy(p.UserId).OrderBy(p.Id)))).ToSql();
        Assert.Contains("OVER (PARTITION BY t.`user_id` ORDER BY t.`id` ASC)", sql);
    }
}

/// <summary>对应 Java fork：functions/WindowSqlTest.java（窗口：分区、全局窗口、frame、分布函数、参数校验）。</summary>
public sealed class WindowSqlTest
{
    // 只取窗口那一列的 SQL
    private static string Column<T>(System.Linq.Expressions.Expression<Func<ForkPost, T>> window)
    {
        var parameter = window.Parameters[0];
        var draft = System.Linq.Expressions.Expression.New(typeof(Draft<string, T>).GetConstructors()[0],
            System.Linq.Expressions.Expression.Property(parameter, nameof(ForkPost.Id)), window.Body);
        var selector = System.Linq.Expressions.Expression.Lambda<Func<ForkPost, Draft<string, T>>>(draft, parameter);
        var sql = SqlOnly.Db("mysql").Query<ForkPost>().Select(selector).ToSql();
        const string prefix = "SELECT t.`id` AS `value1`,";
        return sql[prefix.Length..sql.IndexOf(" AS `value2`", StringComparison.Ordinal)];
    }

    [Fact, Upstream("fork/functions/WindowSqlTest.java#partitionedWindowIsUnchanged")]
    public void PartitionedWindowIsUnchanged()
    {
        Assert.Equal("(SUM(t.`amount`) OVER (PARTITION BY t.`user_id` ORDER BY t.`id` ASC))",
            Column(p => Sql.Over.Sum(p.Amount, w => w.PartitionBy(p.UserId).OrderBy(p.Id))));
        Assert.Equal("(ROW_NUMBER() OVER (PARTITION BY t.`user_id`, t.`title` ORDER BY t.`id` DESC))",
            Column(p => Sql.Over.RowNumber(w => w.PartitionBy(p.UserId, p.Title).OrderByDescending(p.Id))));
    }

    [Fact, Upstream("fork/functions/WindowSqlTest.java#globalWindowWithoutPartition")]
    public void GlobalWindowWithoutPartition()
    {
        Assert.Equal("(ROW_NUMBER() OVER (ORDER BY t.`amount` DESC))", Column(p => Sql.Over.RowNumber(w => w.OrderByDescending(p.Amount))));
        Assert.Equal("(SUM(t.`amount`) OVER ())", Column(p => Sql.Over.Sum(p.Amount, w => w)));
        Assert.Equal("(COUNT(t.`id`) OVER ())", Column(p => Sql.Over.Count(p.Id, w => w)));
    }

    [Fact, Upstream("fork/functions/WindowSqlTest.java#framesFollowOrderBy")]
    public void FramesFollowOrderBy()
    {
        Assert.Equal("(SUM(t.`amount`) OVER (PARTITION BY t.`user_id` ORDER BY t.`id` ASC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW))",
            Column(p => Sql.Over.Sum(p.Amount, w => w.PartitionBy(p.UserId).OrderBy(p.Id).Rows(WindowBound.UnboundedPreceding, WindowBound.CurrentRow))));
        Assert.Equal("(AVG(t.`amount`) OVER (ORDER BY t.`id` ASC ROWS BETWEEN 2 PRECEDING AND 1 FOLLOWING))",
            Column(p => Sql.Over.Avg(p.Amount, w => w.OrderBy(p.Id).Rows(WindowBound.Preceding(2), WindowBound.Following(1)))));
        Assert.Equal("(MAX(t.`amount`) OVER (ORDER BY t.`amount` ASC RANGE BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING))",
            Column(p => Sql.Over.Max(p.Amount, w => w.OrderBy(p.Amount).Range(WindowBound.CurrentRow, WindowBound.UnboundedFollowing))));
    }

    [Fact, Upstream("fork/functions/WindowSqlTest.java#distributionFunctions")]
    public void DistributionFunctions()
    {
        Assert.Equal("(NTILE(4) OVER (ORDER BY t.`amount` ASC))", Column(p => Sql.Over.Ntile(4, w => w.OrderBy(p.Amount))));
        Assert.Equal("(PERCENT_RANK() OVER (PARTITION BY t.`user_id` ORDER BY t.`amount` ASC))",
            Column(p => Sql.Over.PercentRank(w => w.PartitionBy(p.UserId).OrderBy(p.Amount))));
        Assert.Equal("(CUME_DIST() OVER (ORDER BY t.`amount` ASC))", Column(p => Sql.Over.CumeDist(w => w.OrderBy(p.Amount))));
    }

    [Fact, Upstream("fork/functions/WindowSqlTest.java#invalidArgumentsAreRejected")]
    public void InvalidArgumentsAreRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => WindowBound.Preceding(-1));
        Assert.ThrowsAny<Exception>(() => Column(p => Sql.Over.Ntile(0, w => w)));
        Assert.ThrowsAny<Exception>(() => Column(p => Sql.Over.Sum(p.Amount, w => w.PartitionBy())));
    }
}

/// <summary>对应 Java fork：functions/GroupingSqlTest.java（ROLLUP / CUBE / GROUPING）。</summary>
public sealed class GroupingSqlTest
{
    [Fact, Upstream("fork/functions/GroupingSqlTest.java#postgresRollupAndCube")]
    public void PostgresRollupAndCube()
    {
        Assert.Equal("SELECT t.\"user_id\" AS \"value1\",t.\"title\" AS \"value2\",SUM(t.\"amount\") AS \"value3\",GROUPING(t.\"title\") AS \"value4\""
                     + " FROM \"t_fork_post\" t GROUP BY ROLLUP(t.\"user_id\",t.\"title\")",
            SqlOnly.Db("pgsql").Query<ForkPost>().GroupBy(p => new { p.UserId, p.Title }).Rollup()
                .Select(g => new Draft<string?, string?, decimal?, int>(g.Key.UserId, g.Key.Title, g.Sum(p => p.Amount), g.Grouping(g.Key.Title))).ToSql());
        Assert.EndsWith(" GROUP BY CUBE(t.\"user_id\",t.\"title\")",
            SqlOnly.Db("pgsql").Query<ForkPost>().GroupBy(p => new { p.UserId, p.Title }).Cube()
                .Select(g => new Draft<string?, long>(g.Key.UserId, g.Count())).ToSql());
    }

    [Fact, Upstream("fork/functions/GroupingSqlTest.java#mysqlWithRollupAndNoCube")]
    public void MysqlWithRollupAndNoCube()
    {
        Assert.EndsWith(" GROUP BY t.`user_id` WITH ROLLUP",
            SqlOnly.Db("mysql").Query<ForkPost>().GroupBy(p => p.UserId).Rollup().Select(g => new Draft<string?, long>(g.Key, g.Count())).ToSql());
        var ex = Assert.Throws<NotSupportedException>(() =>
            SqlOnly.Db("mysql").Query<ForkPost>().GroupBy(p => p.UserId).Cube().Select(g => new Draft<string?, long>(g.Key, g.Count())).ToSql());
        Assert.Contains("CUBE", ex.Message);
    }

    [Fact, Upstream("fork/functions/GroupingSqlTest.java#plainGroupByIsUnchanged")]
    public void PlainGroupByIsUnchanged() =>
        Assert.EndsWith(" GROUP BY t.`user_id`",
            SqlOnly.Db("mysql").Query<ForkPost>().GroupBy(p => p.UserId).Select(g => new Draft<string?, long>(g.Key, g.Count())).ToSql());

    [Fact, Upstream("fork/functions/GroupingSqlTest.java#rollupNeedsGroupBy",
         NotApplicable = "Rollup 只定义在 GroupBy 之后的分组查询上，没有分组时写不出来（编译期保证）")]
    public void RollupNeedsGroupBy() { }
}
