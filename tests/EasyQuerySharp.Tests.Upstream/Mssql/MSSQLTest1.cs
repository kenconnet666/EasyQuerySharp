namespace EasyQuerySharp.Tests.Upstream.Mssql;

/// <summary>
/// 对应 Java：mssql/MSSQLTest1.java（MsSQLBaseTest2：默认命名约定，表不存在，用例只比对 SQL）。
/// </summary>
public sealed class MSSQLTest1
{
    // 先联表再投影成 DTO，之后对 DTO 的列过滤：投影包成派生表，条件落在外层
    [Fact, Upstream("mssql/MSSQLTest1.java#test1")]
    public void Test1()
    {
        var statement = SqlOnly.Db("mssql").Query<MSSQLA>()
            .LeftJoin<MSSQLB>((a, b) => a.Id == b.Id)
            .Select((a, b) => new MSSQLADTO { Code = a.Code, Name = b.Code })
            .Where(d => d.Name == "123" && d.Code == "123")
            .ToStatement();
        SqlOnly.AssertStatement(statement,
            "SELECT t2.[code] AS [code],t2.[name] AS [name] FROM (SELECT t.[Code] AS [code],t1.[Code] AS [name] FROM [t_mssql_a] t LEFT JOIN [t_mssql_b] t1 ON t.[Id] = t1.[Id]) t2 WHERE t2.[name] = ? AND t2.[code] = ?",
            "123(String),123(String)");
    }
}
