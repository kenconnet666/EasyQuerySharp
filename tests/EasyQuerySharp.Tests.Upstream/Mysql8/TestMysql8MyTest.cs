using EasyQuerySharp.Metadata;
using EasyQuerySharp.TestKit.Databases;
using MySqlConnector;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：testmysql8/MyTest.java（另一个库 easy-query-test123 上的三张后台表：自增主键的基类、text 列）。
/// 实体是 Java 的 testmysql8 包里的 SysUser / SysRole / SysModule，与别处的同名实体无关，写成嵌套类。
/// </summary>
public sealed class TestMysql8MyTest
{
    private const string Database = "easy-query-test123";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public class BaseBean
    {
        [Column(PrimaryKey = true, Generated = true)]
        public int Id { get; set; }

        public DateTime? CreateTime { get; set; }

        public DateTime? UpdateTime { get; set; }
    }

    [Table("t_sys_user")]
    public class SysUser : BaseBean
    {
        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string? RealName { get; set; }

        public int RoleId { get; set; }

        public int AllowLogin { get; set; }
    }

    [Table("t_sys_role")]
    public class SysRole : BaseBean
    {
        public string? Name { get; set; }

        public string? Desc { get; set; }

        [Column(DbType = "text")]
        public string? Auths { get; set; }

        [Column(DbType = "text")]
        public string? Menus { get; set; }
    }

    [Table("t_sys_module")]
    public class SysModule : BaseBean
    {
        public string? Name { get; set; }

        public string? Url { get; set; }

        public string? Pre { get; set; }

        public string? IconClass { get; set; }

        public int IsShowMenu { get; set; }

        public int ParentModuleId { get; set; }

        [Column(DbType = "text")]
        public string? AmisJSON { get; set; }

        public string? AuthList { get; set; }

        public int SortNo { get; set; }
    }

    // Java 先 createDatabaseIfNotExists() 再 syncTable；同步两次，第二次没有要执行的语句
    [Fact, Upstream("testmysql8/MyTest.java#testaa",
         Deviation = "C# 没有 createDatabaseIfNotExists（库由连接串决定，建库属于部署）：测试先用服务器连接建库 easy-query-test123，再同步三张表")]
    public async Task Testaa()
    {
        var serverCs = TestDatabases.MySql.TryConnectionString();
        if (serverCs is null) Assert.Skip(TestDatabases.MySql.SkipReason);
        await using (var server = new MySqlConnection(serverCs))
        {
            await server.OpenAsync(Ct);
            await SqlScript.ExecuteAsync(server, $"CREATE DATABASE IF NOT EXISTS `{Database}` CHARACTER SET utf8mb4;", ct: Ct);
        }

        var db = new EasyDb(o => o.WithJavaNulls().UseMySql(new MySqlDataSource(TestDatabases.MySql.TryConnectionString(Database)!)));
        Type[] tables = [typeof(SysUser), typeof(SysRole), typeof(SysModule)];
        await db.CodeFirst.SyncTablesAsync(tables, Ct);
        Assert.Empty((await db.CodeFirst.PlanSyncAsync(tables, Ct)).Statements);
    }
}
