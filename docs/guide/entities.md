# 实体与映射

实体是普通的 C# 类，用特性声明它对应的表、列和关系。元数据在第一次用到时反射读取，按 `EasyDb` 缓存。本页讲表与列、命名约定、主键生成、枚举与值转换、值对象、计算列；关系（`[Navigate]`）见[联表与导航](joins-and-navigations.md)，逻辑删除、乐观锁、加密等列规则见[列规则](rules.md)。

## 表与列

<!-- snippet: model-attributes -->
```csharp
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
```
<!-- /snippet -->

| 特性 | 参数 | 说明 |
|---|---|---|
| `[Table("表名")]` | `Schema`、`Comment`、`IgnoreProperties`、`NotFoundMessage` | 不写时表名按命名约定由类名推出。`Schema` 渲染成 `schema.table`（MySQL 里是库名）；`IgnoreProperties` 列出基类里这张表没有的属性；`NotFoundMessage` 是 `FirstAsync` / `SingleAsync` / `FindOrThrowAsync` / `EnsureAnyAsync` 查不到时 `NotFoundException` 的默认消息 |
| `[Column("列名")]` | `PrimaryKey` | 主键，可以多个（复合主键） |
| | `Generated` | 自增列：插入时不写这一列，`FillGeneratedKeys()` 回填 |
| | `GeneratedSql` | 插入时这一列写成这段 SQL，如 `"nextval('seq_user')"` |
| | `KeyGenerator` | 实现 `IKeyGenerator` 的类型：插入时主键为 null 就生成一个（雪花 id、GUID 字符串等） |
| | `Converter` | 值转换器，见下文 |
| | `SqlConversion` | 列在 SQL 里的读写转换（如库端加解密），继承 `ColumnSqlConversion` |
| | `AutoSelect` | 默认 `true`；`false` 时查实体不带这一列，显式选择才查（大字段） |
| | `DbType`、`Comment`、`DbDefault` | 建表时用的类型、注释和库端默认值（SQL 原文），见[建表与同步](code-first.md) |
| `[ColumnIgnore]` | | 不是列（DTO 里也用它排除属性） |
| `[InsertIgnore]` | | 插入时不写 |
| `[UpdateIgnore]` | `WhenTrackedChanged` | 按实体更新时不写；`WhenTrackedChanged = true` 时追踪到它改了仍然写 |

公开属性里，标量类型（数字、字符串、日期时间、枚举、`Guid` 等）自动是列；其它类型（如 JSON 列的 `List<string>`）要标 `[Column]`，通常带 `Converter`。导航、值对象分别标 `[Navigate]`、`[ValueObject]`。

`AutoSelect = false` 的列：查实体时不出现在 SELECT 里，投影里显式写才查：

<!-- card: model/NotAutoSelected sql=mysql -->
```csharp
await db.Query<ProfileSummary>().Where(p => p.UserId <= 2).ToListAsync();
return await db.Query<ProfileSummary>().Where(p => p.UserId <= 2).Select(p => new UserBio(p.UserId, p.Bio)).ToListAsync();
```

```sql
-- MySQL
SELECT `user_id`,`website` FROM `t_profile` WHERE `user_id` <= ?;
-- params: [2]
SELECT t.`user_id`,t.`bio` FROM `t_profile` t WHERE t.`user_id` <= ?;
-- params: [2]
```
<!-- /card -->

## 命名约定

`EasyDbOptions.NameConversion` 决定没写名字的表和列怎么命名：

| 约定 | `CreatedAt` → | 对应 Java |
|---|---|---|
| `NameConversion.Underlined`（默认） | `created_at` | `UnderlinedNameConversion` |
| `NameConversion.UpperUnderlined` | `CREATED_AT` | `UpperUnderlinedNameConversion` |
| `NameConversion.LowerCamelCase` | `createdAt` | `LowerCamelCaseNameConversion` |
| `NameConversion.UpperCamelCase` | `CreatedAt` | `UpperCamelCaseNameConversion` |
| `NameConversion.AsIs` | `CreatedAt` | `DefaultNameConversion` |

<!-- snippet: model-naming -->
```csharp
var db = new EasyDb(o =>
{
    o.UseSqlServer(dataSource);
    o.NameConversion = NameConversion.UpperCamelCase;   // CreatedAt → [CreatedAt]，默认 Underlined → created_at
});
```
<!-- /snippet -->

自定义约定继承 `NameConversion`，实现 `Convert(string)`。`[Table("…")]`、`[Column("…")]` 显式写的名字不经过约定。

## 枚举与值转换

- 枚举默认按整数存。
- 按名字存：枚举类型标 `[StoreAsName]`（所有实体都按名字存），或在单个属性上写 `[Column(Converter = typeof(EnumNameConverter<T>))]`。名字取枚举成员上的 `[EnumMember(Value = "…")]`，没有时取成员名。
- 条件里的枚举值同样经过转换器：

<!-- card: model/EnumByName sql=mysql -->
```csharp
db.Query<User>().Where(u => u.Level == UserLevel.Lead).OrderBy(u => u.Id).Select(u => u.Name).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name` FROM `t_user` t WHERE t.`level` = ? ORDER BY t.`id` ASC;
-- params: ['lead']
```
<!-- /card -->

自定义转换器继承 `ValueConverter<TModel, TProvider>`；JSON 列直接用 `JsonValueConverter<T>`（`System.Text.Json`），读出来就是对象：

<!-- snippet: model-converter -->
```csharp
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
```
<!-- /snippet -->

<!-- card: model/JsonColumn sql=mysql -->
```csharp
db.Query<Profile>().Where(p => p.Website != null).OrderBy(p => p.UserId)
    .Select(p => new UserSkills(p.UserId, p.Skills)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`user_id`,t.`skills` FROM `t_profile` t WHERE t.`website` IS NOT NULL ORDER BY t.`user_id` ASC;
```
<!-- /card -->

转换器作用在读取结果、写入的值、条件里与这一列比较的值上。JSON 内部的查询（取某个键、长度）见[函数](functions.md#json)。

## 值对象

几列合成实体上的一个嵌套对象（对应 TS 版的 `embed()`）。属性标 `[ValueObject]`，值对象类型的属性就是列，列名默认是"属性名_子属性名"，`Prefix` 换前缀，可以嵌套：

<!-- snippet: sample-shop -->
```csharp
/// <summary>门店：地址是值对象，在库里展开成几列（address_city、address_street、geo_lat、geo_lng）。</summary>
[Table("t_shop")]
public class Shop
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(50)")]
    public string Name { get; set; } = "";

    [ValueObject]
    public ShopAddress Address { get; set; } = new();
}

/// <summary>门店地址（值对象），里面再嵌一个坐标。</summary>
public class ShopAddress
{
    [Column(DbType = "VARCHAR(20)")]
    public string City { get; set; } = "";

    [Column(DbType = "VARCHAR(100)")]
    public string? Street { get; set; }

    [ValueObject(Prefix = "geo_")]
    public GeoPoint Geo { get; set; } = new();
}

/// <summary>坐标。</summary>
public class GeoPoint
{
    public double Lat { get; set; }

    public double Lng { get; set; }
}
```
<!-- /snippet -->

条件里 `s.Address.City` 就是一列；整个值对象可以投影、按结构映射到 DTO：

<!-- card: query/basics/ValueObjects sql=mysql -->
```csharp
db.Query<Shop>().Where(s => s.Address.City == "杭州" && s.Address.Street != null)
    .Select(s => new ShopPoint(s.Name, s.Address.Geo)).ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t.`geo_lat`,t.`geo_lng` FROM `t_shop` t WHERE t.`address_city` = ? AND t.`address_street` IS NOT NULL;
-- params: ['杭州']
```
<!-- /card -->

插入时展开成几列，更新时可以整个写，也可以只写其中一列：

<!-- card: model/ValueObjectInsert sql=mysql -->
```csharp
db.Insert(new Shop
{
    Name = "鼓楼店",
    Address = new ShopAddress { City = "南京", Street = "中山路 1 号", Geo = new GeoPoint { Lat = 32.06, Lng = 118.78 } },
}).ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_shop` (`name`,`address_city`,`address_street`,`geo_lat`,`geo_lng`) VALUES (?,?,?,?,?);
-- params: ['鼓楼店', '南京', '中山路 1 号', 32.06, 118.78]
```
<!-- /card -->

<!-- card: model/ValueObjectUpdate sql=mysql -->
```csharp
await db.Update<Shop>().Set(s => new Shop { Address = new ShopAddress { City = "杭州", Street = "江南大道 3 号" } }).Where(s => s.Id == 2).ExecuteAsync();
return await db.Update<Shop>().Set(s => s.Address.Geo.Lat, 30.2).Where(s => s.Id == 2).ExecuteAsync();
```

```sql
-- MySQL
UPDATE `t_shop` SET `address_city` = ?,`address_street` = ? WHERE `id` = ?;
-- params: ['杭州', '江南大道 3 号', 2]
UPDATE `t_shop` SET `geo_lat` = ? WHERE `id` = ?;
-- params: [30.2, 2]
```
<!-- /card -->

`s.Address == null` 表示值对象的列全为 NULL。

## 计算列

表里没有这一列，值由同一实体的其它列算出（对应 Java 的 `ColumnValueSQLConverter` 且 `isRealColumn = false`、TS 的 `defineComputed`）。`[Computed]` 指向实体上的一个静态表达式：

<!-- snippet: model-computed -->
```csharp
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
```
<!-- /snippet -->

查询时就地展开：SELECT 里写成 `表达式 AS 列名`，条件、排序里写成表达式本身；插入、更新不写这一列。

<!-- card: model/ComputedColumn sql=mysql -->
```csharp
db.Query<PostStats>().Where(p => p.KiloViews >= 2).OrderBy(p => p.Id).ToListAsync();
```

```sql
-- MySQL
SELECT `id`,`title`,`views`,(`views` DIV ?) AS `kilo_views` FROM `t_post` WHERE (`views` DIV ?) >= ? ORDER BY `id` ASC;
-- params: [1000, 1000, 2]
```
<!-- /card -->

## 关系

导航属性标 `[Navigate(关系类型, …)]`，键不写时取主键。示例模型的用户：

<!-- snippet: sample-user -->
```csharp
/// <summary>用户：方圆的年龄为空，顾言未成年且没有公司；<see cref="ManagerId"/> 指向上级。</summary>
[Table("t_user")]
public class User
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(50)")]
    public string Name { get; set; } = "";

    public int? Age { get; set; }

    public long? CompanyId { get; set; }

    public long? DepartmentId { get; set; }

    public long? ManagerId { get; set; }

    [Column(Converter = typeof(EnumNameConverter<UserLevel>), DbType = "VARCHAR(10)")]
    public UserLevel Level { get; set; }

    public DateOnly JoinedAt { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])]
    public Company? Company { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(DepartmentId)])]
    public Department? Department { get; set; }

    /// <summary>一对一：资料表的主键就是用户 id。</summary>
    [Navigate(RelationType.OneToOne, TargetProperties = [nameof(Samples.Profile.UserId)])]
    public Profile? Profile { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ManagerId)])]
    public User? Manager { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(ManagerId)])]
    public List<User>? Reports { get; set; }

    /// <summary>关注我的人：经关注表的多对多。</summary>
    [Navigate(RelationType.ManyToMany, Mapping = typeof(Follow), SelfMappingProperties = [nameof(Follow.FolloweeId)], TargetMappingProperties = [nameof(Follow.FollowerId)])]
    public List<User>? Followers { get; set; }

    /// <summary>我关注的人。</summary>
    [Navigate(RelationType.ManyToMany, Mapping = typeof(Follow), SelfMappingProperties = [nameof(Follow.FollowerId)], TargetMappingProperties = [nameof(Follow.FolloweeId)])]
    public List<User>? Following { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.AuthorId)])]
    public List<Post>? Posts { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Comment.AuthorId)])]
    public List<Comment>? Comments { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Order.UserId)])]
    public List<Order>? Orders { get; set; }

    /// <summary>不是主键的关联：文档记的是创建人的名字。</summary>
    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Name)], TargetProperties = [nameof(Doc.CreatedBy)])]
    public List<Doc>? Docs { get; set; }

    /// <summary>固定条件的一对多：只算阅读量过千的帖子。</summary>
    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.AuthorId)], ExtraFilter = nameof(HotPostFilter))]
    public List<Post>? HotPosts { get; set; }

    private static Expression<Func<Post, bool>> HotPostFilter => p => p.Views >= 1000;
}
```
<!-- /snippet -->

| 关系 | 需要写的键 | 属性类型 |
|---|---|---|
| `RelationType.ManyToOne` | `SelfProperties`：本表的外键（`TargetProperties` 不写取目标主键） | 实体 |
| `RelationType.OneToOne` | 两端不写时都取主键；常见是 `TargetProperties` 指向目标表里引用本表的列 | 实体 |
| `RelationType.OneToMany` | `TargetProperties`：目标表的外键（`SelfProperties` 不写取本表主键） | `List<T>` |
| `RelationType.ManyToMany` | `Mapping`（中间表实体）、`SelfMappingProperties`、`TargetMappingProperties` | `List<T>` |

其余参数（`Required`、`ExtraFilter`、`OrderBy` / `Limit`、`DirectMapping`、`GroupJoin`、`KeyMatch`、`Cascade`）的用法见[联表与导航](joins-and-navigations.md)、[带出与对象树](include-and-trees.md)、[追踪与聚合保存](save-and-tracking.md)。

## 限制

- 元数据靠反射读取；裁剪 / Native AOT 下由核心包自带的生成器保留实体的成员，见[裁剪与 Native AOT](native-aot.md)。
- 没有 Fluent API：映射只能写在特性上。不想改实体时，逻辑删除可以用 `options.ApplyLogicDelete(..)` 配置（见[列规则](rules.md)），其余映射暂不支持外部配置。
- 不支持多态实体（TS 版的 `entity.union`），记在 BACKLOG。
- `DbType`、`Comment`、`DbDefault` 只在建表时使用，见[建表与同步](code-first.md)。
- 值对象的类型要是类，不能是结构体或标量。

## 对应 Java

| Java | C# |
|---|---|
| `@Table(value, schema, comment)` | `[Table("…", Schema = …, Comment = …)]` |
| `@Column(value, primaryKey, generatedKey)` | `[Column("…", PrimaryKey = true, Generated = true)]` |
| `@Column(exist = false)` / `@ColumnIgnore` | `[ColumnIgnore]` |
| `@InsertIgnore` / `@UpdateIgnore(updateSetInTrackDiff)` | `[InsertIgnore]` / `[UpdateIgnore(WhenTrackedChanged = true)]` |
| `@Column(autoSelect = false)` | `[Column(AutoSelect = false)]` |
| `@Column(primaryKeyGenerator = …)`、`PrimaryKeyGenerator` | `[Column(KeyGenerator = typeof(…))]`、`IKeyGenerator` |
| `generatedSQLColumnGenerator` | `[Column(GeneratedSql = "…")]` |
| `@Column(conversion = …)`、`ValueConverter` | `[Column(Converter = typeof(…))]`、`ValueConverter<TModel, TProvider>` |
| `@Column(sqlConversion = …)`、`ColumnValueSQLConverter` | `[Column(SqlConversion = typeof(…))]`、`ColumnSqlConversion`；不是真实列时用 `[Computed]` |
| `@Enumerated` / 枚举转换器 | `[StoreAsName]` / `EnumNameConverter<T>` |
| `@EasyAssertMessage` | `[Table(NotFoundMessage = "…")]` |
| `NameConversion` 配置项 | `EasyDbOptions.NameConversion` |
| `@ValueObject`（已弃用） | `[ValueObject]` |
| `@Navigate` | `[Navigate]` |
