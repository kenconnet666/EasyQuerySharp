using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Kingbase;

/// <summary>
/// 对应 Java：kingbase/MyKingbaseESTest.java。名字是 Kingbase，两个用例实际连的是开发者本机 MySQL（3316 端口）上的现成数据：
/// 多对多带出，导航上声明了排序，中间表换出的目标键比 relationGroupSize 多时也只查一条（IN 按分组大小拆成 OR），
/// 每个专题的视频按 order_by 升序。分批查询会把一个专题的视频拆到几批里、各批分别排序，拼起来就乱了。
/// </summary>
public sealed class MyKingbaseESTest : IAsyncLifetime
{
    private const string Deviation = "Java 连开发者本机 MySQL 上的现成数据（3 个专题、1074 个视频），这里没有那份数据，"
                                     + "改在 SQLite 内存库里造同样规模的数据（视频的 order_by 与键的顺序相反）：SQL 只差引号，第三条的参数是造的键";
    private const string Database = "Data Source=eqs-kingbase-test;Mode=Memory;Cache=Shared";
    private const int VideoCount = 1074;
    private static readonly string[] SubjectIds = ["1094971186229739525", "1097211886191181829", "1102206513981882373"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 共享内存库只要还有一个连接开着就一直在
    private readonly SqliteConnection _keepAlive = new(Database);

    public async ValueTask InitializeAsync()
    {
        await _keepAlive.OpenAsync(Ct);
        var db = Db(512);
        Type[] tables = [typeof(HelpEduSubjectEntity), typeof(HelpEduVideoEntity), typeof(HelpCategoryAndSubjectEntity)];
        await db.CodeFirst.DropTablesIfExistsAsync(tables, Ct);
        await db.CodeFirst.CreateTablesAsync(tables, Ct);
        await db.Insert(SubjectIds.Select((id, i) => new HelpEduSubjectEntity { Id = id, Name = "专题" + i, OrderBy = i })).ExecuteAsync(Ct);
        await db.Insert(Enumerable.Range(0, VideoCount).Select(i => new HelpEduVideoEntity { Id = VideoId(i), OrderBy = VideoCount - i })).ExecuteAsync(Ct);
        await db.Insert(Enumerable.Range(0, VideoCount).Select(i => new HelpCategoryAndSubjectEntity { Id = "m" + i, SubjectId = SubjectIds[i % 3], ContextId = VideoId(i) }))
            .ExecuteAsync(Ct);
    }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    // relationGroupSize 1512：1074 个视频一个 IN
    [Fact, Upstream("kingbase/MyKingbaseESTest.java#testaa", Deviation = Deviation)]
    public async Task Testaa()
    {
        using var listen = SqlCapture.Listen();
        AssertOrdered(await Db(1512).Query<HelpEduSubjectEntity>().Include(h => h.HelpEduVideoList).ToListAsync(Ct));
        AssertSubjectAndLinkSql(listen);
        listen.AssertSql(2, VideosSql + "t.\"id\" IN (" + Marks(VideoCount) + ") ORDER BY t.\"order_by\" ASC");
        AssertVideoParams(listen);
    }

    // relationGroupSize 512：仍是一条查询，IN 拆成 512、512、50 三段 OR
    [Fact, Upstream("kingbase/MyKingbaseESTest.java#testaa1", Deviation = Deviation)]
    public async Task Testaa1()
    {
        using var listen = SqlCapture.Listen();
        AssertOrdered(await Db(512).Query<HelpEduSubjectEntity>().Include(h => h.HelpEduVideoList).ToListAsync(Ct));
        AssertSubjectAndLinkSql(listen);
        listen.AssertSql(2, VideosSql + "(t.\"id\" IN (" + Marks(512) + ") OR t.\"id\" IN (" + Marks(512) + ") OR t.\"id\" IN (" + Marks(50) + ")) ORDER BY t.\"order_by\" ASC");
        AssertVideoParams(listen);
    }

    private const string VideosSql =
        "SELECT t.\"id\",t.\"create_time\",t.\"update_time\",t.\"create_by\",t.\"update_by\",t.\"deleted\",t.\"delete_by\",t.\"delete_time\",t.\"order_by\" FROM \"help_edu_video\" t WHERE ";

    private static EasyDb Db(int groupSize) => new(o =>
    {
        o.UseSqlite(SqliteFactory.Instance.CreateDataSource(Database)).AddListener(SqlCapture.Instance);
        o.IncludeGroupSize = groupSize;
    });

    private static string VideoId(int i) => "v" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);

    private static string Marks(int count) => string.Join(",", Enumerable.Repeat("?", count));

    // Java 的检查：每个专题的视频按 order_by 不减
    private static void AssertOrdered(List<HelpEduSubjectEntity> subjects)
    {
        Assert.Equal(SubjectIds, subjects.Select(s => s.Id));
        foreach (var subject in subjects)
        {
            Assert.Equal(VideoCount / 3, subject.HelpEduVideoList!.Count);
            Assert.Equal(subject.HelpEduVideoList.Select(v => v.OrderBy).Order(), subject.HelpEduVideoList.Select(v => v.OrderBy));
        }
    }

    private static void AssertSubjectAndLinkSql(SqlCapture.Scope listen)
    {
        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT \"id\",\"create_time\",\"update_time\",\"create_by\",\"update_by\",\"deleted\",\"delete_by\",\"delete_time\",\"name\",\"order_by\" FROM \"help_edu_subject\"");
        listen.AssertSql(1, "SELECT \"subject_id\",\"context_id\" FROM \"help_category_and_subject\" WHERE \"subject_id\" IN (?,?,?)");
        listen.AssertParams(1, "1094971186229739525(String),1097211886191181829(String),1102206513981882373(String)");
    }

    // 第三条的参数是全部视频的键，各一次
    private static void AssertVideoParams(SqlCapture.Scope listen) =>
        Assert.Equal(Enumerable.Range(0, VideoCount).Select(VideoId).Order(), listen.Events[2].Statement.Parameters.Select(p => (string)p.Value!).Order());
}
