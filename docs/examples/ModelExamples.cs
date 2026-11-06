using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"实体与映射"页：表与列的特性、命名约定、值转换、值对象、计算列。</summary>
public static class ModelExamples
{
    // <snippet:model-attributes>
    [Table("t_article", Schema = "blog", Comment = "文章", NotFoundMessage = "文章不存在")]
    public class Article
    {
        [Column(PrimaryKey = true, KeyGenerator = typeof(GuidKey))]
        public string? Id { get; set; }

        [Column("title_text", DbType = "VARCHAR(200)", Comment = "标题")]
        public string Title { get; set; } = "";

        public ArticleState State { get; set; }               // 枚举标了 [StoreAsName]：库里存 "Draft"

        [Column(AutoSelect = false)]
        public string? Body { get; set; }                     // 默认不查，显式选择才查

        [InsertIgnore]
        public DateTime? ReviewedAt { get; set; }             // 插入时不写

        [UpdateIgnore]
        public string CreatedBy { get; set; } = "";           // 按实体更新时不写

        [ColumnIgnore]
        public string? Preview { get; set; }                  // 不是列
    }

    [StoreAsName]
    public enum ArticleState { Draft, Published }

    /// <summary>插入时主键为 null 就生成一个。</summary>
    public sealed class GuidKey : IKeyGenerator
    {
        public object NewKey(ColumnMetadata column) => Guid.NewGuid().ToString("N");
    }
    // </snippet:model-attributes>

    // <snippet:model-converter>
    /// <summary>金额在库里存成"分"（整数）。</summary>
    public sealed class CentsConverter : ValueConverter<decimal, long>
    {
        protected override long ToProvider(decimal value) => (long)(value * 100);

        protected override decimal FromProvider(long value) => value / 100m;
    }

    [Table("t_wallet")]
    public class Wallet
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [Column(Converter = typeof(CentsConverter))]
        public decimal Balance { get; set; }

        [Column(Converter = typeof(JsonValueConverter<List<string>>))]
        public List<string> Labels { get; set; } = [];
    }
    // </snippet:model-converter>

    /// <summary>命名约定（不是卡片，只是保证指南里这段代码能编译）。</summary>
    public static EasyDb NamingConvention(System.Data.Common.DbDataSource dataSource)
    {
        // <snippet:model-naming>
        var db = new EasyDb(o =>
        {
            o.UseSqlServer(dataSource);
            o.NameConversion = NameConversion.UpperCamelCase;   // CreatedAt → [CreatedAt]，默认 Underlined → created_at
        });
        // </snippet:model-naming>
        return db;
    }

    [Example("model", "枚举按名字存：条件里的值也换成名字")]
    public static Task<List<string>> EnumByName(EasyDb db) =>
        db.Query<User>().Where(u => u.Level == UserLevel.Lead).OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();

    /// <summary>用户与技能（JSON 列读成列表）。</summary>
    public sealed record UserSkills(long UserId, List<string> Skills);

    [Example("model", "JSON 列：读出来就是对象")]
    public static Task<List<UserSkills>> JsonColumn(EasyDb db) =>
        db.Query<Profile>().Where(p => p.Website != null).OrderBy(p => p.UserId)
            .Select(p => new UserSkills(p.UserId, p.Skills)).ToListAsync();

    [Example("model", "值对象：插入时展开成几列")]
    public static Task<long> ValueObjectInsert(EasyDb db) =>
        db.Insert(new Shop
        {
            Name = "鼓楼店",
            Address = new ShopAddress { City = "南京", Street = "中山路 1 号", Geo = new GeoPoint { Lat = 32.06, Lng = 118.78 } },
        }).ExecuteAsync();

    [Example("model", "值对象：整个或只写其中一列")]
    public static async Task<long> ValueObjectUpdate(EasyDb db)
    {
        await db.Update<Shop>().Set(s => new Shop { Address = new ShopAddress { City = "杭州", Street = "江南大道 3 号" } }).Where(s => s.Id == 2).ExecuteAsync();
        return await db.Update<Shop>().Set(s => s.Address.Geo.Lat, 30.2).Where(s => s.Id == 2).ExecuteAsync();
    }

    // <snippet:model-computed>
    /// <summary>帖子：计算列"千次阅读"不在表里，由 views 算出。</summary>
    [Table("t_post")]
    public class PostStats
    {
        [Column(PrimaryKey = true)]
        public long Id { get; set; }

        public string Title { get; set; } = "";

        public int Views { get; set; }

        [Computed(nameof(KiloViewsSql))]
        public int KiloViews { get; set; }

        private static Expression<Func<PostStats, int>> KiloViewsSql => p => p.Views / 1000;
    }
    // </snippet:model-computed>

    [Example("model", "计算列：查询时就地展开，条件里也能用")]
    public static Task<List<PostStats>> ComputedColumn(EasyDb db) =>
        db.Query<PostStats>().Where(p => p.KiloViews >= 2).OrderBy(p => p.Id).ToListAsync();

    /// <summary>资料：简介默认不查。</summary>
    [Table("t_profile")]
    public class ProfileSummary
    {
        [Column(PrimaryKey = true)]
        public long UserId { get; set; }

        [Column(AutoSelect = false)]
        public string Bio { get; set; } = "";

        public string? Website { get; set; }
    }

    /// <summary>用户 id 与简介。</summary>
    public sealed record UserBio(long UserId, string Bio);

    [Example("model", "AutoSelect = false：查实体时不带这一列，显式选择才查")]
    public static async Task<List<UserBio>> NotAutoSelected(EasyDb db)
    {
        await db.Query<ProfileSummary>().Where(p => p.UserId <= 2).ToListAsync();
        return await db.Query<ProfileSummary>().Where(p => p.UserId <= 2).Select(p => new UserBio(p.UserId, p.Bio)).ToListAsync();
    }
}
