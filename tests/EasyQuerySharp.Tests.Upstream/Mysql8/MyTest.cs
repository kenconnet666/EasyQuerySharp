using EasyQuerySharp.Metadata;
using EasyQuerySharp.TestKit.Databases;
using MySqlConnector;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：testmysql8/MyTest.java（独立的库 easy-query-test123，列名就是属性名，实体共用带自增主键的基类）。
/// 实体与 mysql8 的同名实体无关，嵌在这里。
/// </summary>
public sealed class MyTest
{
    private const string Database = "easy-query-test123";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>对应 Java：testmysql8/BaseBean。</summary>
    public abstract class BaseBean
    {
        [Column(PrimaryKey = true, Generated = true)]
        public int Id { get; set; }

        public DateTime? CreateTime { get; set; }

        public DateTime? UpdateTime { get; set; }
    }

    /// <summary>对应 Java：testmysql8/SysUser。</summary>
    [Table("t_sys_user")]
    public class SysUser : BaseBean
    {
        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string? RealName { get; set; }

        public int RoleId { get; set; }

        public int AllowLogin { get; set; }
    }

    /// <summary>对应 Java：testmysql8/SysRole。</summary>
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

    /// <summary>对应 Java：testmysql8/SysModule。</summary>
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

    // 建库、建三张表，之后能查（Java 的 PropertyFirstEntityMappingRule：列名取属性名，对应 LowerCamelCase）
    [Fact, Upstream("testmysql8/MyTest.java#testaa", Deviation = "C# 的 code-first 不建库（Java 的 createDatabaseIfNotExists），用驱动连接执行 CREATE DATABASE IF NOT EXISTS；"
                                                                + "表已存在时 CreateTablesAsync 跳过，不做 Java syncTable 的补列（只增不删的同步归 4.1）")]
    public async Task TestAa()
    {
        await using (var server = new MySqlConnection(TestDatabases.MySql.ConnectionString()))
        {
            await server.OpenAsync(Ct);
            await SqlScript.ExecuteAsync(server, $"CREATE DATABASE IF NOT EXISTS `{Database}` CHARACTER SET utf8mb4;", ct: Ct);
        }
        var db = new EasyDb(o =>
        {
            o.NameConversion = NameConversion.LowerCamelCase;
            o.UseMySql(new MySqlDataSource(TestDatabases.MySql.ConnectionString(Database)));
        });
        await db.CodeFirst.CreateTablesAsync([typeof(SysUser), typeof(SysRole), typeof(SysModule)], Ct);

        await db.Query<SysUser>().Where(u => u.UserName == "admin" && u.AllowLogin == 1).ToListAsync(Ct);
        await db.Query<SysRole>().Where(r => r.Desc != null).ToListAsync(Ct);
        await db.Query<SysModule>().OrderBy(m => m.SortNo).ToListAsync(Ct);
    }
}
