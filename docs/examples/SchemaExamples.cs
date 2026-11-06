using EasyQuerySharp.Metadata;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"建表与同步（code-first）"页的卡片：建表（注释、索引、外键）、只增不删的同步、读库结构。</summary>
public static class SchemaExamples
{
    // <snippet:schema-note>
    /// <summary>笔记：表注释、列注释、按作者与时间倒序的索引，作者列是引用 t_user 的外键。</summary>
    [Table("t_note", Comment = "笔记")]
    [TableIndex(nameof(AuthorId), nameof(CreatedAt), Descending = [nameof(CreatedAt)])]
    public class Note
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [Column(Comment = "标题")]
        public string Title { get; set; } = "";

        public long AuthorId { get; set; }

        public DateTime CreatedAt { get; set; }

        [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AuthorId)]), ForeignKey(Action = "ON DELETE CASCADE")]
        public User? Author { get; set; }
    }
    // </snippet:schema-note>

    // <snippet:schema-label>
    /// <summary>t_tag 的下一版：表改名成 t_label，name 列改名成 title，加 color 列和唯一索引。</summary>
    [Table("t_label"), RenamedFrom("t_tag")]
    [TableIndex(nameof(Title), Unique = true)]
    public class Label
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [Column(DbType = "VARCHAR(30)"), RenamedFrom("name")]
        public string Title { get; set; } = "";

        [Column(Comment = "颜色")]
        public string? Color { get; set; }
    }
    // </snippet:schema-label>

    // <snippet:schema-defaults>
    /// <summary>草稿：状态与优先级有库端默认值（DEFAULT 后面的 SQL 原样写进建表语句，字符串要自己带引号）。</summary>
    [Table("t_draft")]
    public class Draft
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        public string Title { get; set; } = "";

        [Column(DbDefault = "'draft'")]
        public string? Status { get; set; }

        [Column(DbDefault = "0")]
        public int? Priority { get; set; }
    }
    // </snippet:schema-defaults>

    [Example("schema", "建表：注释、索引、外键一起建（外键排在全部建表之后）")]
    public static async Task<long> CreateTables(EasyDb db)
    {
        await db.CodeFirst.CreateTablesAsync(typeof(Note));
        return await db.Query<Note>().CountAsync();
    }

    [Example("schema", "库端默认值：建表时写上 DEFAULT；插入时为 null 的列不写，由库填上")]
    public static async Task<Draft> DbDefaults(EasyDb db)
    {
        await db.CodeFirst.CreateTablesAsync(typeof(Draft));
        await db.Insert(new Draft { Title = "周报" }).ExecuteAsync();
        return await db.Query<Draft>().FirstAsync();
    }

    [Example("schema", "同步：先看要执行的语句，再执行；只增不删——改表名、改列名、加列、补索引")]
    public static async Task<string> SyncTables(EasyDb db)
    {
        var sync = await db.CodeFirst.PlanSyncAsync(typeof(Label));
        await sync.ExecuteAsync();
        return sync.ToSql();
    }

    [Example("schema", "读库结构：列、主键、索引、外键")]
    public static async Task<object> ReadTables(EasyDb db)
    {
        var tag = (await db.CodeFirst.ReadTablesAsync()).Single(t => t.Name == "t_tag");
        return new { tag.Name, Columns = tag.Columns.Select(c => $"{c.Name} {c.Type}{(c.Nullable ? "" : " NOT NULL")}").ToList(), tag.PrimaryKey };
    }
}
