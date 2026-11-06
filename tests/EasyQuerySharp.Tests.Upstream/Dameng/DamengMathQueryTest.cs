namespace EasyQuerySharp.Tests.Upstream.Dameng;

/// <summary>对应 Java：dameng/MathQueryTest.java（取整、四舍五入等与 Oracle 相同：CEIL、不带位数的 ROUND(x)）。</summary>
public sealed class DamengMathQueryTest : DamengTest
{
    [Fact, Upstream("dameng/MathQueryTest.java#testPropertyMath1",
        Deviation = "Java 的 DTO 代理 set() 同名列也写 AS，C# 同名不写；Java 还在实库 10000 行随机数上比对结果，实库验证在 3.1 冒烟里做")]
    public void TestPropertyMath1() =>
        SqlOnly.AssertStatement(Db.Query<MathTest>().Select(m => new MathTestDTO
            {
                TestValue = m.TestValue,
                TestAbs = Math.Abs(m.TestValue!.Value),
                TestSignum = Math.Sign(m.TestValue.Value),
                TestFloor = Math.Floor(m.TestValue.Value),
                TestCeiling = Math.Ceiling(m.TestValue.Value),
                TestRound1 = Math.Round(m.TestValue.Value),
                TestRound2 = Math.Round(m.TestValue.Value, 2),
            }).ToStatement(),
            "SELECT t.\"TEST_VALUE\",ABS(t.\"TEST_VALUE\") AS \"TEST_ABS\",SIGN(t.\"TEST_VALUE\") AS \"TEST_SIGNUM\",FLOOR(t.\"TEST_VALUE\") AS \"TEST_FLOOR\",CEIL(t.\"TEST_VALUE\") AS \"TEST_CEILING\",ROUND(t.\"TEST_VALUE\") AS \"TEST_ROUND1\",ROUND(t.\"TEST_VALUE\",?) AS \"TEST_ROUND2\" FROM \"t_math_test\" t",
            "2(Integer)");
}
