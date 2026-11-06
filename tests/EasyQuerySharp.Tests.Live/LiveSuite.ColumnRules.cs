using EasyQuerySharp.Encryption;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Live;

// 列规则（TS 的 column rules）：逻辑删除、库端与客户端默认值、更新时填值、按名字存的枚举、乐观锁、只在插入时写的列、列加密
public abstract partial class LiveSuite
{
    /// <summary>用到全部列规则的文档，用例里单独建表。</summary>
    [Table("t_live_doc")]
    public sealed class RuleDoc
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [Column(DbType = "VARCHAR(100)")]
        public string Title { get; set; } = "";

        /// <summary>没给时由库填 draft。</summary>
        [Column(Converter = typeof(EnumNameConverter<DocStatus>), DbType = "VARCHAR(20)", DbDefault = "'draft'")]
        public DocStatus? Status { get; set; }

        /// <summary>客户端默认值（TS 的 <c>$default</c>）：C# 里就是属性的初值。</summary>
        [Column(DbType = "VARCHAR(20)")]
        public string Token { get; set; } = "tok";

        [LogicDelete(LogicDeleteStrategy.Boolean)]
        public bool Deleted { get; set; }

        [Version]
        public int Version { get; set; }

        [UpdateIgnore]
        [Column(DbType = "VARCHAR(50)")]
        public string CreatedBy { get; set; } = "";

        [Encryption(typeof(Rot13Encryption))]
        [Column(DbType = "VARCHAR(100)")]
        public string Secret { get; set; } = "";

        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>文档状态，按名字存。</summary>
    public enum DocStatus
    {
        [System.Runtime.Serialization.EnumMember(Value = "draft")] Draft,
        [System.Runtime.Serialization.EnumMember(Value = "published")] Published,
    }

    /// <summary>ROT13：库里看得出是密文、又能逐字核对的可逆"加密"。</summary>
    public sealed class Rot13Encryption : IEncryptionStrategy
    {
        public object? Encrypt(Type entityType, string propertyName, object? plaintext) => plaintext is string s ? Rot13(s) : plaintext;

        public object? Decrypt(Type entityType, string propertyName, object? ciphertext) => ciphertext is string s ? Rot13(s) : ciphertext;

        private static string Rot13(string text) => string.Concat(text.Select(c => c switch
        {
            >= 'a' and <= 'z' => (char)('a' + (c - 'a' + 13) % 26),
            >= 'A' and <= 'Z' => (char)('A' + (c - 'A' + 13) % 26),
            _ => c,
        }));
    }

    /// <summary>按条件更新时填更新时间（用户自己写了就不覆盖）。</summary>
    private sealed class TouchDoc : EntityInterceptor<RuleDoc>
    {
        public static readonly DateTime Now = new(2026, 5, 6, 7, 8, 9);

        public override void BeforeUpdate(RuleDoc entity) => entity.UpdatedAt = Now;

        public override void BeforeConditionUpdate(UpdateSetBuilder<RuleDoc> sets) => sets.Set(d => d.UpdatedAt, Now);
    }

    private async Task<EasyDb> RuleDocsAsync(params string[] titles)
    {
        var db = await SeededAsync(o => o.AddInterceptor(new TouchDoc()));
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(RuleDoc)], Ct);
        await db.CodeFirst.CreateTablesAsync([typeof(RuleDoc)], Ct);
        if (titles.Length > 0) await db.Insert(titles.Select(NewDoc)).ExecuteAsync(Ct);
        return db;
    }

    private static RuleDoc NewDoc(string title) => new() { Title = title, CreatedBy = "ann", Secret = "hello" };

    // 删除改写成 UPDATE，查询看不到已删除的行；再删、再更新都碰不到它；Hard() 才物理删除
    [Fact]
    public async Task TurnsDeleteIntoAnUpdateAndHidesSoftDeletedRows()
    {
        var db = await RuleDocsAsync("a", "b", "c");
        Assert.Equal(1, await db.Delete<RuleDoc>().Where(d => d.Id == 1).IgnoreVersion().ExecuteAsync(Ct));
        Assert.Equal(2, await db.Query<RuleDoc>().CountAsync(Ct));
        var all = await db.Query<RuleDoc>().DisableLogicDelete().OrderBy(d => d.Id).ToListAsync(Ct);
        Assert.Equal([("a", true), ("b", false), ("c", false)], all.Select(d => (d.Title, d.Deleted)));

        Assert.Equal(0, await db.Delete<RuleDoc>().Where(d => d.Id == 1).IgnoreVersion().ExecuteAsync(Ct));
        Assert.Equal(0, await db.Update<RuleDoc>().Set(d => d.Title, "x").Where(d => d.Id == 1).IgnoreVersion().ExecuteAsync(Ct));
        Assert.Equal(1, await db.Delete<RuleDoc>().Where(d => d.Id == 1).IgnoreVersion().Hard().ExecuteAsync(Ct));
        Assert.Equal(2, await db.Query<RuleDoc>().DisableLogicDelete().CountAsync(Ct));
    }

    // 插入：库端默认值（status）、属性初值（token）、逻辑删除列写"未删除"；按条件更新由拦截器填更新时间，自己写了就用自己的
    [Fact]
    public async Task FillsDefaultsOnInsertAndUpdateTimeValuesOnUpdate()
    {
        var db = await RuleDocsAsync();
        await db.Insert(NewDoc("a"), new RuleDoc { Title = "b", CreatedBy = "ann", Secret = "hello", Token = "mine" }).ExecuteAsync(Ct);
        var rows = await db.Query<RuleDoc>().OrderBy(d => d.Id).ToListAsync(Ct);
        Assert.Equal([("tok", DocStatus.Draft, false, 0, (DateTime?)null), ("mine", DocStatus.Draft, false, 0, null)],
            rows.Select(r => (r.Token, r.Status, r.Deleted, r.Version, r.UpdatedAt)));

        await db.Update<RuleDoc>().Set(d => d.Title, "a2").Where(d => d.Id == 1).IgnoreVersion().ExecuteAsync(Ct);
        Assert.Equal(TouchDoc.Now, (await db.Query<RuleDoc>().FindAsync(1L, Ct))!.UpdatedAt);
        var given = new DateTime(2030, 1, 1);
        await db.Update<RuleDoc>().Set(d => d.Title, "b2").Set(d => d.UpdatedAt, given).Where(d => d.Id == 2).IgnoreVersion().ExecuteAsync(Ct);
        Assert.Equal(given, (await db.Query<RuleDoc>().FindAsync(2L, Ct))!.UpdatedAt);
    }

    // 按名字存的枚举：写入、按枚举过滤；库里被写进不认识的名字时读出报错，而不是悄悄当成合法值
    [Fact]
    public async Task StoresEnumsByNameAndRejectsUnknownNamesOnRead()
    {
        var db = await RuleDocsAsync();
        await db.Insert(new RuleDoc { Title = "a", CreatedBy = "ann", Secret = "s", Status = DocStatus.Published }).ExecuteAsync(Ct);
        Assert.Equal(DocStatus.Published, (await db.Query<RuleDoc>().SingleAsync(Ct)).Status);
        Assert.Equal(1, await db.Query<RuleDoc>().Where(d => d.Status == DocStatus.Published).CountAsync(Ct));
        Assert.Equal(["published"], await db.SqlQueryAsync<string>($"SELECT {Sql.Identifier("status")} FROM {Sql.Identifier("t_live_doc")}", Ct));

        await db.ExecuteSqlAsync($"UPDATE {Sql.Identifier("t_live_doc")} SET {Sql.Identifier("status")} = {"weird"}", Ct);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => db.Query<RuleDoc>().ToListAsync(Ct));
        Assert.Contains("weird", error.ToString());
    }

    // 按实体更新带旧版本做条件并加一；拿着旧版本再更新影响 0 行，ExecuteAsync(1, ..) 抛 ConcurrencyException；
    // 按条件更新带 WithVersion 同样加一，别人手里的旧版本随之失效
    [Fact]
    public async Task GuardsUpdatesByVersionAndBumpsItOnEveryUpdate()
    {
        var db = await RuleDocsAsync("a");
        var stale = await db.Query<RuleDoc>().AsNoTracking().SingleAsync(Ct);
        Assert.Equal(0, stale.Version);
        var first = await db.Query<RuleDoc>().AsNoTracking().SingleAsync(Ct);
        first.Title = "first";
        Assert.Equal(1, await db.Update(first).ExecuteAsync(1, "已被别人修改", Ct));
        Assert.Equal(1, first.Version);
        Assert.Equal(1, (await db.Query<RuleDoc>().SingleAsync(Ct)).Version);

        stale.Title = "second";
        var error = await Assert.ThrowsAsync<ConcurrencyException>(() => db.Update(stale).ExecuteAsync(1, "已被别人修改", Ct));
        Assert.Equal((1, 0), (error.ExpectedRows, error.ActualRows));
        Assert.Equal("first", (await db.Query<RuleDoc>().SingleAsync(Ct)).Title);

        await db.Update<RuleDoc>().Set(d => d.Title, "third").Where(d => d.Id == 1).WithVersion(1).ExecuteAsync(Ct);
        var third = await db.Query<RuleDoc>().SingleAsync(Ct);
        Assert.Equal(("third", 2), (third.Title, third.Version));
        Assert.Throws<InvalidOperationException>(() => db.Update<RuleDoc>().Set(d => d.Title, "x").Where(d => d.Id == 1).ToSql());
    }

    // 批量的按实体更新：每行带自己的版本条件，加密列照样加密；还拿着旧版本时两行都改不到
    [Fact]
    public async Task MergesVersionedUpdatesAndStillEncodesEncryptedColumns()
    {
        var db = await RuleDocsAsync("a", "b");
        var docs = await db.Query<RuleDoc>().AsNoTracking().OrderBy(d => d.Id).ToListAsync(Ct);
        var edited = docs.Select(d => new RuleDoc
        {
            Id = d.Id, Title = d.Title + "!", Status = d.Status, Token = d.Token, Version = d.Version, CreatedBy = d.CreatedBy, Secret = "new " + d.Title,
        }).ToList();
        Assert.Equal(2, await db.Update(edited).Batch().ExecuteAsync(2, "已被别人修改", Ct));
        var after = await db.Query<RuleDoc>().OrderBy(d => d.Id).ToListAsync(Ct);
        Assert.Equal([("a!", "new a", 1), ("b!", "new b", 1)], after.Select(d => (d.Title, d.Secret, d.Version)));

        var error = await Assert.ThrowsAsync<ConcurrencyException>(() => db.Update(docs).Batch().ExecuteAsync(2, "已被别人修改", Ct));
        Assert.Equal(0, error.ActualRows);
    }

    // IgnoreVersion：按条件更新不换版本，按实体更新不带版本条件；按实体删除带版本条件，版本不对删不到（逻辑删除顺带换版本）
    [Fact]
    public async Task IgnoresTheVersionWhenAskedAndGuardsDeletesByVersion()
    {
        var db = await RuleDocsAsync("a", "b");
        var docs = await db.Query<RuleDoc>().AsNoTracking().OrderBy(d => d.Id).ToListAsync(Ct);
        var (a, b) = (docs[0], docs[1]);
        await db.Update<RuleDoc>().Set(d => d.Title, "a1").Where(d => d.Id == a.Id).IgnoreVersion().ExecuteAsync(Ct);
        a.Title = "a2";
        a.Version = 41;
        Assert.Equal(1, await db.Update(a).IgnoreVersion().ExecuteAsync(Ct));
        var row = (await db.Query<RuleDoc>().FindAsync(a.Id, Ct))!;
        Assert.Equal("a2", row.Title);

        var wrong = new RuleDoc { Id = b.Id, Version = 7 };
        await Assert.ThrowsAsync<ConcurrencyException>(() => db.Delete(wrong).ExecuteAsync(1, "已被别人修改", Ct));
        Assert.Equal(1, await db.Delete(b).ExecuteAsync(1, "已被别人修改", Ct));
        var removed = (await db.Query<RuleDoc>().DisableLogicDelete().FindAsync(b.Id, Ct))!;
        Assert.Equal((true, 1), (removed.Deleted, removed.Version));
    }

    // [UpdateIgnore]：按实体更新不写这一列（与 Java 一样，显式 Set 时照写）
    [Fact]
    public async Task NeverUpdatesIgnoredColumnsOfAnEntity()
    {
        var db = await RuleDocsAsync("a");
        var row = await db.Query<RuleDoc>().AsNoTracking().SingleAsync(Ct);
        row.Title = "a2";
        row.CreatedBy = "bob";
        await db.Update(row).ExecuteAsync(Ct);
        var saved = await db.Query<RuleDoc>().SingleAsync(Ct);
        Assert.Equal(("a2", "ann"), (saved.Title, saved.CreatedBy));
        Assert.DoesNotContain("created_by", db.Update(row).ToSql());
    }

    // 库里存密文，读出解密，等值条件的值整体加密后与密文比较
    [Fact]
    public async Task EncryptsTransparentlyIncludingInEqualityConditions()
    {
        var db = await RuleDocsAsync("a");
        Assert.Equal(["uryyb"], await db.SqlQueryAsync<string>($"SELECT {Sql.Identifier("secret")} FROM {Sql.Identifier("t_live_doc")}", Ct));
        Assert.Equal("hello", (await db.Query<RuleDoc>().SingleAsync(Ct)).Secret);
        Assert.Equal(1, await db.Query<RuleDoc>().Where(d => d.Secret == "hello").CountAsync(Ct));
    }
}
