namespace EasyQuerySharp.Tests.Upstream.DuckDb;

/// <summary>
/// 对应 Java：duckdb/AllTypeTest.java（各种列类型写入后读回，值不变）。
/// Java 的 Byte 对应 sbyte、java.util.Date 对应 DateTime（取到毫秒，与 Java 的精度一致）、java.sql.Date 对应 DateOnly、java.sql.Time 对应 TimeOnly。
/// </summary>
public sealed class AllTypeTest(DuckDbFixture fixture) : DuckDbTest(fixture)
{
    private static ALLTYPE NewAllType(string id)
    {
        var now = DateTime.Now;
        return new ALLTYPE
        {
            Id = id,
            NumberDecimal = 12.33m,
            NumberFloat = 12.3f,
            NumberDouble = 22.1d,
            NumberShort = 12,
            NumberInteger = 33,
            NumberLong = 12345678911L,
            NumberByte = -1,
            Enable = true,
            TimeLocalDateTime = new DateTime(2021, 1, 1, 0, 0, 0),
            TimeLocalDate = new DateOnly(2121, 1, 2),
            TimeLocalTime = new TimeOnly(21, 1, 9),
            OnlyDate = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond),
            SqlDate = DateOnly.FromDateTime(now),
            OnlyTime = new TimeOnly(12, 9, 10),
            Value = "3322",
            Uid = Guid.NewGuid(),
            NumberFloatBasic = 12.3f,
            NumberDoubleBasic = 22.1d,
            NumberShortBasic = 12,
            NumberIntegerBasic = 33,
            NumberLongBasic = 12345678911L,
            NumberByteBasic = -1,
            EnableBasic = true,
        };
    }

    [Fact, Upstream("duckdb/AllTypeTest.java#testAllType")]
    public async Task TestAllType()
    {
        var expected = NewAllType("123456");
        Assert.Equal(1, await Db.Insert(expected).ExecuteAsync(Ct));
        var actual = await Db.Query<ALLTYPE>().WhereById("123456").FirstOrDefaultAsync(Ct);
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.NumberDecimal, actual.NumberDecimal);
        Assert.Equal(expected.NumberFloat, actual.NumberFloat);
        Assert.Equal(expected.NumberDouble, actual.NumberDouble);
        Assert.Equal(expected.NumberShort, actual.NumberShort);
        Assert.Equal(expected.NumberInteger, actual.NumberInteger);
        Assert.Equal(expected.NumberLong, actual.NumberLong);
        Assert.Equal(expected.NumberByte, actual.NumberByte);
        Assert.Equal(expected.TimeLocalDateTime, actual.TimeLocalDateTime);
        Assert.Equal(expected.TimeLocalDate, actual.TimeLocalDate);
        Assert.Equal(expected.TimeLocalTime, actual.TimeLocalTime);
        Assert.Equal(expected.Enable, actual.Enable);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.Uid, actual.Uid);
        Assert.Equal(expected.SqlDate, actual.SqlDate);
        Assert.Equal(expected.OnlyDate, actual.OnlyDate);
        Assert.Equal(expected.OnlyTime, actual.OnlyTime);
        Assert.Equal(expected.NumberFloatBasic, actual.NumberFloatBasic);
        Assert.Equal(expected.NumberDoubleBasic, actual.NumberDoubleBasic);
        Assert.Equal(expected.NumberShortBasic, actual.NumberShortBasic);
        Assert.Equal(expected.NumberIntegerBasic, actual.NumberIntegerBasic);
        Assert.Equal(expected.NumberLongBasic, actual.NumberLongBasic);
        Assert.Equal(expected.NumberByteBasic, actual.NumberByteBasic);
        Assert.Equal(expected.EnableBasic, actual.EnableBasic);
    }

    // Java 的 onConflictThen(s -> s.FETCHER.allFields())：主键冲突时更新全部列；这里的主键是新的，走的是插入
    [Fact, Upstream("duckdb/AllTypeTest.java#testAllType1")]
    public async Task TestAllType1()
    {
        using var listen = SqlCapture.Listen();
        var rows = await Db.Insert(NewAllType("123456123123")).OnConflictUpdate().ExecuteAsync(Ct);
        Assert.Equal(1, rows);
        Assert.Contains("ON CONFLICT (\"id\") DO UPDATE SET \"number_decimal\" = EXCLUDED.\"number_decimal\"", listen.Last.Statement.Text);
    }
}
