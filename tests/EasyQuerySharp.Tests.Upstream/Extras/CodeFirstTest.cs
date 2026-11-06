using EasyQuerySharp.Metadata;
using EasyQuerySharp.Schema;
using EasyQuerySharp.Tests.Upstream.DuckDb;
using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>
/// code-first 的建表与同步（Java 用例没有覆盖的部分）：各方言的注释、索引、外键与同步语句的写法，
/// 以及在 SQLite、DuckDB 上实际建表、读库结构、改名加列的同步。
/// </summary>
public sealed class CodeFirstTest(DuckDbFixture duck) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 共享内存库只要还有一个连接开着就一直在
    private const string SqliteCs = "Data Source=eqs-code-first;Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive = new(SqliteCs);
    private readonly EasyDb _sqlite = new(o => o.UseSqlite(SqliteFactory.Instance.CreateDataSource(SqliteCs)).AddListener(SqlCapture.Instance));

    public async ValueTask InitializeAsync() => await _keepAlive.OpenAsync(Ct);

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    [Table("cf_author", Comment = "作者")]
    public class CfAuthor
    {
        [Column(PrimaryKey = true)]
        public string? Id { get; set; }

        [Column(Comment = "名字")]
        public string? Name { get; set; }
    }

    [Table("cf_post")]
    [TableIndex(nameof(Title))]
    [TableIndex(nameof(AuthorId), nameof(Title), Name = "cf_post_author_title", Unique = true, Descending = [nameof(Title)])]
    public class CfPost
    {
        [Column(PrimaryKey = true, Generated = true)]
        public int Id { get; set; }

        public string? AuthorId { get; set; }

        public string? Title { get; set; }

        public int Views { get; set; }

        [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AuthorId)]), ForeignKey(Action = "ON DELETE CASCADE")]
        public CfAuthor? Author { get; set; }
    }

    /// <summary>cf_post 的下一版：表改名、title 改名成 headline、加 summary 列和一个索引。</summary>
    [Table("cf_article"), RenamedFrom("cf_post")]
    [TableIndex(nameof(Headline))]
    public class CfArticle
    {
        [Column(PrimaryKey = true, Generated = true)]
        public int Id { get; set; }

        public string? AuthorId { get; set; }

        [RenamedFrom("title")]
        public string? Headline { get; set; }

        public int Views { get; set; }

        [Column(Comment = "摘要")]
        public string? Summary { get; set; }

        [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(AuthorId)]), ForeignKey]
        public CfAuthor? Author { get; set; }
    }

    // 库里的 cf_post：没有外键，多一列 legacy
    private static readonly DbTableInfo[] PostTable =
    [
        new("cf_post", null,
        [
            new("id", "integer", false, null, null, true), new("author_id", "varchar(255)", true, null, null, false),
            new("title", "varchar(255)", true, null, null, false), new("views", "integer", false, null, null, false),
            new("legacy", "varchar(255)", true, null, null, false),
        ], ["id"], [new("cf_post_title_idx", ["title"], false)], []),
    ];

    [Fact]
    public void MySqlCreateTable() => Assert.Equal(
        "CREATE TABLE IF NOT EXISTS `cf_post` ( \n`id` INT(11) NOT NULL  AUTO_INCREMENT,\n`author_id` VARCHAR(255) NULL ,\n`title` VARCHAR(255) NULL ,\n`views` INT(11) NOT NULL , \n PRIMARY KEY (`id`)\n) Engine=InnoDB;\n"
        + "CREATE INDEX cf_post_title_idx ON `cf_post` (`title` ASC);\n"
        + "CREATE UNIQUE INDEX cf_post_author_title ON `cf_post` (`author_id` ASC,`title` DESC);\n"
        + "ALTER TABLE `cf_post` ADD CONSTRAINT cf_post_cf_author_id_fk FOREIGN KEY (`author_id`) REFERENCES `cf_author` (`id`) ON DELETE CASCADE ;",
        SqlOnly.Db("mysql").CodeFirst.CreateTableSql(typeof(CfPost)));

    // MySQL8Test2 的表：索引名（表名_列名_idx / _uidx）与写法与 Java 逐字一致（那个用例还差列默认值，留骨架）
    [Fact]
    public void MySqlIndexesMatchJava() => Assert.Equal(
        ["CREATE INDEX m8_test_index_column1_column2_idx ON `m8_test_index` (`column1` ASC,`column2` ASC);", "CREATE UNIQUE INDEX m8_test_index_column2_uidx ON `m8_test_index` (`column2` ASC);"],
        SqlOnly.Db("mysql").CodeFirst.CreateTableSql(typeof(M8TestIndex)).Split('\n')[^2..]);

    // 注释：MySQL 写在建表语句里，PostgreSQL 接在建表语句后面，Oracle、SQL Server 是单独的语句
    [Fact]
    public void Comments()
    {
        Assert.Equal("CREATE TABLE IF NOT EXISTS `cf_author` ( \n`id` VARCHAR(255) NOT NULL ,\n`name` VARCHAR(255) NULL  COMMENT '名字', \n PRIMARY KEY (`id`)\n) Engine=InnoDB COMMENT='作者';",
            SqlOnly.Db("mysql").CodeFirst.CreateTableSql(typeof(CfAuthor)));
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"cf_author\" ( \n\"id\" VARCHAR(255) NOT NULL ,\n\"name\" VARCHAR(255) NULL , \n PRIMARY KEY (\"id\")\n);\n\n"
                     + "COMMENT ON TABLE \"cf_author\" IS '作者';\nCOMMENT ON COLUMN \"cf_author\".\"name\" IS '名字';",
            SqlOnly.Db("pgsql").CodeFirst.CreateTableSql(typeof(CfAuthor)));
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"cf_author\" (\"id\" VARCHAR2(255) NOT NULL, \"name\" VARCHAR2(255) NULL, PRIMARY KEY (\"id\"))\n"
                     + "COMMENT ON TABLE \"cf_author\" IS '作者'\nCOMMENT ON COLUMN \"cf_author\".\"name\" IS '名字'",
            SqlOnly.Db("oracle").CodeFirst.CreateTableSql(typeof(CfAuthor)));
        Assert.Equal("IF OBJECT_ID(N'[cf_author]', N'U') IS NULL CREATE TABLE [cf_author] ([id] NVARCHAR(255) NOT NULL, [name] NVARCHAR(255) NULL, PRIMARY KEY ([id]))\n"
                     + "IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty('MS_Description', 'SCHEMA', N'dbo', 'TABLE', N'cf_author', NULL, NULL)) "
                     + "EXEC sp_addextendedproperty 'MS_Description', N'作者', 'SCHEMA', N'dbo', 'TABLE', N'cf_author'\n"
                     + "IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty('MS_Description', 'SCHEMA', N'dbo', 'TABLE', N'cf_author', 'COLUMN', N'name')) "
                     + "EXEC sp_addextendedproperty 'MS_Description', N'名字', 'SCHEMA', N'dbo', 'TABLE', N'cf_author', 'COLUMN', N'name'",
            SqlOnly.Db("mssql").CodeFirst.CreateTableSql(typeof(CfAuthor)));
        // SQLite 没有注释
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"cf_author\" ( \n\"id\" VARCHAR(255) NOT NULL ,\n\"name\" VARCHAR(255) NULL , \n PRIMARY KEY (\"id\")\n);",
            SqlOnly.Db("sqlite").CodeFirst.CreateTableSql(typeof(CfAuthor)));
    }

    // SQLite 不能给已有的表加外键：外键写在建表语句里；自增主键写成 INTEGER PRIMARY KEY AUTOINCREMENT
    [Fact]
    public void SqliteInlinesForeignKeys() => Assert.Equal(
        "CREATE TABLE IF NOT EXISTS \"cf_post\" ( \n\"id\" INTEGER PRIMARY KEY AUTOINCREMENT,\n\"author_id\" VARCHAR(255) NULL ,\n\"title\" VARCHAR(255) NULL ,\n\"views\" integer NOT NULL \n"
        + ", FOREIGN KEY (\"author_id\") REFERENCES \"cf_author\"(\"id\") ON DELETE CASCADE \n);\n"
        + "CREATE INDEX cf_post_title_idx ON \"cf_post\" (\"title\" ASC);\n"
        + "CREATE UNIQUE INDEX cf_post_author_title ON \"cf_post\" (\"author_id\" ASC,\"title\" DESC);",
        SqlOnly.Db("sqlite").CodeFirst.CreateTableSql(typeof(CfPost)));

    // 标准写法的方言：索引名、外键名加引号
    [Fact]
    public void StandardIndexAndForeignKey()
    {
        var oracle = SqlOnly.Db("oracle").CodeFirst.CreateTableSql(typeof(CfPost)).Split('\n');
        Assert.Equal(
        [
            "CREATE INDEX \"cf_post_title_idx\" ON \"cf_post\" (\"title\" ASC)",
            "CREATE UNIQUE INDEX \"cf_post_author_title\" ON \"cf_post\" (\"author_id\" ASC, \"title\" DESC)",
            "ALTER TABLE \"cf_post\" ADD CONSTRAINT \"cf_post_cf_author_id_fk\" FOREIGN KEY (\"author_id\") REFERENCES \"cf_author\" (\"id\") ON DELETE CASCADE",
        ], oracle[1..]);
        Assert.Equal("ALTER TABLE `cf_post` ADD INDEX `cf_post_title_idx` (`title`) TYPE minmax GRANULARITY 1",
            SqlOnly.Db("clickhouse").CodeFirst.CreateTableSql(typeof(CfAuthorlessPost)).Split('\n')[1]);
        Assert.Throws<NotSupportedException>(() => SqlOnly.Db("clickhouse").CodeFirst.CreateTableSql(typeof(CfPost)));
    }

    [Table("cf_post")]
    [TableIndex(nameof(Title))]
    public class CfAuthorlessPost
    {
        [Column(PrimaryKey = true)]
        public string? Id { get; set; }

        public string? Title { get; set; }
    }

    // 同步：表改名后按旧表比较，列改名、加列（带注释）、补索引与外键；库里多出来的列只列为差异
    [Theory]
    [InlineData("mysql",
        "ALTER TABLE `cf_post` RENAME TO `cf_article`;",
        "ALTER TABLE `cf_article` CHANGE `title` `headline` VARCHAR(255) NULL;",
        "ALTER TABLE `cf_article` ADD `summary` VARCHAR(255) NULL COMMENT '摘要';",
        "CREATE INDEX cf_article_headline_idx ON `cf_article` (`headline` ASC);",
        "ALTER TABLE `cf_article` ADD CONSTRAINT cf_article_cf_author_id_fk FOREIGN KEY (`author_id`) REFERENCES `cf_author` (`id`);")]
    [InlineData("pgsql",
        "ALTER TABLE \"cf_post\" RENAME TO \"cf_article\";",
        "ALTER TABLE \"cf_article\" RENAME COLUMN \"title\" TO \"headline\";",
        "ALTER TABLE \"cf_article\" ADD \"summary\" VARCHAR(255) NULL;\n COMMENT ON COLUMN \"cf_article\".\"summary\" IS '摘要';",
        "CREATE INDEX cf_article_headline_idx ON \"cf_article\" (\"headline\" ASC);",
        "ALTER TABLE \"cf_article\" ADD CONSTRAINT cf_article_cf_author_id_fk FOREIGN KEY (\"author_id\") REFERENCES \"cf_author\" (\"id\");")]
    [InlineData("mssql",
        "EXEC sp_rename N'[cf_post]', N'cf_article'",
        "EXEC sp_rename N'[cf_article].[title]', N'headline', 'COLUMN'",
        "ALTER TABLE [cf_article] ADD [summary] NVARCHAR(255) NULL",
        "IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty('MS_Description', 'SCHEMA', N'dbo', 'TABLE', N'cf_article', 'COLUMN', N'summary')) "
        + "EXEC sp_addextendedproperty 'MS_Description', N'摘要', 'SCHEMA', N'dbo', 'TABLE', N'cf_article', 'COLUMN', N'summary'",
        "CREATE INDEX [cf_article_headline_idx] ON [cf_article] ([headline] ASC)",
        "ALTER TABLE [cf_article] ADD CONSTRAINT [cf_article_cf_author_id_fk] FOREIGN KEY ([author_id]) REFERENCES [cf_author] ([id])")]
    [InlineData("oracle",
        "ALTER TABLE \"cf_post\" RENAME TO \"cf_article\"",
        "ALTER TABLE \"cf_article\" RENAME COLUMN \"title\" TO \"headline\"",
        "ALTER TABLE \"cf_article\" ADD (\"summary\" VARCHAR2(255) NULL)",
        "COMMENT ON COLUMN \"cf_article\".\"summary\" IS '摘要'",
        "CREATE INDEX \"cf_article_headline_idx\" ON \"cf_article\" (\"headline\" ASC)",
        "ALTER TABLE \"cf_article\" ADD CONSTRAINT \"cf_article_cf_author_id_fk\" FOREIGN KEY (\"author_id\") REFERENCES \"cf_author\" (\"id\")")]
    [InlineData("hana",
        "RENAME TABLE \"cf_post\" TO \"cf_article\"",
        "RENAME COLUMN \"cf_article\".\"title\" TO \"headline\"",
        "ALTER TABLE \"cf_article\" ADD (\"summary\" NVARCHAR(255) NULL)",
        "COMMENT ON COLUMN \"cf_article\".\"summary\" IS '摘要'",
        "CREATE INDEX \"cf_article_headline_idx\" ON \"cf_article\" (\"headline\" ASC)",
        "ALTER TABLE \"cf_article\" ADD CONSTRAINT \"cf_article_cf_author_id_fk\" FOREIGN KEY (\"author_id\") REFERENCES \"cf_author\" (\"id\")")]
    [InlineData("db2",
        "RENAME TABLE \"cf_post\" TO \"cf_article\"",
        "ALTER TABLE \"cf_article\" RENAME COLUMN \"title\" TO \"headline\"",
        "ALTER TABLE \"cf_article\" ADD COLUMN \"summary\" VARCHAR(255 CODEUNITS32) NULL",
        "COMMENT ON COLUMN \"cf_article\".\"summary\" IS '摘要'",
        "CREATE INDEX \"cf_article_headline_idx\" ON \"cf_article\" (\"headline\" ASC)",
        "ALTER TABLE \"cf_article\" ADD CONSTRAINT \"cf_article_cf_author_id_fk\" FOREIGN KEY (\"author_id\") REFERENCES \"cf_author\" (\"id\")")]
    public void PlanSync(string dialect, params string[] expected)
    {
        var sync = SqlOnly.Db(dialect).CodeFirst.PlanSync([typeof(CfArticle)], PostTable);
        Assert.Equal(expected, sync.Statements);
        Assert.Equal(["cf_article.legacy：库里有这一列，实体上没有"], sync.Differences);
    }

    // SQLite：同步语句一条一句；缺的外键加不上，列为差异
    [Fact]
    public void SqlitePlanSync()
    {
        var sync = SqlOnly.Db("sqlite").CodeFirst.PlanSync([typeof(CfArticle)], PostTable);
        Assert.Equal(
        [
            "ALTER TABLE \"cf_post\" RENAME TO \"cf_article\";",
            "ALTER TABLE \"cf_article\" RENAME COLUMN \"title\" TO \"headline\";",
            "ALTER TABLE \"cf_article\" ADD COLUMN \"summary\" VARCHAR(255) NULL;",
            "CREATE INDEX cf_article_headline_idx ON \"cf_article\" (\"headline\" ASC);",
        ], sync.Statements);
        Assert.Equal(["cf_article.legacy：库里有这一列，实体上没有", "cf_article：缺外键 cf_article_cf_author_id_fk，sqlite 不能给已有的表加外键"], sync.Differences);
    }

    // 表与库里一致时没有要执行的语句；可空性不一致只列为差异
    [Fact]
    public void NothingToSync()
    {
        DbTableInfo[] tables = [new("cf_author", "作者", [new("id", "varchar(255)", false, null, null, false), new("name", "varchar(255)", false, null, "名字", false)], ["id"], [], [])];
        var sync = SqlOnly.Db("mysql").CodeFirst.PlanSync([typeof(CfAuthor)], tables);
        Assert.Empty(sync.Statements);
        Assert.Equal("", sync.ToSql());
        Assert.Equal(["cf_author.name：库里是 NOT NULL，实体声明 NULL"], sync.Differences);
    }

    // SQLite 上实际建表、读结构、同步：数据跟着改名后的表与列走，再同步一次没有语句
    [Fact]
    public async Task SqliteCreateReadAndSync()
    {
        var db = _sqlite;
        await db.ExecuteSqlAsync("DROP TABLE IF EXISTS cf_article", cancellationToken: Ct);
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(CfPost), typeof(CfAuthor)], Ct);
        Assert.False(await db.CodeFirst.TableExistsAsync(typeof(CfPost), Ct));

        await db.CodeFirst.CreateTablesAsync([typeof(CfAuthor), typeof(CfPost)], Ct);
        Assert.True(await db.CodeFirst.TableExistsAsync(typeof(CfPost), Ct));
        var post = Assert.Single(await db.CodeFirst.ReadTablesAsync(Ct), t => t.Name == "cf_post");
        Assert.Equal(["id", "author_id", "title", "views"], post.Columns.Select(c => c.Name));
        Assert.Equal(["id"], post.PrimaryKey);
        Assert.True(post.FindColumn("ID")!.Generated);
        Assert.False(post.FindColumn("views")!.Nullable);
        Assert.Equal([("cf_post_author_title", true, "author_id,title"), ("cf_post_title_idx", false, "title")],
            post.Indexes.OrderBy(i => i.Name, StringComparer.Ordinal).Select(i => (i.Name, i.Unique, string.Join(",", i.Columns))));
        var fk = Assert.Single(post.ForeignKeys);
        Assert.Equal(["author_id"], fk.Columns);
        Assert.Equal("cf_author", fk.ReferencedTable);
        Assert.Equal(["id"], fk.ReferencedColumns);

        await db.Insert(new CfAuthor { Id = "a1", Name = "小明" }).ExecuteAsync(Ct);
        await db.Insert(new CfPost { AuthorId = "a1", Title = "hello", Views = 3 }).ExecuteAsync(Ct);

        // 外键是建表时写进去的（SQLite 的外键没有名字，按列比较），改名后仍在
        var sync = await db.CodeFirst.SyncTablesAsync([typeof(CfAuthor), typeof(CfArticle)], Ct);
        Assert.Equal(4, sync.Statements.Count);
        Assert.Empty(sync.Differences);
        var article = Assert.Single(await db.Query<CfArticle>().ToListAsync(Ct));
        Assert.Equal(("hello", 3, null), (article.Headline, article.Views, article.Summary));

        // 再同步一次：没有要执行的语句（旧表上的两个索引、改名留下的差异都不再出现）
        var again = await db.CodeFirst.PlanSyncAsync([typeof(CfAuthor), typeof(CfArticle)], Ct);
        Assert.Empty(again.Statements);
        Assert.Empty(again.Differences);
    }

    // DuckDB（进程内）：外键写在建表语句里（DuckDB 的外键不能带 ON DELETE 动作），读结构用 duckdb_* 系统函数
    [Fact]
    public async Task DuckDbCreateAndRead()
    {
        var db = duck.Db;
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(CfArticle), typeof(CfAuthor)], Ct);
        var create = await db.CodeFirst.SyncTablesAsync([typeof(CfAuthor), typeof(CfArticle)], Ct);
        Assert.Equal(3, create.Statements.Count);

        var tables = await db.CodeFirst.ReadTablesAsync(Ct);
        var author = Assert.Single(tables, t => t.Name == "cf_author");
        Assert.Equal("作者", author.Comment);
        Assert.Equal("名字", author.FindColumn("name")!.Comment);
        var article = Assert.Single(tables, t => t.Name == "cf_article");
        Assert.Equal(["id", "author_id", "headline", "views", "summary"], article.Columns.Select(c => c.Name));
        Assert.Equal(["id"], article.PrimaryKey);
        Assert.True(article.FindColumn("id")!.Generated);
        Assert.False(article.FindColumn("views")!.Nullable);
        var index = Assert.Single(article.Indexes);
        Assert.Equal(("cf_article_headline_idx", false), (index.Name, index.Unique));
        Assert.Equal(["headline"], index.Columns);
        var fk = Assert.Single(article.ForeignKeys);
        Assert.Equal(["author_id"], fk.Columns);
        Assert.Equal("cf_author", fk.ReferencedTable);
        Assert.Equal(["id"], fk.ReferencedColumns);

        Assert.Empty((await db.CodeFirst.PlanSyncAsync([typeof(CfAuthor), typeof(CfArticle)], Ct)).Statements);
        await db.CodeFirst.DropTablesIfExistsAsync([typeof(CfArticle), typeof(CfAuthor)], Ct);
    }

    [Fact]
    public async Task TDengineCannotReadTables() =>
        Assert.Contains("不支持读取库结构", (await Assert.ThrowsAsync<NotSupportedException>(() => SqlOnly.Db("tdengine").CodeFirst.ReadTablesAsync(Ct))).Message);

    // [ForeignKey] 只能用在对一导航上
    [Fact]
    public void ForeignKeyOnCollectionIsRejected() =>
        Assert.Contains("只能用在对一导航上", Assert.Throws<InvalidOperationException>(() => SqlOnly.Db("mysql").CodeFirst.CreateTableSql(typeof(CfBadAuthor))).Message);

    [Table("cf_bad_author")]
    public class CfBadAuthor
    {
        [Column(PrimaryKey = true)]
        public string? Id { get; set; }

        [Navigate(RelationType.OneToMany, TargetProperties = [nameof(CfPost.AuthorId)]), ForeignKey]
        public List<CfPost>? Posts { get; set; }
    }
}
