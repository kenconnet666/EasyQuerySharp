using System.Globalization;
using EasyQuerySharp.TestKit.Databases;
using MySqlConnector;

namespace EasyQuerySharp.Tests.Upstream.Sharding;

/// <summary>
/// 对应 Java 的 ShardingBaseTest：默认数据源 ds2020 是 <c>easy-query-test</c>（<see cref="MySqlFixture"/> 每次重建它，t_topic 也在里面），
/// 另有 ds2021 … ds2023 三个库（每次重建）。87 张分表由分片的 code-first 建出，种子与 Java 的 initData 相同、经分片写入落到各表：
/// <list type="bullet">
/// <item><see cref="TopicSharding"/>：按 id 取模 3 张表（ds2020），500 行；</item>
/// <item><see cref="TopicShardingTime"/>：按月 40 张表（ds2020，2020-01 … 2023-04），id 末 6 位是月份作额外分片键，默认按 create_time 降序；</item>
/// <item><see cref="TopicShardingDataSource"/>：按年分库，四个库里各一张 t_topic_sharding_ds；</item>
/// <item><see cref="TopicShardingDataSourceTime"/>：按年分库、按月分表（ds2020 12 张、ds2021 12 张、ds2022 12 张、ds2023 4 张）。</item>
/// </list>
/// 后三者每天一行，从 2020-01-01 01:01 起共 1216 行。改了数据的用例自己还原。没有配置 MySQL 时用到 <see cref="Db"/> 的测试被跳过。
/// 类级固件：程序集固件（含 <see cref="MySqlFixture"/>）全部建好之后才建它。
/// </summary>
public sealed class ShardingFixture : IAsyncLifetime
{
    /// <summary>每天一行的起点（Java 的 beginTime）。</summary>
    public static readonly DateTime Begin = new(2020, 1, 1, 1, 1, 0);

    /// <summary>每天一行的终点（不含，Java 的 endTime）。</summary>
    public static readonly DateTime End = new(2023, 5, 1, 1, 1, 0);

    /// <summary>按天写入的行数（Java 的 days）。</summary>
    public static readonly int Days = (int)(End - Begin).TotalDays;

    // 按月 / 按年分片的截止时间（Java 的 TopicShardingTimeShardingInitializer.getEndTime 与各库的表清单）
    private static readonly DateTime LastPeriod = new(2023, 4, 30);

    private static readonly Type[] ShardedTypes = [typeof(TopicSharding), typeof(TopicShardingTime), typeof(TopicShardingDataSource), typeof(TopicShardingDataSourceTime)];

    private EasyDb? _db;
    private string? _skipReason;

    /// <summary>分库分表的 EasyDb（对应 Java 的 easyEntityQuery），带 <see cref="SqlCapture"/> 监听。</summary>
    public EasyDb Db
    {
        get
        {
            if (_db is null) Assert.Skip(_skipReason ?? "分片固件没有初始化");
            return _db;
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (TestDatabases.MySql.TryConnectionString() is not { } serverCs)
        {
            _skipReason = TestDatabases.MySql.SkipReason;
            return;
        }
        await using (var server = new MySqlConnection(serverCs))
        {
            await server.OpenAsync();
            foreach (var year in new[] { 2021, 2022, 2023 })
                await SqlScript.ExecuteAsync(server, $"DROP DATABASE IF EXISTS `{YearDatabase(year)}`; CREATE DATABASE `{YearDatabase(year)}` CHARACTER SET utf8mb4;");
        }

        _db = new EasyDb(o =>
        {
            // 与 Java 的 ShardingBaseTest 一致：深分页反向排序的阈值为 10，与 BaseTest 相同的拦截器
            o.ReverseOffsetThreshold = 10;
            o.UseMySql(new MySqlDataSource(TestDatabases.MySql.TryConnectionString(MySqlFixture.Database)!)).WithDefaultDataSourceName("ds2020")
                .AddDataSource("ds2021", new MySqlDataSource(TestDatabases.MySql.TryConnectionString(YearDatabase(2021))!))
                .AddDataSource("ds2022", new MySqlDataSource(TestDatabases.MySql.TryConnectionString(YearDatabase(2022))!))
                .AddDataSource("ds2023", new MySqlDataSource(TestDatabases.MySql.TryConnectionString(YearDatabase(2023))!))
                .AddListener(SqlCapture.Instance)
                .AddInterceptor(new MyEntityInterceptor())
                .AddInterceptor(new Topic1Interceptor())
                .AddInterceptor(new MyTenantInterceptor())
                .AddInterceptor(new TopicUpdateInterceptor());
            ConfigureRules(o);
        });
        // easy-query-test 每次由 MySqlFixture 重建，另外三个库刚重建：直接建 87 张分表
        await _db.CodeFirst.CreateTablesAsync(ShardedTypes);
        await SeedAsync(_db);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// 与 Java 的四个初始化器 + 路由类相同的规则：TopicShardingShardingInitializer / TopicShardingTableRoute（取模 3）、
    /// TopicShardingTimeShardingInitializer / TopicShardingTimeTableRoute（按月，id 末 6 位换算成月份）、
    /// DataSourceShardingInitializer / TopicShardingDataSourceRoute（按年分库）、DataSourceAndTableShardingInitializer + 两个路由（按年分库、按月分表）。
    /// </summary>
    public static void ConfigureRules(EasyDbOptions options) => options
        .Shard<TopicSharding>(r => r.Mod(t => t.Id, 3))
        .Shard<TopicShardingTime>(r => r.ByMonth(t => t.CreateTime, from: Begin, to: () => LastPeriod)
            .ExtraKey(t => t.Id, MonthOfIdTail)
            .DefaultOrder(t => t.CreateTime, descending: true))
        .Shard<TopicShardingDataSource>(r => r.ByYear(t => t.CreateTime, from: Begin, to: () => LastPeriod, dataSource: d => $"ds{d.Year}", splitTable: false)
            .DefaultOrder(t => t.CreateTime, descending: true))
        .Shard<TopicShardingDataSourceTime>(r => r.ByMonth(t => t.CreateTime, from: Begin, to: () => LastPeriod, dataSource: d => $"ds{d.Year}")
            .DefaultOrder(t => t.CreateTime, descending: true));

    // Java 的 getIdRouteFilter：id 末 6 位是 yyyyMM；不够 6 位或不是月份时不裁剪
    private static object? MonthOfIdTail(string? id) =>
        id is { Length: > 6 } && DateTime.TryParseExact(id[^6..], "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month) ? month : null;

    private static string YearDatabase(int year) => $"{MySqlFixture.Database}{year}";

    // 与 Java ShardingBaseTest.initData 相同的数据（批量写入，各行按分片键落到各自的表与库）
    private static async Task SeedAsync(EasyDb db)
    {
        var now = DateTime.Now;
        await db.Insert(Enumerable.Range(0, 500).Select(i => new TopicSharding
        {
            Id = Text(i), Title = "title" + i, Stars = i, CreateTime = now.AddMinutes(i),
        })).Batch().ExecuteAsync();
        await db.Insert(Daily((id, title, stars, time) => new TopicShardingTime { Id = id, Title = title, Stars = stars, CreateTime = time })).Batch().ExecuteAsync();
        await db.Insert(Daily((id, title, stars, time) => new TopicShardingDataSourceTime { Id = id, Title = title, Stars = stars, CreateTime = time })).Batch().ExecuteAsync();
        await db.Insert(Daily((id, title, stars, time) => new TopicShardingDataSource { Id = id, Title = title, Stars = stars, CreateTime = time })).Batch().ExecuteAsync();
    }

    // 每天一行：id 是去掉横线的 UUID 加上 yyyyMM，标题 title + yyyyMM，stars 是第几天
    private static List<T> Daily<T>(Func<string, string, int, DateTime, T> create) =>
    [
        .. Enumerable.Range(0, Days).Select(i =>
        {
            var time = Begin.AddDays(i);
            var month = time.ToString("yyyyMM", CultureInfo.InvariantCulture);
            return create(Guid.NewGuid().ToString("N") + month, "title" + month, i, time);
        }),
    ];

    private static string Text(int i) => i.ToString(CultureInfo.InvariantCulture);
}
