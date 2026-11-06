using EasyQuerySharp.Commands;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/M8SaveTest.java（聚合保存：根、一对一、一对多、经中间表的多对多，按追踪快照的差异增删改，见 0004）。
/// Java 的 invoke(..) 在追踪作用域里执行并记录全部 SQL，这里是 <see cref="Invoke"/>。
/// </summary>
public sealed class M8SaveTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private async Task DeleteAll()
    {
        await Db.Delete<M8SaveRoot>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRoot2Many>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootMany>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootMiddleMany>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootOne>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootManyOne>().Where(o => o.Id != null).ExecuteAsync(Ct);
    }

    // 追踪作用域里执行，监听期间的全部 SQL
    private async Task Invoke(Func<SqlCapture.Scope, Task> action)
    {
        using var tracking = Db.BeginTracking();
        using var listen = SqlCapture.Listen();
        await action(listen);
    }

    private async Task Save<T>(params T[] roots) where T : class => await Save(s => s, roots);

    private async Task Save<T>(Func<SaveCommand<T>, SaveCommand<T>> configure, params T[] roots) where T : class
    {
        await using var tx = Db.BeginTransaction();
        await configure(Db.Save(roots)).ExecuteAsync(Ct);
        await tx.CommitAsync(Ct);
    }

    // 新建整棵聚合：多对多的目标先单独保存，再保存根；根、一对一、一对多、中间表、子对象的一对一依次插入
    private async Task<string> InsertOne()
    {
        using var tracking = Db.BeginTracking();
        var rootOne = new M8SaveRootOne();
        var many = new List<M8SaveRootMany> { new() { Name = "rootMany1" }, new() { Name = "rootMany2", M8SaveRootManyOne = new M8SaveRootManyOne { Name = "rootMany2One" } } };
        var many2many = new List<M8SaveRoot2Many> { new() { Name = "root2Many1" }, new() { Name = "root2Many2" } };
        var root = new M8SaveRoot { Name = "rootname", Code = "rootcode", M8SaveRootOne = rootOne, M8SaveRootManyList = many, M8SaveRoot2ManyList = many2many };

        using var listen = SqlCapture.Listen();
        await using (var tx = Db.BeginTransaction())
        {
            await Db.Save(many2many).ExecuteAsync(Ct);
            await Db.Save(root).ExecuteAsync(Ct);
            await tx.CommitAsync(Ct);
        }
        Assert.Equal(9, listen.Events.Count);
        listen.AssertSql(0, "INSERT INTO `m8_save_root_2many` (`id`,`name`) VALUES (?,?)");
        listen.AssertParams(0, $"{many2many[0].Id}(String),root2Many1(String)");
        listen.AssertSql(1, "INSERT INTO `m8_save_root_2many` (`id`,`name`) VALUES (?,?)");
        listen.AssertParams(1, $"{many2many[1].Id}(String),root2Many2(String)");
        listen.AssertSql(2, "INSERT INTO `m8_save_root` (`id`,`name`,`code`) VALUES (?,?,?)");
        listen.AssertParams(2, $"{root.Id}(String),rootname(String),rootcode(String)");
        listen.AssertSql(3, "INSERT INTO `m8_save_root_one` (`id`,`root_id`) VALUES (?,?)");
        listen.AssertParams(3, $"{rootOne.Id}(String),{root.Id}(String)");
        listen.AssertSql(4, "INSERT INTO `m8_save_root_many` (`id`,`root_id`,`name`) VALUES (?,?,?)");
        listen.AssertParams(4, $"{many[0].Id}(String),{root.Id}(String),rootMany1(String)");
        listen.AssertSql(5, "INSERT INTO `m8_save_root_many` (`id`,`root_id`,`name`) VALUES (?,?,?)");
        listen.AssertParams(5, $"{many[1].Id}(String),{root.Id}(String),rootMany2(String)");
        listen.AssertSql(6, "INSERT INTO `m8_save_root_middle_many` (`id`,`root_id`,`many_id`) VALUES (?,?,?)");
        Assert.EndsWith($"(String),{root.Id}(String),{many2many[0].Id}(String)", JavaParams.Format(listen.Events[6].Statement));
        listen.AssertSql(7, "INSERT INTO `m8_save_root_middle_many` (`id`,`root_id`,`many_id`) VALUES (?,?,?)");
        Assert.EndsWith($"(String),{root.Id}(String),{many2many[1].Id}(String)", JavaParams.Format(listen.Events[7].Statement));
        listen.AssertSql(8, "INSERT INTO `m8_save_root_many_one` (`id`,`root_many_id`,`name`) VALUES (?,?,?)");
        listen.AssertParams(8, $"{many[1].M8SaveRootManyOne!.Id}(String),{many[1].Id}(String),rootMany2One(String)");
        return root.Id!;
    }

    private async Task<string> Before()
    {
        await DeleteAll();
        return await InsertOne();
    }

    private Task<M8SaveRoot> RootWithOne(string rootId) => Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRootOne).SingleAsync(Ct);

    private static void AssertLoadedRootWithOne(SqlCapture.Scope listen, string rootId)
    {
        listen.AssertSql(0, "SELECT `id`,`name`,`code` FROM `m8_save_root` WHERE `id` = ?");
        listen.AssertParams(0, $"{rootId}(String)");
        listen.AssertSql(1, "SELECT t.`id`,t.`root_id`,t.`name` FROM `m8_save_root_one` t WHERE t.`root_id` IN (?)");
        listen.AssertParams(1, $"{rootId}(String)");
    }

    [Fact, Upstream("mysql8/M8SaveTest.java#testInsert")]
    public async Task TestInsert() => await Before();

    // 一对一置空：删除原来的子对象；再挂回去：插入
    [Fact, Upstream("mysql8/M8SaveTest.java#test2One1")]
    public async Task Test2One1()
    {
        var rootId = await Before();
        M8SaveRootOne? removed = null;
        await Invoke(async listen =>
        {
            var root = await RootWithOne(rootId);
            removed = root.M8SaveRootOne;
            root.M8SaveRootOne = null;
            await Save(root);
            Assert.Equal(3, listen.Events.Count);
            AssertLoadedRootWithOne(listen, rootId);
            listen.AssertSql(2, "DELETE FROM `m8_save_root_one` WHERE `id` = ?");
            listen.AssertParams(2, $"{removed!.Id}(String)");
        });
        await Invoke(async listen =>
        {
            var root = await RootWithOne(rootId);
            Assert.Null(root.M8SaveRootOne);
            root.M8SaveRootOne = removed;
            await Save(root);
            Assert.Equal(3, listen.Events.Count);
            AssertLoadedRootWithOne(listen, rootId);
            listen.AssertSql(2, "INSERT INTO `m8_save_root_one` (`id`,`root_id`) VALUES (?,?)");
            listen.AssertParams(2, $"{removed!.Id}(String),{rootId}(String)");
        });
    }

    // 没 Include 的导航不能当差异保存
    [Fact, Upstream("mysql8/M8SaveTest.java#test2One2", Deviation = "异常信息用中文，说明导航没有被追踪、该怎样改")]
    public async Task Test2One2()
    {
        var rootId = await Before();
        M8SaveRootOne? removed = null;
        await Invoke(async listen =>
        {
            var root = await RootWithOne(rootId);
            removed = root.M8SaveRootOne;
            root.M8SaveRootOne = null;
            await Save(root);
            Assert.Equal(3, listen.Events.Count);
            listen.AssertSql(2, "DELETE FROM `m8_save_root_one` WHERE `id` = ?");
        });
        await Invoke(async _ =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).SingleAsync(Ct);
            Assert.Null(root.M8SaveRootOne);
            root.M8SaveRootOne = removed;
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(root));
            Assert.Contains("M8SaveRoot.M8SaveRootOne 没有被追踪", ex.Message);
        });
    }
}
