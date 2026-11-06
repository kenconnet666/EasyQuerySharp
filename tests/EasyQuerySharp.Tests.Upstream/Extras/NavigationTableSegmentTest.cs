using EasyQuerySharp.Tests.Upstream.Mssql;
using SysBankCard = EasyQuerySharp.Tests.Upstream.Mysql8.SysBankCard;
using SysUser = EasyQuerySharp.Tests.Upstream.Mysql8.SysUser;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>导航上的 <c>Sql.AsTableSegment</c> 标记（Java 的 nav().configure(r -> r.asTableSegment(..))，C# 独有的写法，没有对应的 Java 用例）。</summary>
public sealed class NavigationTableSegmentTest
{
    private static string WithRowLock(string table, string? alias) => alias is null ? table + " WITH(ROWLOCK)" : table + " " + alias + " WITH(ROWLOCK)";

    [Fact]
    public void CollectionNavigationSubQuery()
    {
        var statement = SqlOnly.Db("mssql").Query<SysUser>()
            .Where(u => u.BankCards!.AsTableSegment(MsSQLQueryTest.WithNoLock).Any(c => c.Type == "储蓄卡"))
            .ToStatement();
        SqlOnly.AssertStatement(statement,
            "SELECT t.[id],t.[name],t.[phone],t.[age],t.[create_time] FROM [t_sys_user] t WHERE EXISTS(SELECT TOP 1 1 FROM [t_bank_card] t1 WITH(NOLOCK) WHERE t1.[uid] = t.[id] AND t1.[type] = ?)",
            "储蓄卡(String)");
    }

    // 合并成分组 JOIN 时，GroupJoin 的配置里指定的表写法用在派生表里的目标表上
    [Fact]
    public void GroupJoinConfigure()
    {
        var statement = SqlOnly.Db("mssql").Query<SysUser>()
            .GroupJoin(u => u.BankCards, q => q.AsTableSegment(MsSQLQueryTest.WithNoLock))
            .Where(u => u.BankCards!.Any(c => c.Type == "储蓄卡"))
            .ToStatement();
        SqlOnly.AssertStatement(statement,
            "SELECT t.[id],t.[name],t.[phone],t.[age],t.[create_time] FROM [t_sys_user] t LEFT JOIN (SELECT t1.[uid] AS [__group_key1__],(CASE WHEN (COUNT(?) > 0) THEN ? ELSE ? END) AS [__any2__] FROM [t_bank_card] t1 WITH(NOLOCK) WHERE t1.[type] = ? GROUP BY t1.[uid]) t2 ON t2.[__group_key1__] = t.[id] WHERE ISNULL(t2.[__any2__],?) = ?",
            "1(Integer),true(Boolean),false(Boolean),储蓄卡(String),false(Boolean),true(Boolean)");
    }

    // 同一导航带同一个表写法只连一次，带不同写法各连一次
    [Fact]
    public void ToOneNavigationJoinsOncePerSegment()
    {
        var statement = SqlOnly.Db("mssql").Query<SysBankCard>()
            .Where(c => Sql.AsTableSegment(c.User, MsSQLQueryTest.WithNoLock)!.Name == "a" && Sql.AsTableSegment(c.User, MsSQLQueryTest.WithNoLock)!.Phone == "b"
                        && Sql.AsTableSegment(c.User, WithRowLock)!.Age == 1)
            .ToStatement();
        SqlOnly.AssertStatement(statement,
            "SELECT t.[id],t.[uid],t.[code],t.[type],t.[bank_id],t.[open_time] FROM [t_bank_card] t LEFT JOIN [t_sys_user] t1 WITH(NOLOCK) ON t1.[id] = t.[uid] LEFT JOIN [t_sys_user] t2 WITH(ROWLOCK) ON t2.[id] = t.[uid] WHERE t1.[name] = ? AND t1.[phone] = ? AND t2.[age] = ?",
            "a(String),b(String),1(Integer)");
    }
}
