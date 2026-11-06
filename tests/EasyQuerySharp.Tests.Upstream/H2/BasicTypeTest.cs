namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/BasicTypeTest.java（各基本类型的常量与列投影：参数的类型文本、读回的值）。改在 SQLite 上跑，SQL 与 Java 相同（见 <see cref="H2Fixture"/>）。
/// Java 的 new XxxProxy(值 / 列) 在 C# 直接写成投影的值或列；Java 的 Byte、Time 对应 C# 的 sbyte、TimeSpan。
/// </summary>
public sealed class BasicTypeTest(H2Fixture fixture) : H2Test(fixture)
{
    /// <summary>固件里那一行 ALLTYPE 的主键（Java initData 写入的同一行）。</summary>
    public const string SampleId = "123xxxxxxx1qq";

    private const string ConstantSql = "SELECT ? FROM t_all_type t WHERE t.id = ? LIMIT 1";

    private Query<ALLTYPE> Sample => Db.Query<ALLTYPE>().Where(a => a.Id == SampleId);

    // 投影一个常量：参数化后原样读回
    private async Task AssertConstant<T>(T value, string expectedParam)
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal(value, await Sample.Select(t => value).FirstAsync(Ct));
        listen.AssertSql(ConstantSql);
        listen.AssertParams(expectedParam + ",123xxxxxxx1qq(String)");
    }

    // 投影一列：读回种子数据里的值
    private async Task AssertColumn<T>(System.Linq.Expressions.Expression<Func<ALLTYPE, T>> column, T expected, string expectedColumn)
    {
        using var listen = SqlCapture.Listen();
        Assert.Equal(expected, await Sample.Select(column).FirstAsync(Ct));
        listen.AssertSql($"SELECT t.{expectedColumn} FROM t_all_type t WHERE t.id = ? LIMIT 1");
        listen.AssertParams("123xxxxxxx1qq(String)");
    }

    [Fact, Upstream("h2/BasicTypeTest.java#booleanTest1")]
    public async Task BooleanTest1()
    {
        using var listen = SqlCapture.Listen();
        Assert.True(await Db.Query<ALLTYPE>().Select(t => true).FirstAsync(Ct));
        listen.AssertSql("SELECT ? FROM t_all_type t LIMIT 1");
        listen.AssertParams("true(Boolean)");
    }

    [Fact, Upstream("h2/BasicTypeTest.java#booleanTest2")]
    public async Task BooleanTest2()
    {
        using var listen = SqlCapture.Listen();
        Assert.True(await Db.Query<ALLTYPE>().Where(a => a.Enable == true).Select(t => t.Enable).FirstAsync(Ct));
        listen.AssertSql("SELECT t.enable FROM t_all_type t WHERE t.enable = ? LIMIT 1");
        listen.AssertParams("true(Boolean)");
    }

    [Fact, Upstream("h2/BasicTypeTest.java#booleanTest3")]
    public async Task BooleanTest3()
    {
        using var listen = SqlCapture.Listen();
        Assert.True(await Db.Query<ALLTYPE>().Where(a => a.EnableBasic == true).Select(t => t.EnableBasic).FirstAsync(Ct));
        listen.AssertSql("SELECT t.enable_basic FROM t_all_type t WHERE t.enable_basic = ? LIMIT 1");
        listen.AssertParams("true(Boolean)");
    }

    [Fact, Upstream("h2/BasicTypeTest.java#doubleTest1")]
    public Task DoubleTest1() => AssertConstant(1.1d, "1.1(Double)");

    [Fact, Upstream("h2/BasicTypeTest.java#doubleTest2")]
    public Task DoubleTest2() => AssertColumn(t => t.NumberDouble, 22.1d, "number_double");

    [Fact, Upstream("h2/BasicTypeTest.java#doubleTest3")]
    public Task DoubleTest3() => AssertColumn(t => t.NumberDoubleBasic, 22.1d, "number_double_basic");

    [Fact, Upstream("h2/BasicTypeTest.java#floatTest1")]
    public Task FloatTest1() => AssertConstant(12.3f, "12.3(Float)");

    [Fact, Upstream("h2/BasicTypeTest.java#floatTest2")]
    public Task FloatTest2() => AssertColumn(t => t.NumberFloat, 12.3f, "number_float");

    [Fact, Upstream("h2/BasicTypeTest.java#floatTest3")]
    public Task FloatTest3() => AssertColumn(t => t.NumberFloatBasic, 12.3f, "number_float_basic");

    [Fact, Upstream("h2/BasicTypeTest.java#intTest1")]
    public Task IntTest1() => AssertConstant(33, "33(Integer)");

    [Fact, Upstream("h2/BasicTypeTest.java#intTest2")]
    public Task IntTest2() => AssertColumn(t => t.NumberInteger, 33, "number_integer");

    [Fact, Upstream("h2/BasicTypeTest.java#intTest3")]
    public Task IntTest3() => AssertColumn(t => t.NumberIntegerBasic, 33, "number_integer_basic");

    [Fact, Upstream("h2/BasicTypeTest.java#longTest1")]
    public Task LongTest1() => AssertConstant(12345678911L, "12345678911(Long)");

    [Fact, Upstream("h2/BasicTypeTest.java#longTest2")]
    public Task LongTest2() => AssertColumn(t => t.NumberLong, 12345678911L, "number_long");

    [Fact, Upstream("h2/BasicTypeTest.java#longTest3")]
    public Task LongTest3() => AssertColumn(t => t.NumberLongBasic, 12345678911L, "number_long_basic");

    [Fact, Upstream("h2/BasicTypeTest.java#shortTest1")]
    public Task ShortTest1() => AssertConstant((short)12, "12(Short)");

    [Fact, Upstream("h2/BasicTypeTest.java#shortTest2")]
    public Task ShortTest2() => AssertColumn(t => t.NumberShort, (short)12, "number_short");

    [Fact, Upstream("h2/BasicTypeTest.java#shortTest3")]
    public Task ShortTest3() => AssertColumn(t => t.NumberShortBasic, (short)12, "number_short_basic");

    [Fact, Upstream("h2/BasicTypeTest.java#localDateTimeTest1")]
    public Task LocalDateTimeTest1() => AssertConstant(new DateTime(2021, 1, 1, 0, 0, 0), "2021-01-01T00:00(LocalDateTime)");

    [Fact, Upstream("h2/BasicTypeTest.java#localDateTimeTest2")]
    public Task LocalDateTimeTest2() => AssertColumn(t => t.TimeLocalDateTime, new DateTime(2021, 1, 1, 0, 0, 0), "time_local_date_time");

    [Fact, Upstream("h2/BasicTypeTest.java#localDateTest1")]
    public Task LocalDateTest1() => AssertConstant(new DateOnly(2121, 1, 2), "2121-01-02(LocalDate)");

    [Fact, Upstream("h2/BasicTypeTest.java#localDateTest2")]
    public Task LocalDateTest2() => AssertColumn(t => t.TimeLocalDate, new DateOnly(2121, 1, 2), "time_local_date");

    [Fact, Upstream("h2/BasicTypeTest.java#localTimeTest1")]
    public Task LocalTimeTest1() => AssertConstant(new TimeOnly(21, 1, 9), "21:01:09(LocalTime)");

    [Fact, Upstream("h2/BasicTypeTest.java#localTimeTest2")]
    public Task LocalTimeTest2() => AssertColumn(t => t.TimeLocalTime, new TimeOnly(21, 1, 9), "time_local_time");

    [Fact, Upstream("h2/BasicTypeTest.java#timeTest1")]
    public Task TimeTest1() => AssertConstant(new TimeSpan(12, 9, 10), "12:09:10(Time)");

    [Fact, Upstream("h2/BasicTypeTest.java#timeTest2")]
    public Task TimeTest2() => AssertColumn(t => t.OnlyTime, new TimeSpan(12, 9, 10), "only_time");

    [Fact, Upstream("h2/BasicTypeTest.java#byteTest1")]
    public Task ByteTest1() => AssertConstant((sbyte)-1, "-1(Byte)");

    [Fact, Upstream("h2/BasicTypeTest.java#byteTest2")]
    public Task ByteTest2() => AssertColumn(t => t.NumberByte, (sbyte)-1, "number_byte");

    [Fact, Upstream("h2/BasicTypeTest.java#byteTest3")]
    public Task ByteTest3() => AssertColumn(t => t.NumberByteBasic, (sbyte)-1, "number_byte_basic");
}
