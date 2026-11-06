using EasyQuerySharp.Metadata;
using Company = EasyQuerySharp.Tests.Upstream.Entities.BlogTest.Company;

namespace EasyQuerySharp.Tests.Upstream.Dialects;

/// <summary>时序表：时间戳主键（TDengine 每张表的第一列）。</summary>
[Table("t_reading")]
public class TdReading
{
    [Column(PrimaryKey = true)]
    public DateTime Ts { get; set; }

    public string? Device { get; set; }

    public double? Value { get; set; }
}

/// <summary>
/// TDengine 方言的写法（Java 的 sql-tsdb 没有用例，这里是 C# 独有的）：分页、插入与按时间删除、没有 UPDATE / upsert / 行锁、
/// 连接、分组、函数表与建表。只渲染，不连库。
/// </summary>
public sealed class TDengineDialectTest
{
    private const string TopicColumns = "SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic`";
    private const string Day2025 = "2025-01-01T00:00(LocalDateTime)";
    private static readonly DateTime Day = new(2025, 1, 1);

    private static EasyDb TDengine => SqlOnly.Db("tdengine");

    [Fact]
    public void PagingUsesLimitOffset() =>
        SqlOnly.AssertStatement(TDengine.Query<TdReading>().Where(r => r.Device == "d1").OrderBy(r => r.Ts).Skip(10).Take(5).ToStatement(),
            "SELECT `ts`,`device`,`value` FROM `t_reading` WHERE `device` = ? ORDER BY `ts` ASC LIMIT 5 OFFSET 10", "d1(String)");

    // 插入是普通的 INSERT（同一时间戳再插入即覆盖）；删除不带别名，一般按时间范围
    [Fact]
    public void InsertAndDelete()
    {
        SqlOnly.AssertStatement(TDengine.Insert(new TdReading { Ts = Day, Device = "d1", Value = 1.5 }).ToStatement(),
            "INSERT INTO `t_reading` (`ts`,`device`,`value`) VALUES (?,?,?)", Day2025 + ",d1(String),1.5(Double)");
        SqlOnly.AssertStatement(TDengine.Delete<TdReading>().Where(r => r.Ts < Day).ToStatement()!, "DELETE FROM `t_reading` WHERE `ts` < ?", Day2025);
    }

    // 驱动执行的文本里值写成字面量、不绑定参数（stmt 接口收到带参数的 DELETE 会让 taosAdapter 崩溃）；Text 仍用 ? 占位
    [Fact]
    public void CommandTextInlinesValues()
    {
        var statement = TDengine.Delete<TdReading>().Where(r => r.Ts < Day && r.Device == "b'x\\" && r.Value > 1.5).ToStatement()!;
        Assert.Equal("""DELETE FROM `t_reading` WHERE `ts` < '2025-01-01 00:00:00.000' AND `device` = 'b\'x\\' AND `value` > 1.5""", statement.CommandText);
        Assert.Equal("DELETE FROM `t_reading` WHERE `ts` < ? AND `device` = ? AND `value` > ?", statement.Text);
        Assert.True(TDengine.Options.Dialect.InlineParameters);
    }

    // 没有 UPDATE、upsert、行锁；删除不能走导航或关联子查询
    [Fact]
    public void UnsupportedStatements()
    {
        Assert.Contains("没有 UPDATE", Assert.Throws<NotSupportedException>(() => TDengine.Update<TdReading>().Set(r => r.Value, 2).Where(r => r.Ts == Day).ToSql()).Message);
        Assert.Contains("upsert", Assert.Throws<NotSupportedException>(() => TDengine.Insert(new TdReading { Ts = Day }).OnConflictIgnore().ToSql()).Message);
        Assert.Contains("只能按本表的列过滤", Assert.Throws<NotSupportedException>(() => TDengine.Delete<Company>().Where(c => c.Users!.Where(u => u.Age > 18).Any()).ToSql()).Message);
        using var tx = TDengine.BeginTransaction();
        Assert.Contains("TDengine 不支持 FOR UPDATE", Assert.Throws<NotSupportedException>(() => TDengine.Query<TdReading>().ForUpdate().ToSql()).Message);
    }

    [Fact]
    public void JoinAndGroupBy()
    {
        SqlOnly.AssertStatement(TDengine.Query<BlogEntity>().LeftJoin<Topic>((b, t) => b.Id == t.Id).Where((_, t) => t.Title == "x").Select((b, t) => new Draft<string?, string?>(b.Id, t.Title)).ToStatement(),
            "SELECT t.`id` AS `value1`,t1.`title` AS `value2` FROM `t_blog` t LEFT JOIN `t_topic` t1 ON t.`id` = t1.`id` WHERE t.`deleted` = ? AND t1.`title` = ?", "false(Boolean),x(String)");
        Assert.Equal("SELECT t.`device` AS `value1`,COUNT(*) AS `value2` FROM `t_reading` t GROUP BY t.`device`",
            TDengine.Query<TdReading>().GroupBy(r => r.Device).Select(g => new Draft<string?, long>(g.Key, g.Count())).ToSql());
    }

    // 日期：TO_CHAR 格式化、取部分；TIMEDIFF 相差、TIMETRUNCATE 截断（到天及以下）；时长只能写字面量，不能按参数加减
    [Fact]
    public void FunctionTable()
    {
        Assert.Equal("SELECT TO_CHAR(t.`ts`,'yyyy-mm-dd hh24:mi:ss') AS `value1`,CAST(TO_CHAR(t.`ts`,'yyyy') AS INT) AS `value2`,(WEEKDAY(t.`ts`)+1) AS `value3`,TIMEDIFF(?,t.`ts`,1h) AS `value4`,TIMETRUNCATE(t.`ts`,1d) AS `value5`,NOW() AS `value6` FROM `t_reading` t",
            TDengine.Query<TdReading>().Select(r => new Draft<string, int, int, long, DateTime, DateTime>(r.Ts.ToString("yyyy-MM-dd HH:mm:ss"), r.Ts.Year, r.Ts.IsoDayOfWeek(),
                (long)(Day - r.Ts).TotalHours, r.Ts.DateTrunc(DateTruncUnit.Day), Sql.Now())).ToSql());
        Assert.Contains("字面量", Assert.Throws<NotSupportedException>(() => TDengine.Query<TdReading>().Select(r => new Draft<DateTime>(r.Ts.AddDays(1))).ToSql()).Message);
        Assert.Equal("SELECT CHAR_LENGTH(t.`title`) AS `value1`,(POSITION(? IN t.`title`) - 1) AS `value2`,LOG(t.`stars`,?) AS `value3`,POW(t.`stars`,?) AS `value4`,GREATEST(t.`stars`,?,?) AS `value5` FROM `t_topic` t",
            TDengine.Query<Topic>().Select(t => new Draft<int, int, double, double, int?>(t.Title!.Length, t.Title!.IndexOf("x"), Math.Log((double)t.Stars!, 2), Math.Pow((double)t.Stars!, 2), Sql.MaxColumns(t.Stars, 1, 2))).ToSql());
    }

    // 值里含 % 或 _ 时转义后按字面匹配：反斜杠本来就是 LIKE 的转义符，不写 ESCAPE
    [Fact]
    public void LikeEscapesWildcardValues() =>
        SqlOnly.AssertStatement(TDengine.Query<Topic>().Where(t => t.Title!.Contains("30%")).ToStatement(), TopicColumns + " WHERE `title` LIKE ?", "%30\\%%(String)");

    // 列只有名字与类型（时间戳主键在第一列）；没有时间戳主键的实体建不了表
    [Fact]
    public void CreateTable()
    {
        Assert.Equal("CREATE TABLE IF NOT EXISTS `t_reading` (`ts` TIMESTAMP, `device` NCHAR(255), `value` DOUBLE)", TDengine.CodeFirst.CreateTableSql(typeof(TdReading)));
        Assert.Contains("DateTime 主键", Assert.Throws<NotSupportedException>(() => TDengine.CodeFirst.CreateTableSql(typeof(Topic))).Message);
    }
}
