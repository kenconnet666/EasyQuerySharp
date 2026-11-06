using EasyQuerySharp.Commands;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8Save2Test.java（聚合保存的归属变更：子对象换父对象、置空与空集合的开关）。</summary>
public sealed class M8Save2Test(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private async Task DeleteAll()
    {
        await Db.Delete<M8SaveRoot>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRoot2Many>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootMany>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootMiddleMany>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootOne>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootOne2>().Where(o => o.Id != null).ExecuteAsync(Ct);
        await Db.Delete<M8SaveRootManyOne>().Where(o => o.Id != null).ExecuteAsync(Ct);
    }

    // 根带一对一、第二个一对一与两个一对多子对象
    private async Task<string> InsertOne()
    {
        using var tracking = Db.BeginTracking();
        var root = new M8SaveRoot
        {
            Name = "rootname", Code = "rootcode", M8SaveRootOne = new M8SaveRootOne(), M8SaveRootOne2 = new M8SaveRootOne2(),
            M8SaveRootManyList = [new M8SaveRootMany { Name = "rootMany1" }, new M8SaveRootMany { Name = "rootMany2" }],
        };
        await Save(s => s, root);
        return root.Id!;
    }

    private async Task Invoke(Func<SqlCapture.Scope, Task> action)
    {
        using var tracking = Db.BeginTracking();
        using var listen = SqlCapture.Listen();
        await action(listen);
    }

    private async Task Save(Func<SaveCommand<M8SaveRoot>, SaveCommand<M8SaveRoot>> configure, params M8SaveRoot[] roots)
    {
        await using var tx = Db.BeginTransaction();
        await configure(Db.Save(roots)).ExecuteAsync(Ct);
        await tx.CommitAsync(Ct);
    }

    // 两个根各带两个子对象（主键给定）
    private async Task InsertTwoRoots() =>
        await Invoke(async _ => await Save(s => s,
            new M8SaveRoot { Id = "1", M8SaveRootManyList = [new() { Id = "2" }, new() { Id = "3" }] },
            new M8SaveRoot { Id = "4", M8SaveRootManyList = [new() { Id = "5" }, new() { Id = "6" }] }));

    private Task<List<M8SaveRoot>> LoadTwoRoots() =>
        Db.Query<M8SaveRoot>().WhereByIds(new[] { "1", "4" }).Include(m => m.M8SaveRootManyList).ToListAsync(Ct);

    [Fact, Upstream("mysql8/M8Save2Test.java#testInsert")]
    public async Task TestInsert()
    {
        await DeleteAll();
        await InsertOne();
    }

    // 子对象从 4 移到 1：默认不允许改归属
    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2Many1", Deviation = "异常信息用中文，说明哪个子对象的外键、当前值与应有的值")]
    public async Task TestOne2Many1()
    {
        await DeleteAll();
        await InsertTwoRoots();
        await Invoke(async _ =>
        {
            var list = await LoadTwoRoots();
            var root1 = list.Single(o => o.Id == "1");
            var root4 = list.Single(o => o.Id == "4");
            var id5 = root4.M8SaveRootManyList![0];
            root4.M8SaveRootManyList.RemoveAt(0);
            root1.M8SaveRootManyList!.Add(id5);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(s => s, [.. list]));
            Assert.Contains("M8SaveRootMany.RootId 的值 4", ex.Message);
            Assert.Contains("应为 1", ex.Message);
        });
    }

    // 允许改归属：只改外键
    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2Many2")]
    public async Task TestOne2Many2()
    {
        await DeleteAll();
        await InsertTwoRoots();
        await Invoke(async listen =>
        {
            var list = await LoadTwoRoots();
            var root1 = list.Single(o => o.Id == "1");
            var root4 = list.Single(o => o.Id == "4");
            var id5 = root4.M8SaveRootManyList![0];
            root4.M8SaveRootManyList.RemoveAt(0);
            root1.M8SaveRootManyList!.Add(id5);
            await Save(s => s.With(SaveBehavior.AllowOwnershipChange), [.. list]);
            Assert.Equal(3, listen.Events.Count);
            listen.AssertSql(0, "SELECT `id`,`name`,`code` FROM `m8_save_root` WHERE `id` IN (?,?)");
            listen.AssertParams(0, "1(String),4(String)");
            listen.AssertSql(1, "SELECT t.`id`,t.`root_id`,t.`name` FROM `m8_save_root_many` t WHERE t.`root_id` IN (?,?)");
            listen.AssertSql(2, "UPDATE `m8_save_root_many` SET `root_id` = ? WHERE `id` = ?");
            listen.AssertParams(2, "1(String),5(String)");
        });
    }

    // 同一子对象既挂到 1 又留在 4：冲突
    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2Many3", Deviation = "异常信息用中文，说明同一个子对象只能属于一个父对象")]
    public async Task TestOne2Many3()
    {
        await DeleteAll();
        await InsertTwoRoots();
        await Invoke(async _ =>
        {
            var list = await LoadTwoRoots();
            var root1 = list.Single(o => o.Id == "1");
            var root4 = list.Single(o => o.Id == "4");
            root1.M8SaveRootManyList!.Add(root4.M8SaveRootManyList![0]);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(s => s.With(SaveBehavior.AllowOwnershipChange), [.. list]));
            Assert.Contains("只能属于一个父对象", ex.Message);
        });
    }

    // 集合置为 null：原来的子对象全部删除
    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2Many4")]
    public async Task TestOne2Many4()
    {
        await DeleteAll();
        await InsertOne();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().Include(m => m.M8SaveRootManyList).FirstAsync(Ct);
            root.M8SaveRootManyList = null;
            await Save(s => s, root);
            Assert.Equal(4, listen.Events.Count);
            listen.AssertSql(2, "DELETE FROM `m8_save_root_many` WHERE `id` = ?");
            listen.AssertSql(3, "DELETE FROM `m8_save_root_many` WHERE `id` = ?");
        });
    }

    // IgnoreNull：置为 null 的导航跳过
    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2Many5")]
    public async Task TestOne2Many5()
    {
        await DeleteAll();
        await InsertOne();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().Include(m => m.M8SaveRootManyList).FirstAsync(Ct);
            root.M8SaveRootManyList = null;
            await Save(s => s.With(SaveBehavior.IgnoreNull), root);
            Assert.Equal(2, listen.Events.Count);
        });
    }

    // IgnoreEmpty：空集合跳过
    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2Many6")]
    public async Task TestOne2Many6()
    {
        await DeleteAll();
        await InsertOne();
        await Invoke(async listen =>
        {
            var root = await Db.Query<M8SaveRoot>().Include(m => m.M8SaveRootManyList).FirstAsync(Ct);
            root.M8SaveRootManyList = [];
            await Save(s => s.With(SaveBehavior.IgnoreEmpty), root);
            Assert.Equal(2, listen.Events.Count);
        });
    }

    // 两个根交换一对一子对象：允许改归属时各改一次外键
    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2OneChange")]
    public async Task TestOne2OneChange()
    {
        await DeleteAll();
        var rootId1 = await InsertOne();
        var rootId2 = await InsertOne();
        await Invoke(async listen =>
        {
            var list = await Db.Query<M8SaveRoot>().Include(m => m.M8SaveRootOne).ToListAsync(Ct);
            var (first, second) = (list[0], list[1]);
            var (firstSub, secondSub) = (first.M8SaveRootOne!, second.M8SaveRootOne!);
            first.M8SaveRootOne = secondSub;
            second.M8SaveRootOne = firstSub;
            await Save(s => s.With(SaveBehavior.AllowOwnershipChange), [.. list]);
            Assert.Equal(4, listen.Events.Count);
            listen.AssertSql(0, "SELECT `id`,`name`,`code` FROM `m8_save_root`");
            listen.AssertSql(1, "SELECT t.`id`,t.`root_id`,t.`name` FROM `m8_save_root_one` t WHERE t.`root_id` IN (?,?)");
            Assert.Contains(rootId1, JavaParams.Format(listen.Events[1].Statement));
            Assert.Contains(rootId2, JavaParams.Format(listen.Events[1].Statement));
            listen.AssertSql(2, "UPDATE `m8_save_root_one` SET `root_id` = ? WHERE `id` = ?");
            listen.AssertParams(2, $"{first.Id}(String),{secondSub.Id}(String)");
            listen.AssertSql(3, "UPDATE `m8_save_root_one` SET `root_id` = ? WHERE `id` = ?");
            listen.AssertParams(3, $"{second.Id}(String),{firstSub.Id}(String)");
        });
    }

    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2OneChange2")]
    public async Task TestOne2OneChange2()
    {
        await DeleteAll();
        await InsertOne();
        await InsertOne();
        await Invoke(async listen =>
        {
            var list = await Db.Query<M8SaveRoot>().Include(m => m.M8SaveRootOne2).ToListAsync(Ct);
            var (first, second) = (list[0], list[1]);
            var (firstSub, secondSub) = (first.M8SaveRootOne2!, second.M8SaveRootOne2!);
            first.M8SaveRootOne2 = secondSub;
            second.M8SaveRootOne2 = firstSub;
            await Save(s => s.With(SaveBehavior.AllowOwnershipChange), [.. list]);
            Assert.Equal(4, listen.Events.Count);
            listen.AssertSql(1, "SELECT t.`id`,t.`root_id`,t.`name` FROM `m8_save_root_one2` t WHERE t.`root_id` IN (?,?)");
            listen.AssertSql(2, "UPDATE `m8_save_root_one2` SET `root_id` = ? WHERE `id` = ?");
            listen.AssertParams(2, $"{first.Id}(String),{secondSub.Id}(String)");
            listen.AssertSql(3, "UPDATE `m8_save_root_one2` SET `root_id` = ? WHERE `id` = ?");
            listen.AssertParams(3, $"{second.Id}(String),{firstSub.Id}(String)");
        });
    }

    // 不允许改归属时交换一对一子对象：报错
    [Fact, Upstream("mysql8/M8Save2Test.java#testOne2OneChange3", Deviation = "异常信息用中文，说明哪个子对象的外键已指向别的父对象")]
    public async Task TestOne2OneChange3()
    {
        await DeleteAll();
        await InsertOne();
        await InsertOne();
        await Invoke(async _ =>
        {
            var list = await Db.Query<M8SaveRoot>().Include(m => m.M8SaveRootOne2).ToListAsync(Ct);
            var (first, second) = (list[0], list[1]);
            (first.M8SaveRootOne2, second.M8SaveRootOne2) = (second.M8SaveRootOne2, first.M8SaveRootOne2);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(s => s, [.. list]));
            Assert.Contains("AllowOwnershipChange", ex.Message);
        });
    }
}
