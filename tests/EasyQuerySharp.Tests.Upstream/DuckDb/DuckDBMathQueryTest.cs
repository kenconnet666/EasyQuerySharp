using EasyQuerySharp.Tests.Upstream.Functions;

namespace EasyQuerySharp.Tests.Upstream.DuckDb;

/// <summary>
/// 对应 Java：duckdb/DuckDBMathQueryTest.java（越界的参数 DuckDB 会报错，用 CASE 先排除；TRUNC 不带小数位）。
/// Java 在 10000 行随机数上断言，这里与 MySQL、PG 的数学用例共用同一份固定数据。
/// </summary>
public sealed class DuckDBMathQueryTest(DuckDbFixture fixture) : DuckDbTest(fixture)
{
    private const string DoubleParams = "C# 的 Math.Pow / Atan2 只有 double 重载，pow 与 atan2 的参数是 Double（Java 是 BigDecimal）；Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写";

    [Fact, Upstream("duckdb/DuckDBMathQueryTest.java#testPropertyMath", Deviation = DoubleParams)]
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
        listen.AssertSql("SELECT t.\"test_value\",ABS(t.\"test_value\") AS \"test_abs\",SIGN(t.\"test_value\") AS \"test_signum\",FLOOR(t.\"test_value\") AS \"test_floor\",CEILING(t.\"test_value\") AS \"test_ceiling\",ROUND(t.\"test_value\") AS \"test_round1\",ROUND(t.\"test_value\",?) AS \"test_round2\",(CASE WHEN t.\"test_value\" > ? THEN LN(t.\"test_value\") ELSE NULL END) AS \"test_log\",(CASE WHEN t.\"test_value\" > ? THEN LOG10(t.\"test_value\") ELSE NULL END) AS \"test_log10\",(CASE WHEN t.\"test_value\" <> ? THEN POW(t.\"test_value\",?) ELSE NULL END) AS \"test_pow\",(CASE WHEN t.\"test_value\" >= ? THEN SQRT(t.\"test_value\") ELSE NULL END) AS \"test_sqrt\",COS(t.\"test_value\") AS \"test_cos\",SIN(t.\"test_value\") AS \"test_sin\",TAN(t.\"test_value\") AS \"test_tan\",(CASE WHEN (t.\"test_value\" >= ? AND t.\"test_value\" <= ?) THEN ACOS(t.\"test_value\") ELSE NULL END) AS \"test_acos\",(CASE WHEN (t.\"test_value\" >= ? AND t.\"test_value\" <= ?) THEN ASIN(t.\"test_value\") ELSE NULL END) AS \"test_asin\",ATAN(t.\"test_value\") AS \"test_atan\",ATAN2(t.\"test_value\",?) AS \"test_atan2\",TRUNC(t.\"test_value\") AS \"test_truncate\" FROM \"t_math_test\" t");
        listen.AssertParams("2(Integer),0(BigDecimal),0(BigDecimal),0(BigDecimal),3(Double),0(BigDecimal),-1(BigDecimal),1(BigDecimal),-1(BigDecimal),1(BigDecimal),1(Double)");
        MathAssert.Check(rows);
    }
}
