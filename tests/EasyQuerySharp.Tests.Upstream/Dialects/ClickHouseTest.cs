using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Dialects;

/// <summary>
/// 对应 Java：click/ClickHouseTest.java（4 个用例）。ClickHouse 的更新、删除是 mutation：<c>ALTER TABLE … UPDATE / DELETE WHERE …</c>。
/// Java 连一个不存在的库执行、在监听器里取 SQL；这里只渲染不执行。列名按 Java 的 ClickHouseBaseTest 用 UpperUnderlined 命名。
/// </summary>
public sealed class ClickHouseTest
{
    internal static EasyDb ClickHouse { get; } = new(o =>
    {
        o.WithJavaNulls();
        o.UseClickHouse();
        o.NameConversion = NameConversion.UpperUnderlined;
    });

    [Fact, Upstream("click/ClickHouseTest.java#testUpdate1")]
    public void TestUpdate1() =>
        SqlOnly.AssertStatement(ClickHouse.Update<Topic>().Set(t => t.Title, "123").WhereById("123123").ToStatement(),
            "ALTER TABLE `t_topic` UPDATE `TITLE` = ? WHERE `ID` = ?", "123(String),123123(String)");

    [Fact, Upstream("click/ClickHouseTest.java#testUpdate2")]
    public void TestUpdate2() =>
        SqlOnly.AssertStatement(ClickHouse.Update(new Topic { Id = "123", Title = "456" }).ToStatement(),
            "ALTER TABLE `t_topic` UPDATE `STARS` = ?,`TITLE` = ?,`CREATE_TIME` = ? WHERE `ID` = ?", "null(null),456(String),null(null),123(String)");

    [Fact, Upstream("click/ClickHouseTest.java#testDelete1")]
    public void TestDelete1() =>
        SqlOnly.AssertStatement(ClickHouse.Delete<Topic>().WhereById("123123").ToStatement()!,
            "ALTER TABLE `t_topic` DELETE WHERE `ID` = ?", "123123(String)");

    [Fact, Upstream("click/ClickHouseTest.java#testDelete2")]
    public void TestDelete2() =>
        SqlOnly.AssertStatement(ClickHouse.Delete(new Topic { Id = "123", Title = "456" }).ToStatement()!,
            "ALTER TABLE `t_topic` DELETE WHERE `ID` = ?", "123(String)");
}
