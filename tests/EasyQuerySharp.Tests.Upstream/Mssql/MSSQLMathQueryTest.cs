using EasyQuerySharp.Tests.Upstream.Functions;

namespace EasyQuerySharp.Tests.Upstream.Mssql;

/// <summary>
/// 对应 Java：mssql/MSSQLMathQueryTest.java。SQL Server 的 ROUND 必须带位数、没有 ATAN2（用 ATAN 按象限补 π）；越界的参数会报错，用 CASE 先排除。
/// Java 在 10000 行随机数上断言，这里与 MySQL、PG 的数学用例共用同一份固定数据。
/// </summary>
public sealed class MSSQLMathQueryTest(MsSqlFixture fixture) : MsSqlTest(fixture)
{
    // Java 的 floor() 在 SQL Server 上写成"非负用 FLOOR、负数用 CEILING"（即截断），对负数得到的不是向下取整，且 Java 自己的断言（setScale(0, FLOOR)）会因此失败；
    // C# 的 Math.Floor 仍写 FLOOR
    private const string FloorDeviation = MathQueryTest.DoubleParams + "；Java 把 floor 写成与 truncate 相同的 CASE（对负数是 Java 的 bug），C# 写 FLOOR";

    [Fact, Upstream("mssql/MSSQLMathQueryTest.java#testPropertyMath", Deviation = FloorDeviation)]
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
        listen.AssertSql("SELECT t.[TestValue],ABS(t.[TestValue]) AS [TestAbs],SIGN(t.[TestValue]) AS [TestSignum],FLOOR(t.[TestValue]) AS [TestFloor],CEILING(t.[TestValue]) AS [TestCeiling],ROUND(t.[TestValue],0) AS [TestRound1],ROUND(t.[TestValue],?) AS [TestRound2],(CASE WHEN t.[TestValue] > ? THEN LOG(t.[TestValue]) ELSE NULL END) AS [TestLog],(CASE WHEN t.[TestValue] > ? THEN LOG10(t.[TestValue]) ELSE NULL END) AS [TestLog10],(CASE WHEN t.[TestValue] <> ? THEN POWER(t.[TestValue],?) ELSE NULL END) AS [TestPow],(CASE WHEN t.[TestValue] >= ? THEN SQRT(t.[TestValue]) ELSE NULL END) AS [TestSqrt],COS(t.[TestValue]) AS [TestCos],SIN(t.[TestValue]) AS [TestSin],TAN(t.[TestValue]) AS [TestTan],(CASE WHEN (t.[TestValue] >= ? AND t.[TestValue] <= ?) THEN ACOS(t.[TestValue]) ELSE NULL END) AS [TestAcos],(CASE WHEN (t.[TestValue] >= ? AND t.[TestValue] <= ?) THEN ASIN(t.[TestValue]) ELSE NULL END) AS [TestAsin],ATAN(t.[TestValue]) AS [TestAtan],(CASE WHEN ?<>0 THEN ATAN(t.[TestValue]/?) + CASE WHEN ?<0 AND t.[TestValue]>=0 THEN PI() WHEN ?<0 AND t.[TestValue]<0 THEN -PI() ELSE 0 END WHEN ?=0 AND t.[TestValue]>0 THEN PI()/2 WHEN ?=0 AND t.[TestValue]<0 THEN -PI()/2 ELSE NULL END) AS [TestAtan2],(CASE WHEN t.[TestValue] >= 0 THEN FLOOR(t.[TestValue]) ELSE CEILING(t.[TestValue]) END) AS [TestTruncate] FROM [t_math_test] t");
        listen.AssertParams("2(Integer),0(BigDecimal),0(BigDecimal),0(BigDecimal),3(Double),0(BigDecimal),-1(BigDecimal),1(BigDecimal),-1(BigDecimal),1(BigDecimal),1(Double),1(Double),1(Double),1(Double),1(Double),1(Double)");
        MathAssert.Check(rows);
    }
}
