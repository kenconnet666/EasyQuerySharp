using System.Linq.Expressions;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Live;

// JSON 列的就地修改（TS 的 JSON edits）：一条 UPDATE 里改嵌套路径、追加数组元素、删键、合并顶层键。
// C# 的写法按方言分：MySQL 系 Sql.MySql.Json(..).Edit（JSON_SET / JSON_ARRAY_APPEND / JSON_REMOVE），PostgreSQL Sql.Pg.Jsonb(..).Edit（jsonb_set / #- / ||）；
// 其余库没有这类写法，跳过（TS 的 jsonSome 按数组元素过滤 C# 没有对应的 API）
public abstract partial class LiveSuite
{
    /// <summary>JSON 文档。</summary>
    public sealed class PrefSettings
    {
        public string Theme { get; set; } = "";

        public PrefProfile? Profile { get; set; }

        public List<string> Tags { get; set; } = [];
    }

    /// <summary>嵌套对象。</summary>
    public sealed class PrefProfile
    {
        public string? Name { get; set; }

        public int? Level { get; set; }
    }

    /// <summary>MySQL 的 JSON 列。</summary>
    [Table("t_live_jpref")]
    public sealed class MySqlPref
    {
        [Column(PrimaryKey = true)]
        public int Id { get; set; }

        [Column(Converter = typeof(JsonValueConverter<PrefSettings>), DbType = "JSON")]
        public PrefSettings? Settings { get; set; }
    }

    /// <summary>PostgreSQL 的 jsonb 列。</summary>
    [Table("t_live_jpref")]
    public sealed class PgPref
    {
        [Column(PrimaryKey = true)]
        public int Id { get; set; }

        [Column(Converter = typeof(JsonValueConverter<PrefSettings>), DbType = "JSONB")]
        public PrefSettings? Settings { get; set; }
    }

    private static PrefSettings NewSettings() => new() { Theme = "light", Profile = new PrefProfile { Name = "a", Level = 1 }, Tags = ["x"] };

    private const string Quoted = "it's \"b\"";

    [Fact]
    public async Task EditsInsideAJsonColumnInOneUpdate()
    {
        var mySql = Dialect is MySqlDialect;
        SkipUnless(mySql || Dialect.GetType() == typeof(PostgreSqlDialect), "方言专属的 JSON 编辑（只有 MySQL 系与 PostgreSQL 有）");
        var db = await SeededAsync();
        var type = mySql ? typeof(MySqlPref) : typeof(PgPref);
        await db.CodeFirst.DropTablesIfExistsAsync([type], Ct);
        await db.CodeFirst.CreateTablesAsync([type], Ct);
        try
        {
            if (mySql) await MySqlEditsAsync(db);
            else await PgEditsAsync(db);
        }
        finally
        {
            await db.CodeFirst.DropTablesIfExistsAsync([type], Ct);
        }
    }

    private static async Task MySqlEditsAsync(EasyDb db)
    {
        await db.Insert(new MySqlPref { Id = 1, Settings = NewSettings() }).ExecuteAsync(Ct);
        async Task<PrefSettings> Settings() => (await db.Query<MySqlPref>().SingleAsync(Ct)).Settings!;
        // 编辑要在查询里直接写成 lambda，所以整个新值表达式作为参数传进来
        Task Edit(Expression<Func<MySqlPref, PrefSettings?>> edited) => db.Update<MySqlPref>().Set(p => p.Settings, edited).Where(p => p.Id == 1).ExecuteAsync(Ct);

        // 嵌套路径上换值：字符串、数字都按 JSON 写进去（不是带引号的文字）
        await Edit(p => Sql.MySql.Json(p.Settings).Edit(e => e.Set("$.profile.level", 2).Set("$.profile.name", Quoted)));
        var settings = await Settings();
        Assert.Equal((Quoted, 2), (settings.Profile!.Name, settings.Profile.Level));

        await Edit(p => Sql.MySql.Json(p.Settings).Edit(e => e.ArrayAppend("$.tags", "y")));
        Assert.Equal(["x", "y"], (await Settings()).Tags);

        await Edit(p => Sql.MySql.Json(p.Settings).Edit(e => e.Remove("$.profile.level")));
        Assert.Equal((Quoted, (int?)null), ((await Settings()).Profile!.Name, (await Settings()).Profile!.Level));

        // 顶层键整个换掉（MySQL 没有浅合并，逐个 Set）
        await Edit(p => Sql.MySql.Json(p.Settings).Edit(e => e.Set("$.theme", "dark").SetJson("$.tags", "[]").Remove("$.profile")));
        settings = await Settings();
        Assert.Equal(("dark", (PrefProfile?)null, 0), (settings.Theme, settings.Profile, settings.Tags.Count));
    }

    private static async Task PgEditsAsync(EasyDb db)
    {
        await db.Insert(new PgPref { Id = 1, Settings = NewSettings() }).ExecuteAsync(Ct);
        async Task<PrefSettings> Settings() => (await db.Query<PgPref>().SingleAsync(Ct)).Settings!;
        Task Edit(Expression<Func<PgPref, PrefSettings?>> edited) => db.Update<PgPref>().Set(p => p.Settings, edited).Where(p => p.Id == 1).ExecuteAsync(Ct);

        await Edit(p => Sql.Pg.Jsonb(p.Settings).Edit(e => e.Set("profile.level", "2").Set("profile.name", System.Text.Json.JsonSerializer.Serialize(Quoted))));
        var settings = await Settings();
        Assert.Equal((Quoted, 2), (settings.Profile!.Name, settings.Profile.Level));

        // 追加：写到下标 1（jsonb_set 的路径可以指向数组末尾的下一个位置）
        await Edit(p => Sql.Pg.Jsonb(p.Settings).Edit(e => e.Set("tags.1", "\"y\"")));
        Assert.Equal(["x", "y"], (await Settings()).Tags);

        await Edit(p => Sql.Pg.Jsonb(p.Settings).Edit(e => e.Remove("profile.level")));
        Assert.Equal((Quoted, (int?)null), ((await Settings()).Profile!.Name, (await Settings()).Profile!.Level));

        // 浅合并：给出的顶层键整个换掉
        await Edit(p => Sql.Pg.Jsonb(p.Settings).Edit(e => e.Merge("""{"theme":"dark","tags":[]}""")));
        settings = await Settings();
        Assert.Equal(("dark", Quoted, 0), (settings.Theme, settings.Profile!.Name, settings.Tags.Count));

        await Edit(p => Sql.Pg.Jsonb(p.Settings).Edit(e => e.Remove("profile")));
        Assert.Null((await Settings()).Profile);
    }
}
