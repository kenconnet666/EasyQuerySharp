using DuckDB.NET.Data;
using EasyQuerySharp.Tests.Upstream.Mysql8;
using EasyQuerySharp.TestKit.Databases;

[assembly: AssemblyFixture(typeof(EasyQuerySharp.Tests.Upstream.DuckDb.DuckDbFixture))]

namespace EasyQuerySharp.Tests.Upstream.DuckDb;

/// <summary>
/// 对应 Java 的 duckdb/DuckDBBaseTest：每次测试运行在临时文件里重建 DuckDB 库，code-first 建表后写入同样的博客种子数据。
/// DuckDB 在进程内运行，不需要配置，CI 与本地都直接跑。数学函数用例的 <c>t_math_test</c> 由用例自己建表写入
/// （与 MySQL、PG 的数学用例共用同一份固定数据，不是 Java 的 10000 行随机数）。
/// </summary>
public sealed class DuckDbFixture : IAsyncLifetime
{
    // Java 在 initData 里 syncTable 的表（TestJdjg 没有用例用到，不建）
    private static readonly Type[] Tables = [typeof(BlogEntity), typeof(ALLTYPE), typeof(MathTest), typeof(M8ToMany1), typeof(M8ToMany2), typeof(M8ToMany3)];

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"eqs_upstream_{Environment.ProcessId}.duckdb");

    // DuckDB.NET 在最后一个连接关闭时关库（落盘），每条语句都重新开关会慢得多：固件存活期间留一个连接不关
    private DuckDBConnection? _anchor;

    /// <summary>连到固件库的 EasyDb，带 <see cref="SqlCapture"/> 监听。</summary>
    public EasyDb Db { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        DeleteFiles();
        var connectionString = $"Data Source={_file}";
        _anchor = new DuckDBConnection(connectionString);
        _anchor.Open();
        Db = new EasyDb(o => o.WithJavaNulls().UseDuckDb(TestDatabases.DuckDb.Factory.CreateDataSource(connectionString)).AddListener(SqlCapture.Instance));
        await Db.CodeFirst.DropTablesIfExistsAsync(Tables);
        await Db.CodeFirst.CreateTablesAsync(Tables);
        await SeedAsync(Db);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _anchor?.Dispose();
        DeleteFiles();
        return ValueTask.CompletedTask;
    }

    // 删不掉（别的进程占用）不影响测试结果，下次运行会先清掉
    private void DeleteFiles()
    {
        foreach (var path in new[] { _file, _file + ".wal" })
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }

    // 与 Java DuckDBBaseTest.initData 相同的 100 条博客
    private static async Task SeedAsync(EasyDb db)
    {
        var begin = new DateTime(2020, 1, 1, 1, 1, 1);
        await db.Insert(Enumerable.Range(0, 100).Select(i => new BlogEntity
        {
            Id = i.ToString(),
            CreateBy = i.ToString(),
            CreateTime = begin.AddDays(i),
            UpdateBy = i.ToString(),
            UpdateTime = begin.AddDays(i),
            Title = "title" + i,
            Content = "content" + i,
            Url = "http://blog.easy-query.com/" + i,
            Star = i,
            Score = 1.2m,
            Status = i % 3 == 0 ? 0 : 1,
            Order = 1.2m * i,
            IsTop = i % 2 == 0,
            Top = i % 2 == 0,
            Deleted = false,
        })).ExecuteAsync();
    }
}

/// <summary>用 DuckDB 固件的测试基类（对应 Java 的 DuckDBBaseTest 子类）。</summary>
public abstract class DuckDbTest(DuckDbFixture fixture)
{
    /// <summary>连到固件库的 EasyDb（对应 Java 的 easyEntityQuery）。</summary>
    protected EasyDb Db => fixture.Db;

    /// <summary>当前测试的取消令牌。</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;
}
