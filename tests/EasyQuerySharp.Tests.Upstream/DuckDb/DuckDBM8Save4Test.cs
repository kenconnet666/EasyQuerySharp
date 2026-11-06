using EasyQuerySharp.Tests.Upstream.Mysql8;

namespace EasyQuerySharp.Tests.Upstream.DuckDb;

/// <summary>对应 Java：duckdb/M8Save4Test.java（两层一对多：整体换掉子集合，新对象沿用已有主键时按已有行处理；与 mysql8 的同名用例是同一个场景）。</summary>
public sealed class DuckDBM8Save4Test(DuckDbFixture fixture) : DuckDbTest(fixture)
{
    private async Task DeleteAll()
    {
        await Db.Delete<M8ToMany1>().Where(o => o.A != null).ExecuteAsync(Ct);
        await Db.Delete<M8ToMany2>().Where(o => o.D != null).ExecuteAsync(Ct);
        await Db.Delete<M8ToMany3>().Where(o => o.G != null).ExecuteAsync(Ct);
    }

    private async Task Save(M8ToMany1 root)
    {
        await using var tx = Db.BeginTransaction();
        await Db.Save(root).ExecuteAsync(Ct);
        await tx.CommitAsync(Ct);
    }

    private static M8ToMany2 Two(string d, string suffix, params (string G, string H, string I)[] threes) => new()
    {
        D = d, A = "a", E = "e" + suffix, F = "f" + suffix,
        M8ToMany3List = [.. threes.Select(t => new M8ToMany3 { G = t.G, D = d, H = t.H, I = t.I })],
    };

    // 原来 d1、d2 两组，换成 d3 与沿用主键的 d2（子对象全换）
    [Fact, Upstream("duckdb/M8Save4Test.java#testToManyToMany1")]
    public async Task TestToManyToMany1()
    {
        await DeleteAll();
        using (Db.BeginTracking())
        {
            await Save(new M8ToMany1
            {
                A = "a", B = "b", C = "c",
                M8ToMany2List = [Two("d1", "1", ("g1", "h1", "i1"), ("g11", "h11", "i11")), Two("d2", "2", ("g2", "h2", "i2"), ("g22", "h22", "i22"))],
            });
        }
        using (Db.BeginTracking())
        {
            var root = await Db.Query<M8ToMany1>().Where(m => m.A == "a").Include(m => m.M8ToMany2List, m2 => m2.Include(x => x.M8ToMany3List)).SingleAsync(Ct);
            root.M8ToMany2List = [Two("d3", "3", ("g3", "h1", "i1"), ("g33", "h11", "i11")), Two("d2", "2", ("g4", "h2", "i2"), ("g44", "h22", "i22"))];
            await Save(root);
        }
        Assert.Equal(["d2", "d3"], (await Db.Query<M8ToMany2>().OrderBy(o => o.D).ToListAsync(Ct)).Select(o => o.D));
        Assert.Equal(["g3", "g33", "g4", "g44"], (await Db.Query<M8ToMany3>().OrderBy(o => o.G).ToListAsync(Ct)).Select(o => o.G));
    }
}
