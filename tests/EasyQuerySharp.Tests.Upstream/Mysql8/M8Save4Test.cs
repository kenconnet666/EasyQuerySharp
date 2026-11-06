namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/M8Save4Test.java（两层一对多：整体换掉子集合，新对象沿用已有主键时按已有行处理）。</summary>
public sealed class M8Save4Test(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private async Task DeleteAll()
    {
        await Db.Delete<M8ToMany1>().Where(o => o.A != null).ExecuteAsync(Ct);
        await Db.Delete<M8ToMany2>().Where(o => o.D != null).ExecuteAsync(Ct);
        await Db.Delete<M8ToMany3>().Where(o => o.G != null).ExecuteAsync(Ct);
    }

    private async Task Invoke(Func<SqlCapture.Scope, Task> action)
    {
        using var tracking = Db.BeginTracking();
        using var listen = SqlCapture.Listen();
        await action(listen);
    }

    private async Task Save(M8ToMany1 root)
    {
        await using var tx = Db.BeginTransaction();
        await Db.Save(root).ExecuteAsync(Ct);
        await tx.CommitAsync(Ct);
    }

    private Task<M8ToMany1> Load() =>
        Db.Query<M8ToMany1>().Where(m => m.A == "a").Include(m => m.M8ToMany2List, m2 => m2.Include(x => x.M8ToMany3List)).SingleAsync(Ct);

    private static M8ToMany2 Two(string d, string suffix, params (string G, string H, string I)[] threes) => new()
    {
        D = d, A = "a", E = "e" + suffix, F = "f" + suffix,
        M8ToMany3List = [.. threes.Select(t => new M8ToMany3 { G = t.G, D = d, H = t.H, I = t.I })],
    };

    // 原来 d1、d2 两组，换成 d3 与沿用主键的 d2（子对象全换）：只要求能保存
    [Fact, Upstream("mysql8/M8Save4Test.java#testToManyToMany1")]
    public async Task TestToManyToMany1()
    {
        await DeleteAll();
        await Invoke(async _ => await Save(new M8ToMany1
        {
            A = "a", B = "b", C = "c",
            M8ToMany2List = [Two("d1", "1", ("g1", "h1", "i1"), ("g11", "h11", "i11")), Two("d2", "2", ("g2", "h2", "i2"), ("g22", "h22", "i22"))],
        }));
        await Invoke(async _ =>
        {
            var root = await Load();
            root.M8ToMany2List = [Two("d3", "3", ("g3", "h1", "i1"), ("g33", "h11", "i11")), Two("d2", "2", ("g4", "h2", "i2"), ("g44", "h22", "i22"))];
            await Save(root);
        });
        Assert.Equal(["d2", "d3"], (await Db.Query<M8ToMany2>().OrderBy(o => o.D).ToListAsync(Ct)).Select(o => o.D));
        Assert.Equal(["g3", "g33", "g4", "g44"], (await Db.Query<M8ToMany3>().OrderBy(o => o.G).ToListAsync(Ct)).Select(o => o.G));
    }

    // d2 换成 d2xx，g2 沿用主键挂到 d2xx 下：删 d2、插 d2xx，g2 只改外键
    [Fact, Upstream("mysql8/M8Save4Test.java#testToManyToMany2")]
    public async Task TestToManyToMany2()
    {
        await DeleteAll();
        await Invoke(async _ => await Save(new M8ToMany1 { A = "a", B = "b", C = "c", M8ToMany2List = [Two("d2", "2", ("g2", "h2", "i2"))] }));
        await Invoke(async listen =>
        {
            var root = await Load();
            root.M8ToMany2List = [Two("d2xx", "2", ("g2", "h2", "i2"))];
            await Save(root);
            Assert.Equal(6, listen.Events.Count);
            listen.AssertSql(0, "SELECT `a`,`b`,`c` FROM `m8_many1` WHERE `a` = ?");
            listen.AssertParams(0, "a(String)");
            listen.AssertSql(1, "SELECT t.`d`,t.`a`,t.`e`,t.`f` FROM `m8_many2` t WHERE t.`a` IN (?)");
            listen.AssertParams(1, "a(String)");
            listen.AssertSql(2, "SELECT t.`g`,t.`d`,t.`h`,t.`i` FROM `m8_many3` t WHERE t.`d` IN (?)");
            listen.AssertParams(2, "d2(String)");
            listen.AssertSql(3, "DELETE FROM `m8_many2` WHERE `d` = ?");
            listen.AssertParams(3, "d2(String)");
            listen.AssertSql(4, "INSERT INTO `m8_many2` (`d`,`a`,`e`,`f`) VALUES (?,?,?,?)");
            listen.AssertParams(4, "d2xx(String),a(String),e2(String),f2(String)");
            listen.AssertSql(5, "UPDATE `m8_many3` SET `d` = ? WHERE `g` = ?");
            listen.AssertParams(5, "d2xx(String),g2(String)");
        });
    }
}
