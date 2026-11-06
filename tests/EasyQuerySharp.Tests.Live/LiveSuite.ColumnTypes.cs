using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Live;

// 列类型（TS 的 extension column types 与 uuid / enum 两组）：decimal、超过 2^53 的 long、DateOnly、TimeOnly、byte[]、Guid、枚举
// 的往返、比较与运算；表用 code-first 建，建好后再比较没有要同步的语句
public abstract partial class LiveSuite
{
    /// <summary>各种 .NET 标量类型的列（对应 TS 的 Ext：decimal.js、bigint、@internationalized/date）。</summary>
    [Table("t_live_measure")]
    public sealed class Measure
    {
        [Column(PrimaryKey = true)]
        public int Id { get; set; }

        [Column(DbType = "DECIMAL(20,2)")]
        public decimal Amount { get; set; }

        public long Big { get; set; }

        public DateOnly Day { get; set; }

        public DateTime StartsAt { get; set; }

        public TimeOnly Clock { get; set; }

        public byte[]? Raw { get; set; }
    }

    /// <summary>2^53 + 1：double 表示不了，往返后必须一模一样。</summary>
    private const long Beyond53Bits = 9007199254740993;

    private static Measure NewMeasure(int id, decimal amount, long big) => new()
    {
        Id = id, Amount = amount, Big = big, Day = new DateOnly(2026, 1, 2), StartsAt = new DateTime(2026, 1, 2, 3, 4, 5, 6),
        Clock = new TimeOnly(8, 30, 15), Raw = [0, 1, 2, 254, 255],
    };

    private async Task<EasyDb> MeasuresAsync(params Measure[] rows)
    {
        var db = await SeededAsync();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(Measure)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(Measure)], Ct);
        await db.Insert(rows).ExecuteAsync(Ct);
        return db;
    }

    [Fact]
    public async Task RoundTripsDecimalsLongsDatesTimesAndBytes()
    {
        var db = await MeasuresAsync(NewMeasure(1, 10.50m, Beyond53Bits));
        var back = await db.Query<Measure>().SingleAsync(Ct);
        Assert.Equal(10.50m, back.Amount);
        Assert.Equal(Beyond53Bits, back.Big);
        Assert.Equal(new DateOnly(2026, 1, 2), back.Day);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, 6), back.StartsAt);
        Assert.Equal(new TimeOnly(8, 30, 15), back.Clock);
        Assert.Equal([0, 1, 2, 254, 255], back.Raw);
        Assert.Empty((await db.CodeFirst.PlanSyncAsync([typeof(Measure)], Ct)).Statements);
    }

    [Fact]
    public async Task ComparesAndFiltersWithTheseTypes()
    {
        var db = await MeasuresAsync(NewMeasure(1, 10.50m, Beyond53Bits), NewMeasure(2, 0.25m, 1));
        Assert.Equal(1, await db.Query<Measure>().Where(m => m.Amount > 5m).CountAsync(Ct));
        Assert.Equal(1, await db.Query<Measure>().Where(m => m.Big == Beyond53Bits).CountAsync(Ct));
        Assert.Equal(2, await db.Query<Measure>().Where(m => m.Day >= new DateOnly(2026, 1, 2)).CountAsync(Ct));
        Assert.Equal(0, await db.Query<Measure>().Where(m => m.Day > new DateOnly(2026, 1, 2)).CountAsync(Ct));
        Assert.Equal(2, await db.Query<Measure>().Where(m => m.StartsAt == new DateTime(2026, 1, 2, 3, 4, 5, 6)).CountAsync(Ct));
        Assert.Equal(2, await db.Query<Measure>().Where(m => m.Clock == new TimeOnly(8, 30, 15)).CountAsync(Ct));
        var parts = await db.Query<Measure>().Where(m => m.Id == 1).Select(m => new Draft<int, int, int, int>(m.Day.Year, m.StartsAt.Month, m.Day.Day, m.Day.AddDays(30).Month)).SingleAsync(Ct);
        Assert.Equal((2026, 1, 2, 2), (parts.Value1, parts.Value2, parts.Value3, parts.Value4));
    }

    // 浮点算 0.1 + 0.2 会得到 0.30000000000000004；decimal 不会
    [Fact]
    public async Task DoesExactArithmeticAndAggregatesOnDecimalAndLong()
    {
        var db = await MeasuresAsync(NewMeasure(1, 0.10m, Beyond53Bits), NewMeasure(2, 0.20m, 2));
        Assert.Equal(0.30m, await db.Query<Measure>().SumAsync(m => m.Amount, Ct));
        Assert.Equal(0.15, (await db.Query<Measure>().AverageAsync(m => m.Amount, Ct))!.Value, 6);
        Assert.Equal(0.20m, await db.Query<Measure>().MaxAsync(m => m.Amount, Ct));
        var rows = await db.Query<Measure>().OrderBy(m => m.Id).Select(m => new Draft<decimal, decimal, long>(m.Amount * 2, m.Amount + 1.05m, m.Big + 1)).ToListAsync(Ct);
        Assert.Equal([(0.20m, 1.15m, Beyond53Bits + 1), (0.40m, 1.25m, 3L)], rows.Select(r => (r.Value1, r.Value2, r.Value3)));
        Assert.Equal(Beyond53Bits + 2, await db.Query<Measure>().SumAsync(m => m.Big, Ct));

        await db.Update<Measure>().Set(m => m.Amount, m => m.Amount * 3).Where(m => m.Big == 2).ExecuteAsync(Ct);
        Assert.Equal(0.60m, (await db.Query<Measure>().Where(m => m.Big == 2).SingleAsync(Ct)).Amount);
    }

    /// <summary>主键是 Guid（插入时为 null 由生成器给一个 v7）、可空的 Guid 列、超出 int 的计数、按整数与按名字存的枚举（对应 TS 的 Token）。</summary>
    [Table("t_live_token")]
    public sealed class Token
    {
        [Column(PrimaryKey = true, KeyGenerator = typeof(GuidV7Key))]
        public Guid? Id { get; set; }

        public Guid? Owner { get; set; }

        public long Hits { get; set; }

        public TokenLevel Level { get; set; }

        [Column(Converter = typeof(EnumNameConverter<TokenLevel>), DbType = "VARCHAR(10)")]
        public TokenLevel? Named { get; set; }
    }

    /// <summary>令牌等级。</summary>
    public enum TokenLevel
    {
        Low = 1,
        Mid = 2,
        High = 3,
    }

    /// <summary>按时间有序的 Guid（v7）。</summary>
    public sealed class GuidV7Key : IKeyGenerator
    {
        public object NewKey(ColumnMetadata column) => Guid.CreateVersion7();
    }

    [Fact]
    public async Task StoresGuidsAndEnumsFindsAndFiltersByThemAndSyncsToAnEmptyPlan()
    {
        var db = await SeededAsync();
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(Token)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(Token)], Ct);
        var owner = Guid.Parse("0192F3A4-1B2C-7D8E-9F01-23456789ABCD");
        Token[] tokens = [new() { Owner = owner, Hits = 4_000_000_000, Level = TokenLevel.High, Named = TokenLevel.High }, new() { Hits = 0, Level = TokenLevel.Low }];
        await db.Insert(tokens).ExecuteAsync(Ct);
        Assert.All(tokens, t => Assert.Equal(7, t.Id!.Value.Version));

        // 不按 id 排序：SQL Server 的 UNIQUEIDENTIFIER 先比较最后 6 个字节，v7 在那里不是按时间有序的
        var rows = await db.Query<Token>().OrderByDescending(t => t.Hits).ToListAsync(Ct);
        Assert.Equal([(owner, 4_000_000_000L, TokenLevel.High, TokenLevel.High), ((Guid?)null, 0L, TokenLevel.Low, (TokenLevel?)null)],
            rows.Select(r => (r.Owner, r.Hits, r.Level, r.Named)));
        Assert.Equal(tokens.Select(t => t.Id), rows.Select(r => r.Id));

        Assert.Equal(owner, (await db.Query<Token>().FindAsync(tokens[0].Id!.Value, Ct))!.Owner);
        Assert.Equal(1, await db.Query<Token>().Where(t => t.Owner == owner).CountAsync(Ct));
        Guid?[] ids = [tokens[0].Id, tokens[1].Id];
        Assert.Equal(2, await db.Query<Token>().Where(t => ids.Contains(t.Id)).CountAsync(Ct));
        TokenLevel[] levels = [TokenLevel.High, TokenLevel.Mid];
        Assert.Equal([TokenLevel.High], await db.Query<Token>().Where(t => levels.Contains(t.Level)).Select(t => t.Level).ToListAsync(Ct));
        Assert.Equal(1, await db.Query<Token>().Where(t => t.Named == TokenLevel.High).CountAsync(Ct));
        Assert.Empty((await db.CodeFirst.PlanSyncAsync([typeof(Token)], Ct)).Statements);
    }
}
