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

    // LoadIncludeAsync 加载的导航同样算追踪（不写别名）
    [Fact, Upstream("mysql8/M8SaveTest.java#test2One3")]
    public async Task Test2One3()
    {
        var rootId = await Before();
        M8SaveRootOne? removed = null;
        async Task<M8SaveRoot> Load()
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).SingleAsync(Ct);
            await Db.LoadIncludeAsync([root], m => m.M8SaveRootOne, Ct);
            return root;
        }
        void AssertLoaded(SqlCapture.Scope listen)
        {
            listen.AssertSql(0, "SELECT `id`,`name`,`code` FROM `m8_save_root` WHERE `id` = ?");
            listen.AssertSql(1, "SELECT `id`,`root_id`,`name` FROM `m8_save_root_one` WHERE `root_id` IN (?)");
            listen.AssertParams(1, $"{rootId}(String)");
        }
        await Invoke(async listen =>
        {
            var root = await Load();
            removed = root.M8SaveRootOne;
            root.M8SaveRootOne = null;
            await Save(root);
            Assert.Equal(3, listen.Events.Count);
            AssertLoaded(listen);
            listen.AssertSql(2, "DELETE FROM `m8_save_root_one` WHERE `id` = ?");
            listen.AssertParams(2, $"{removed!.Id}(String)");
        });
        await Invoke(async listen =>
        {
            var root = await Load();
            Assert.Null(root.M8SaveRootOne);
            root.M8SaveRootOne = removed;
            await Save(root);
            Assert.Equal(3, listen.Events.Count);
            AssertLoaded(listen);
            listen.AssertSql(2, "INSERT INTO `m8_save_root_one` (`id`,`root_id`) VALUES (?,?)");
            listen.AssertParams(2, $"{removed!.Id}(String),{rootId}(String)");
        });
    }

    // 只保存一个根，另一个查出来的根不受影响
    [Fact, Upstream("mysql8/M8SaveTest.java#test2One4")]
    public async Task Test2One4()
    {
        await DeleteAll();
        var rootId = await InsertOne();
        var rootId2 = await InsertOne();
        await Invoke(async listen =>
        {
            var root = await RootWithOne(rootId);
            var removed = root.M8SaveRootOne;
            root.M8SaveRootOne = null;
            await Db.Query<M8SaveRoot>().WhereById(rootId2).SingleAsync(Ct);
            await Save(root);
            Assert.Equal(4, listen.Events.Count);
            AssertLoadedRootWithOne(listen, rootId);
            listen.AssertSql(2, "SELECT `id`,`name`,`code` FROM `m8_save_root` WHERE `id` = ?");
            listen.AssertParams(2, $"{rootId2}(String)");
            listen.AssertSql(3, "DELETE FROM `m8_save_root_one` WHERE `id` = ?");
            listen.AssertParams(3, $"{removed!.Id}(String)");
        });
    }

    // 删除根：已加载的子对象从深到浅先删（中间表行、子对象的一对一），最后删根
    [Fact, Upstream("mysql8/M8SaveTest.java#testDeleteAll", Deviation = "Java 的 include2 按路径写多段带出；C# 写成嵌套的 Include")]
    public async Task TestDeleteAll()
    {
        var rootId = await Before();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId)
                .Include(t => t.M8SaveRootOne).Include(t => t.M8SaveRootOne2).Include(t => t.M8SaveRoot2ManyList)
                .Include(t => t.M8SaveRootManyList, m => m.Include(x => x.M8SaveRootManyOne))
                .SingleAsync(Ct);
            await Save(s => s.RemoveRoot(), root);
            Assert.Equal(14, listen.Events.Count);
            listen.AssertSql(7, "DELETE FROM `m8_save_root_many_one` WHERE `id` = ?");
            listen.AssertSql(8, "DELETE FROM `m8_save_root_one` WHERE `id` = ?");
            listen.AssertSql(9, "DELETE FROM `m8_save_root_middle_many` WHERE `root_id` = ? AND `many_id` = ?");
            listen.AssertSql(10, "DELETE FROM `m8_save_root_middle_many` WHERE `root_id` = ? AND `many_id` = ?");
            listen.AssertSql(11, "DELETE FROM `m8_save_root_many` WHERE `id` = ?");
            listen.AssertSql(12, "DELETE FROM `m8_save_root_many` WHERE `id` = ?");
            listen.AssertSql(13, "DELETE FROM `m8_save_root` WHERE `id` = ?");
        });
    }

    // 一对多：保留一个、新加一个、拿掉一个，根也改了名
    [Fact, Upstream("mysql8/M8SaveTest.java#testOne2Many1")]
    public async Task TestOne2Many1()
    {
        var rootId = await Before();
        await Invoke(async _ =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRootManyList).SingleAsync(Ct);
            root.Name = "namenew2";
            root.M8SaveRootManyList = [root.M8SaveRootManyList![0], new M8SaveRootMany { Name = "newOneToMany2" }];
            await Save(root);
        });
    }

    [Fact, Upstream("mysql8/M8SaveTest.java#testOne2Many2")]
    public async Task TestOne2Many2()
    {
        var rootId = await Before();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRootManyList).SingleAsync(Ct);
            var list = root.M8SaveRootManyList!;
            var remove = list[1];
            list.RemoveAt(1);
            var added = new M8SaveRootMany { Name = "newOneToMany2" };
            list.Add(added);
            await Save(root);
            Assert.Equal(4, listen.Events.Count);
            listen.AssertSql(1, "SELECT t.`id`,t.`root_id`,t.`name` FROM `m8_save_root_many` t WHERE t.`root_id` IN (?)");
            listen.AssertSql(2, "DELETE FROM `m8_save_root_many` WHERE `id` = ?");
            listen.AssertParams(2, $"{remove.Id}(String)");
            listen.AssertSql(3, "INSERT INTO `m8_save_root_many` (`id`,`root_id`,`name`) VALUES (?,?,?)");
            listen.AssertParams(3, $"{added.Id}(String),{rootId}(String),newOneToMany2(String)");
        });
    }

    // 新建的同主键对象替换已追踪的：按值合并、没有变化不更新；拿掉的删除，新加的插入
    [Fact, Upstream("mysql8/M8SaveTest.java#testOne2Many2_1")]
    public async Task TestOne2Many2_1()
    {
        var rootId = await Before();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRootManyList).SingleAsync(Ct);
            var list = root.M8SaveRootManyList!;
            var remove = list[1];
            var added = new M8SaveRootMany { Name = "newOneToMany2" };
            root.M8SaveRootManyList = [new M8SaveRootMany { Id = list[0].Id, Name = list[0].Name, RootId = list[0].RootId }, added];
            await Save(root);
            Assert.Equal(4, listen.Events.Count);
            listen.AssertSql(2, "DELETE FROM `m8_save_root_many` WHERE `id` = ?");
            listen.AssertParams(2, $"{remove.Id}(String)");
            listen.AssertSql(3, "INSERT INTO `m8_save_root_many` (`id`,`root_id`,`name`) VALUES (?,?,?)");
            listen.AssertParams(3, $"{added.Id}(String),{rootId}(String),newOneToMany2(String)");
        });
    }

    // 两层：拿掉没有子对象的那一个，新加一个带一对一子对象的
    [Fact, Upstream("mysql8/M8SaveTest.java#testOne2Many3")]
    public async Task TestOne2Many3()
    {
        var rootId = await Before();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRootManyList, rq => rq.Include(s => s.M8SaveRootManyOne)).SingleAsync(Ct);
            var list = root.M8SaveRootManyList!;
            var remove = list.First(o => o.M8SaveRootManyOne is null);
            list.RemoveAll(o => o.M8SaveRootManyOne is null);
            var added = new M8SaveRootMany { Name = "newOneToMany2", M8SaveRootManyOne = new M8SaveRootManyOne { Name = "m8SaveRootManyOne2" } };
            list.Add(added);
            await Save(root);
            Assert.Equal(6, listen.Events.Count);
            listen.AssertSql(2, "SELECT t.`id`,t.`root_many_id`,t.`name` FROM `m8_save_root_many_one` t WHERE t.`root_many_id` IN (?,?)");
            listen.AssertSql(3, "DELETE FROM `m8_save_root_many` WHERE `id` = ?");
            listen.AssertParams(3, $"{remove.Id}(String)");
            listen.AssertSql(4, "INSERT INTO `m8_save_root_many` (`id`,`root_id`,`name`) VALUES (?,?,?)");
            listen.AssertParams(4, $"{added.Id}(String),{rootId}(String),newOneToMany2(String)");
            listen.AssertSql(5, "INSERT INTO `m8_save_root_many_one` (`id`,`root_many_id`,`name`) VALUES (?,?,?)");
            listen.AssertParams(5, $"{added.M8SaveRootManyOne.Id}(String),{added.Id}(String),m8SaveRootManyOne2(String)");
        });
    }

    // 新加的子对象连同它的一对一一起插入
    [Fact, Upstream("mysql8/M8SaveTest.java#testOne2Many4")]
    public async Task TestOne2Many4()
    {
        var rootId = await Before();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRootManyList).SingleAsync(Ct);
            var added = new M8SaveRootMany { Name = "newOneToMany2", M8SaveRootManyOne = new M8SaveRootManyOne { Name = "m8SaveRootManyOne2" } };
            root.M8SaveRootManyList!.Add(added);
            await Save(root);
            Assert.Equal(4, listen.Events.Count);
            listen.AssertSql(2, "INSERT INTO `m8_save_root_many` (`id`,`root_id`,`name`) VALUES (?,?,?)");
            listen.AssertParams(2, $"{added.Id}(String),{rootId}(String),newOneToMany2(String)");
            listen.AssertSql(3, "INSERT INTO `m8_save_root_many_one` (`id`,`root_many_id`,`name`) VALUES (?,?,?)");
            listen.AssertParams(3, $"{added.M8SaveRootManyOne.Id}(String),{added.Id}(String),m8SaveRootManyOne2(String)");
        });
    }

    // 已追踪的子对象上没加载过的导航被赋了值：报错
    [Fact, Upstream("mysql8/M8SaveTest.java#testOne2Many5", Deviation = "异常信息用中文，说明导航没有被追踪、该怎样改")]
    public async Task TestOne2Many5()
    {
        var rootId = await Before();
        await Invoke(async _ =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRootManyList).SingleAsync(Ct);
            root.M8SaveRootManyList![1].M8SaveRootManyOne = new M8SaveRootManyOne { Name = "m8SaveRootManyOne2" };
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(root));
            Assert.Contains("M8SaveRootMany.M8SaveRootManyOne 没有被追踪", ex.Message);
        });
    }

    // SavePath 列出的导航没加载过也照常保存（当作原来没有子对象）
    [Fact, Upstream("mysql8/M8SaveTest.java#testOne2Many6")]
    public async Task TestOne2Many6()
    {
        var rootId = await Before();
        await Invoke(async _ =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRootManyList).SingleAsync(Ct);
            root.M8SaveRootManyList![1].M8SaveRootManyOne = new M8SaveRootManyOne { Name = "m8SaveRootManyOne2" };
            await Save(s => s.SavePath(o => o.M8SaveRootManyList!.Select(m => m.M8SaveRootManyOne)), root);
        });
    }

    // 多对多：清空时删中间表行（目标不动），再加回来时插中间表行
    [Fact, Upstream("mysql8/M8SaveTest.java#testMany2Many1")]
    public async Task TestMany2Many1()
    {
        var rootId = await Before();
        var targets = new List<M8SaveRoot2Many>();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRoot2ManyList).SingleAsync(Ct);
            targets.AddRange(root.M8SaveRoot2ManyList!);
            root.M8SaveRoot2ManyList!.Clear();
            await Save(root);
            Assert.Equal(5, listen.Events.Count);
            listen.AssertSql(1, "SELECT `root_id`,`many_id` FROM `m8_save_root_middle_many` WHERE `root_id` IN (?)");
            listen.AssertSql(2, "SELECT t.`id`,t.`name` FROM `m8_save_root_2many` t WHERE t.`id` IN (?,?)");
            listen.AssertSql(3, "DELETE FROM `m8_save_root_middle_many` WHERE `root_id` = ? AND `many_id` = ?");
            listen.AssertParams(3, $"{rootId}(String),{targets[0].Id}(String)");
            listen.AssertSql(4, "DELETE FROM `m8_save_root_middle_many` WHERE `root_id` = ? AND `many_id` = ?");
            listen.AssertParams(4, $"{rootId}(String),{targets[1].Id}(String)");
        });
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().WhereById(rootId).Include(m => m.M8SaveRoot2ManyList).SingleAsync(Ct);
            root.M8SaveRoot2ManyList!.AddRange(targets);
            await Save(root);
            Assert.Equal(4, listen.Events.Count);
            listen.AssertSql(2, "INSERT INTO `m8_save_root_middle_many` (`id`,`root_id`,`many_id`) VALUES (?,?,?)");
            Assert.EndsWith($"(String),{rootId}(String),{targets[0].Id}(String)", JavaParams.Format(listen.Events[2].Statement));
            listen.AssertSql(3, "INSERT INTO `m8_save_root_middle_many` (`id`,`root_id`,`many_id`) VALUES (?,?,?)");
            Assert.EndsWith($"(String),{rootId}(String),{targets[1].Id}(String)", JavaParams.Format(listen.Events[3].Statement));
        });
    }
}
