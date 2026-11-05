using EasyQuerySharp.Tests.Upstream.Pgsql;

namespace EasyQuerySharp.Tests.Upstream.Functions;

/// <summary>算出的值与 C# 的 Math 对比（三角、对数在库里是浮点，允许小误差）。</summary>
internal static class MathAssert
{
    public static readonly decimal[] Values = [0.5m, 2.25m, -1.5m, 0m, 10m];

    public static async Task SeedAsync(EasyDb db, CancellationToken ct)
    {
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(MathTest)], ct);
        await db.CodeFirst.CreateTablesAsync([typeof(MathTest)], ct);
        await db.Insert(Values.Select((v, i) => new MathTest { Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture), TestValue = v })).ExecuteAsync(ct);
    }

    public static void Check(List<MathTestDTO> rows)
    {
        Assert.Equal(Values.Length, rows.Count);
        foreach (var row in rows)
        {
            var v = row.TestValue!.Value;
            Assert.Equal(Math.Abs(v), row.TestAbs);
            Assert.Equal(Math.Sign(v), row.TestSignum);
            Assert.Equal(Math.Floor(v), row.TestFloor);
            Assert.Equal(Math.Ceiling(v), row.TestCeiling);
            Assert.Equal(Math.Round(v, MidpointRounding.AwayFromZero), row.TestRound1);
            Assert.Equal(Math.Round(v, 2, MidpointRounding.AwayFromZero), row.TestRound2);
            Assert.Equal(Math.Truncate(v), row.TestTruncate);
            Near(Math.Cos((double)v), row.TestCos);
            Near(Math.Sin((double)v), row.TestSin);
            Near(Math.Atan((double)v), row.TestAtan);
            Near(Math.Atan2((double)v, 1), row.TestAtan2);
            if (v > 0)
            {
                Near(Math.Log((double)v), row.TestLog);
                Near(Math.Log10((double)v), row.TestLog10);
            }
            if (v >= 0) Near(Math.Sqrt((double)v), row.TestSqrt);
            if (row.TestPow is not null) Near(Math.Pow((double)v, 3), row.TestPow);
            if (row.TestAcos is not null) Near(Math.Acos((double)v), row.TestAcos);
            if (row.TestAsin is not null) Near(Math.Asin((double)v), row.TestAsin);
        }
    }

    private static void Near(double expected, decimal? actual) => Assert.InRange((double)actual!.Value, expected - 1e-6, expected + 1e-6);
}

/// <summary>对应 Java：MathQueryTest.java（MySQL 上的数学函数：abs、sign、floor、ceiling、round、log、pow、三角、truncate）。</summary>
public sealed class MathQueryTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    internal const string DoubleParams = "C# 的 Math.Pow / Atan2 只有 double 重载，pow 与 atan2 的参数是 Double（Java 是 BigDecimal）；Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写";

    // Java 的 m.testValue().abs() 与 SQLMathExpression.abs(m.testValue()) 两种写法，C# 都是 Math.Abs
    [Fact, Upstream("MathQueryTest.java#testPropertyMath", Deviation = DoubleParams), Upstream("MathQueryTest.java#testPropertyMath2", Deviation = DoubleParams)]
    public async Task TestPropertyMath()
    {
        await MathAssert.SeedAsync(Db, Ct);
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<MathTest>().Select(m => new MathTestDTO
        {
            TestValue = m.TestValue,
            TestAbs = Math.Abs(m.TestValue!.Value),
            TestSignum = Math.Sign(m.TestValue.Value),
            TestFloor = Math.Floor(m.TestValue.Value),
            TestCeiling = Math.Ceiling(m.TestValue.Value),
            TestRound1 = Math.Round(m.TestValue.Value),
            TestRound2 = Math.Round(m.TestValue.Value, 2),
            TestLog = (decimal)Math.Log((double)m.TestValue.Value),
            TestLog10 = (decimal)Math.Log10((double)m.TestValue.Value),
            TestPow = (decimal)Math.Pow((double)m.TestValue.Value, 3),
            TestSqrt = (decimal)Math.Sqrt((double)m.TestValue.Value),
            TestCos = (decimal)Math.Cos((double)m.TestValue.Value),
            TestSin = (decimal)Math.Sin((double)m.TestValue.Value),
            TestTan = (decimal)Math.Tan((double)m.TestValue.Value),
            TestAcos = (decimal)Math.Acos((double)m.TestValue.Value),
            TestAsin = (decimal)Math.Asin((double)m.TestValue.Value),
            TestAtan = (decimal)Math.Atan((double)m.TestValue.Value),
            TestAtan2 = (decimal)Math.Atan2((double)m.TestValue.Value, 1),
            TestTruncate = Math.Truncate(m.TestValue.Value),
        }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.`test_value`,ABS(t.`test_value`) AS `test_abs`,SIGN(t.`test_value`) AS `test_signum`,FLOOR(t.`test_value`) AS `test_floor`,CEILING(t.`test_value`) AS `test_ceiling`,ROUND(t.`test_value`) AS `test_round1`,ROUND(t.`test_value`,?) AS `test_round2`,LOG(t.`test_value`) AS `test_log`,LOG10(t.`test_value`) AS `test_log10`,POW(t.`test_value`,?) AS `test_pow`,SQRT(t.`test_value`) AS `test_sqrt`,COS(t.`test_value`) AS `test_cos`,SIN(t.`test_value`) AS `test_sin`,TAN(t.`test_value`) AS `test_tan`,ACOS(t.`test_value`) AS `test_acos`,ASIN(t.`test_value`) AS `test_asin`,ATAN(t.`test_value`) AS `test_atan`,ATAN2(t.`test_value`,?) AS `test_atan2`,TRUNCATE(t.`test_value`,0) AS `test_truncate` FROM `t_math_test` t");
        listen.AssertParams("2(Integer),3(Double),1(Double)");
        MathAssert.Check(rows);
    }
}

/// <summary>对应 Java：pgsql/MathQueryTest.java（PG 对越界的参数会报错，用 CASE 先排除）。</summary>
public sealed class PgMathQueryTest(PgFixture fixture) : PgTest(fixture)
{
    [Fact, Upstream("pgsql/MathQueryTest.java#testPropertyMath", Deviation = MathQueryTest.DoubleParams)]
    public async Task TestPropertyMath()
    {
        await MathAssert.SeedAsync(Db, Ct);
        using var listen = SqlCapture.Listen();
        var rows = await Db.Query<MathTest>().Select(m => new MathTestDTO
        {
            TestValue = m.TestValue,
            TestAbs = Math.Abs(m.TestValue!.Value),
            TestSignum = Math.Sign(m.TestValue.Value),
            TestFloor = Math.Floor(m.TestValue.Value),
            TestCeiling = Math.Ceiling(m.TestValue.Value),
            TestRound1 = Math.Round(m.TestValue.Value),
            TestRound2 = Math.Round(m.TestValue.Value, 2),
            TestLog = m.TestValue > 0m ? (decimal)Math.Log((double)m.TestValue.Value) : null,
            TestLog10 = m.TestValue > 0m ? (decimal)Math.Log10((double)m.TestValue.Value) : null,
            TestPow = m.TestValue != 0m ? (decimal)Math.Pow((double)m.TestValue.Value, 3) : null,
            TestSqrt = m.TestValue >= 0m ? (decimal)Math.Sqrt((double)m.TestValue.Value) : null,
            TestCos = (decimal)Math.Cos((double)m.TestValue.Value),
            TestSin = (decimal)Math.Sin((double)m.TestValue.Value),
            TestTan = (decimal)Math.Tan((double)m.TestValue.Value),
            TestAcos = m.TestValue.RangeClosed(-1m, 1m) ? (decimal)Math.Acos((double)m.TestValue.Value) : null,
            TestAsin = m.TestValue.RangeClosed(-1m, 1m) ? (decimal)Math.Asin((double)m.TestValue.Value) : null,
            TestAtan = (decimal)Math.Atan((double)m.TestValue.Value),
            TestAtan2 = (decimal)Math.Atan2((double)m.TestValue.Value, 1),
            TestTruncate = Math.Truncate(m.TestValue.Value),
        }).ToListAsync(Ct);
        listen.AssertSql("SELECT t.\"test_value\",ABS(t.\"test_value\") AS \"test_abs\",SIGN(t.\"test_value\") AS \"test_signum\",FLOOR(t.\"test_value\") AS \"test_floor\",CEILING(t.\"test_value\") AS \"test_ceiling\",ROUND(t.\"test_value\") AS \"test_round1\",ROUND(t.\"test_value\",?) AS \"test_round2\",(CASE WHEN t.\"test_value\" > ? THEN LN(t.\"test_value\") ELSE NULL END) AS \"test_log\",(CASE WHEN t.\"test_value\" > ? THEN LOG10(t.\"test_value\") ELSE NULL END) AS \"test_log10\",(CASE WHEN t.\"test_value\" <> ? THEN POW(t.\"test_value\",?) ELSE NULL END) AS \"test_pow\",(CASE WHEN t.\"test_value\" >= ? THEN SQRT(t.\"test_value\") ELSE NULL END) AS \"test_sqrt\",COS(t.\"test_value\") AS \"test_cos\",SIN(t.\"test_value\") AS \"test_sin\",TAN(t.\"test_value\") AS \"test_tan\",(CASE WHEN (t.\"test_value\" >= ? AND t.\"test_value\" <= ?) THEN ACOS(t.\"test_value\") ELSE NULL END) AS \"test_acos\",(CASE WHEN (t.\"test_value\" >= ? AND t.\"test_value\" <= ?) THEN ASIN(t.\"test_value\") ELSE NULL END) AS \"test_asin\",ATAN(t.\"test_value\") AS \"test_atan\",ATAN2(t.\"test_value\",?) AS \"test_atan2\",TRUNC(t.\"test_value\",0) AS \"test_truncate\" FROM \"t_math_test\" t");
        listen.AssertParams("2(Integer),0(BigDecimal),0(BigDecimal),0(BigDecimal),3(Double),0(BigDecimal),-1(BigDecimal),1(BigDecimal),-1(BigDecimal),1(BigDecimal),1(Double)");
        MathAssert.Check(rows);
    }
}
