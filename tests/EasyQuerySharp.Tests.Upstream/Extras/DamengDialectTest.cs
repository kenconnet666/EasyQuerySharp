using EasyQuerySharp.Metadata;
using EasyQuerySharp.Tests.Upstream.Dameng;
using SysUser = EasyQuerySharp.Tests.Upstream.Mysql8.SysUser;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// 达梦方言与 Oracle 不同、但 Java 用例没有覆盖的写法（建表的类型、截断到秒、NULL 位置），
/// 以及达梦沿用的 Oracle 写法里顺手补齐的部分（拼接式 LIKE、带组内排序的字符串聚合）。
/// </summary>
public sealed class DamengDialectTest
{
    /// <summary>建表用的实体：自增主键与各种值类型。</summary>
    [Table("t_dm_types")]
    public sealed class DamengTypes
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public string? Name { get; set; }

        public int? Count { get; set; }

        public bool Flag { get; set; }

        public double? Ratio { get; set; }

        public float? Rate { get; set; }

        public TimeOnly? At { get; set; }

        public DateTime? CreateTime { get; set; }
    }

    // 自增列 IDENTITY(1, 1)，整数用 INT / BIGINT，布尔 BIT，浮点 DOUBLE / REAL，时间 TIME
    [Fact]
    public void CreateTable() =>
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"t_dm_types\" (\"id\" BIGINT IDENTITY(1, 1) NOT NULL, \"name\" VARCHAR2(255) NULL, \"count\" INT NULL, \"flag\" BIT NOT NULL, " +
                     "\"ratio\" DOUBLE NULL, \"rate\" REAL NULL, \"at\" TIME NULL, \"create_time\" TIMESTAMP NULL, PRIMARY KEY (\"id\"))",
            SqlOnly.Db("dameng").CodeFirst.CreateTableSql(typeof(DamengTypes)));

    // DATE 只到日：截断到秒经过字符串转换；其余单位与 Oracle 一样用 TRUNC
    [Fact]
    public void DateTrunc() =>
        Assert.Equal("SELECT TO_TIMESTAMP(TO_CHAR(t.\"create_time\", 'YYYY-MM-DD HH24:MI:SS'), 'YYYY-MM-DD HH24:MI:SS') AS \"value1\",TRUNC(t.\"create_time\", 'DD') AS \"value2\" FROM \"MY_TOPIC\" t",
            SqlOnly.Db("dameng").Query<DamengMyTopic>()
                .Select(t => new Draft<DateTime, DateTime>(t.CreateTime!.Value.DateTrunc(DateTruncUnit.Second), t.CreateTime!.Value.DateTrunc(DateTruncUnit.Day))).ToSql());

    // 加年 / 月：Java 沿用 Oracle 的 add_months，但达梦的 ADD_MONTHS 返回只有日期的 DATE（实测丢掉时间），改用保留时间的 DATEADD
    [Fact]
    public void AddYearsAndMonthsKeepTheTime() =>
        SqlOnly.AssertStatement(SqlOnly.Db("dameng").Query<DamengMyTopic>()
                .Select(t => new Draft<DateTime, DateTime>(t.CreateTime!.Value.AddYears(1), t.CreateTime!.Value.AddMonths(2))).ToStatement(),
            "SELECT DATEADD(YEAR,?,t.\"create_time\") AS \"value1\",DATEADD(MONTH,?,t.\"create_time\") AS \"value2\" FROM \"MY_TOPIC\" t", "1(Long),2(Long)");

    // Java 只给 Oracle 覆盖了 NULL 位置的原生写法，达梦用通用的 CASE
    [Theory]
    [InlineData("oracle", "ORDER BY \"title\" ASC NULLS LAST")]
    [InlineData("dameng", "ORDER BY CASE WHEN \"title\" IS NULL THEN 1 ELSE 0 END ASC,\"title\" ASC")]
    public void NullsLast(string dialect, string orderBy) =>
        Assert.EndsWith(orderBy, SqlOnly.Db(dialect).Query<DamengMyTopic>().OrderBy(t => Sql.NullsLast(t.Title)).ToSql(), StringComparison.Ordinal);

    [Fact]
    public void CastToBoolean() =>
        Assert.Equal("SELECT CAST(t.\"stars\" AS BIT) AS \"value1\" FROM \"MY_TOPIC\" t",
            SqlOnly.Db("dameng").Query<DamengMyTopic>().Select(t => new Draft<bool>(Convert.ToBoolean(t.Stars))).ToSql());

    // 值是列或要求在 SQL 里拼 % 时先转成字符（Java 的 OracleLikeSQLFunction）
    [Theory]
    [InlineData("oracle")]
    [InlineData("dameng")]
    public void LikeConcat(string dialect) =>
        SqlOnly.AssertStatement(SqlOnly.Db(dialect).Query<DamengMyTopic>().Where(t => t.Title!.Contains(t.Id!) || t.Title.LikeEndsWith("x")).ToStatement(),
            "SELECT \"id\",\"stars\",\"title\",\"create_time\" FROM \"MY_TOPIC\" WHERE (\"title\" LIKE ('%'||TO_CHAR(\"id\")||'%') OR \"title\" LIKE ('%'||TO_CHAR(?)))", "x(String)");

    // 带组内排序的字符串聚合：SQL Server / Oracle / 达梦写成 WITHIN GROUP（Java 的 *JoiningSQLFunction）
    [Theory]
    [InlineData("mssql", "STRING_AGG(t1.[type], ?) WITHIN GROUP (ORDER BY t1.[open_time] ASC)")]
    [InlineData("oracle", "LISTAGG(TO_CHAR(t1.\"type\"), ?) WITHIN GROUP(ORDER BY t1.\"open_time\" ASC)")]
    [InlineData("dameng", "LISTAGG(TO_CHAR(t1.\"type\"), ?) WITHIN GROUP(ORDER BY t1.\"open_time\" ASC)")]
    public void OrderedStringJoin(string dialect, string aggregate) =>
        Assert.Contains(aggregate, SqlOnly.Db(dialect).Query<SysUser>().GroupJoinAll()
            .Where(u => string.Join(",", u.BankCards!.OrderBy(c => c.OpenTime).Select(c => c.Type)) == "123").ToSql(), StringComparison.Ordinal);
}
