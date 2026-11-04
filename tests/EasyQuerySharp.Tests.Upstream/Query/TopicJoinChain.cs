using System.Linq.Expressions;
using EasyQuerySharp.Tests.Upstream.Dto;
using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream;

/// <summary>
/// QueryTest4 / QueryTest5 的 joinTest 系列共用的部分：n 张 t_topic 自连接后，在第一张表上加全套比较与 LIKE 变体、
/// 条件对象、分组、HAVING、排序、分页（Java 用例里带 false 的那一半不生效）。
/// </summary>
internal static class TopicJoinChain
{
    /// <summary>第一张表上的全部比较与 LIKE 变体。Java 的 id 是字符串，ge / gt 等在 C# 里写成 string.Compare(..) &gt;= 0。</summary>
    public static readonly Expression<Func<Topic, bool>> AllComparisons = o =>
        o.Id == "1" && o.Id != "1" && string.Compare(o.Id, "1") >= 0 && string.Compare(o.Id, "1") > 0
        && string.Compare(o.Id, "1") <= 0 && string.Compare(o.Id, "1") < 0
        && o.Id!.Contains("1") && !o.Id.Contains("1") && o.Id.StartsWith("1") && o.Id.EndsWith("1")
        && !o.Id.StartsWith("1") && !o.Id.EndsWith("1");

    public static TopicRequest Request() => new() { CreateTimeBegin = DateTime.Now };

    /// <summary>期望的 SQL：<paramref name="select"/> 是 SELECT 列表，<paramref name="lastTable"/> 是最后一张表的表名。</summary>
    public static string Sql(int tables, string select = "t.`id`,COUNT(t.`id`) AS `stars`", string lastTable = "t_topic") =>
        "SELECT " + select + " FROM `t_topic` t"
        + string.Concat(Enumerable.Range(1, tables - 1).Select(i => $" LEFT JOIN `{(i == tables - 1 ? lastTable : "t_topic")}` t{i} ON t.`id` = t{i}.`id`"))
        + " WHERE t.`id` = ? AND t.`id` = ? AND t.`id` = ? AND t.`create_time` > ? AND t.`id` = ? AND t.`id` <> ? AND t.`id` >= ? AND t.`id` > ? AND t.`id` <= ? AND t.`id` < ?"
        + " AND t.`id` LIKE ? AND t.`id` NOT LIKE ? AND t.`id` LIKE ? AND t.`id` LIKE ? AND t.`id` NOT LIKE ? AND t.`id` NOT LIKE ?"
        + " GROUP BY t.`id` HAVING COUNT(t.`id`) >= ? ORDER BY t.`create_time` ASC,t.`create_time` DESC,t.`create_time` ASC,t.`create_time` DESC LIMIT 2 OFFSET 1";
}
