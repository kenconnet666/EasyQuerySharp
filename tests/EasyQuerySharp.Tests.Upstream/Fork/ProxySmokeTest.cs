namespace EasyQuerySharp.Tests.Upstream.Fork;

/// <summary>对应 Java fork：ProxySmokeTest（对一导航的隐式 JOIN 与集合导航的 EXISTS 在测试实体上照常渲染）。</summary>
public sealed class ProxySmokeTest
{
    // fork 的源码不在本机，快照只有 SQL：参数取的是能生成这条 SQL 的任意值
    [Fact, Upstream("fork/ProxySmokeTest.java#implicitJoinAndSubqueryRenderOnTestEntities")]
    public void ImplicitJoinAndSubqueryRenderOnTestEntities() =>
        SqlOnly.AssertStatement(
            SqlOnly.Db("mysql").Query<ForkUser>().Where(u => u.Company!.Name == "acme" && u.Posts!.Any(p => p.Title!.Contains("smoke"))).ToStatement(),
            "SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`attrs`,t.`created_at` FROM `t_fork_user` t LEFT JOIN `t_fork_company` t1 ON t1.`id` = t.`company_id` WHERE t1.`name` = ? AND EXISTS(SELECT 1 FROM `t_fork_post` t2 WHERE t2.`user_id` = t.`id` AND t2.`title` LIKE ? LIMIT 1)",
            "acme(String),%smoke%(String)");
}