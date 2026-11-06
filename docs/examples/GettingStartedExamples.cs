using EasyQuerySharp.Execution;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;
using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Docs;

/// <summary>
/// "入门"页：最小的模型、创建 EasyDb、第一条查询与写入。模型只映射示例库 t_company / t_user 的几列，
/// 所以卡片能直接在示例库上跑。
/// </summary>
public static class GettingStartedExamples
{
    // <snippet:start-entities>
    [Table("t_company")]
    public class Company
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public string Name { get; set; } = "";

        public string City { get; set; } = "";

        [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Employee.CompanyId)])]
        public List<Employee>? Employees { get; set; }
    }

    [Table("t_user")]
    public class Employee
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public string Name { get; set; } = "";

        public int? Age { get; set; }

        public long? CompanyId { get; set; }

        [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
        public Company? Company { get; set; }
    }
    // </snippet:start-entities>

    // <snippet:start-listener>
    /// <summary>把每条 SQL 和耗时打到控制台。</summary>
    public sealed class ConsoleSqlLog : IExecutionListener
    {
        public void OnExecuted(SqlExecutionEvent e) =>
            Console.WriteLine($"{e.Elapsed.TotalMilliseconds:0.0} ms, {e.Rows} 行：{e.Statement.Text}");
    }
    // </snippet:start-listener>

    /// <summary>创建 EasyDb（不是卡片，只是保证指南里这段代码能编译）。</summary>
    public static EasyDb CreateDb()
    {
        // <snippet:start-db>
        var db = new EasyDb(o => o
            .UseSqlite(SqliteFactory.Instance.CreateDataSource("Data Source=app.db"))
            .AddListener(new ConsoleSqlLog()));
        // </snippet:start-db>
        return db;
    }

    /// <summary>员工与公司名。</summary>
    public sealed record EmployeeRow(string Name, int? Age, string? Company);

    [Example("start", "条件、隐式 JOIN、排序、投影")]
    public static Task<List<EmployeeRow>> FirstQuery(EasyDb db) =>
        db.Query<Employee>()
            .Where(e => e.Age >= 30 && e.Company!.City == "杭州")
            .OrderBy(e => e.Name)
            .Select(e => new EmployeeRow(e.Name, e.Age, e.Company!.Name))
            .ToListAsync();

    /// <summary>公司与员工数、员工名单。</summary>
    public sealed record CompanyCard(string Name, long Headcount, List<string> Names);

    [Example("start", "隐式子查询与对象树：集合导航上的 Any / Count / Select")]
    public static Task<List<CompanyCard>> CollectionNavigation(EasyDb db) =>
        db.Query<Company>()
            .Where(c => c.Employees!.Any(e => e.Age < 30))
            .OrderBy(c => c.Id)
            .Select(c => new CompanyCard(c.Name, c.Employees!.Count(), c.Employees!.OrderBy(e => e.Id).Select(e => e.Name).ToList()))
            .ToListAsync();

    [Example("start", "分页")]
    public static Task<Page<string>> FirstPage(EasyDb db) =>
        db.Query<Employee>().OrderBy(e => e.Id).Select(e => e.Name).ToPageAsync(1, 3);

    [Example("start", "插入、更新、删除")]
    public static async Task<long> Writes(EasyDb db)
    {
        var company = new Company { Name = "东海数据", City = "宁波" };
        await db.Insert(company).ExecuteAsync();
        await db.Update<Company>().Set(c => c.City, "舟山").Where(c => c.Name == "东海数据").ExecuteAsync();
        return await db.Delete<Company>().Where(c => c.Name == "东海数据").ExecuteAsync();
    }
}
