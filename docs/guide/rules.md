# 列规则：逻辑删除、乐观锁、拦截器、加密

声明在实体或全局配置上、对每条语句自动生效的规则。示例模型里的文档（`Doc`）用到逻辑删除、乐观锁、只在插入时写的列，工单（`Ticket`）用到多租户过滤、写入前填值、列加密：

<!-- snippet: sample-doc -->
```csharp
/// <summary>用到列规则的文档：逻辑删除、乐观锁、只在插入时写的创建人、更新时由拦截器填的时间。</summary>
[Table("t_doc")]
public class Doc
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Column(DbType = "VARCHAR(100)")]
    public string Title { get; set; } = "";

    [LogicDelete(LogicDeleteStrategy.Boolean)]
    public bool Deleted { get; set; }

    [Version]
    public int Version { get; set; } = 1;

    [UpdateIgnore]
    [Column(DbType = "VARCHAR(50)")]
    public string CreatedBy { get; set; } = "";

    public DateTime? UpdatedAt { get; set; }
}
```
<!-- /snippet -->

<!-- snippet: sample-ticket -->
```csharp
/// <summary>工单：多租户（示例只看 1 号租户）、更新时自动填的时间、加密的联系方式。</summary>
[Table("t_ticket")]
public class Ticket
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    /// <summary>插入时由租户拦截器填。</summary>
    public int TenantId { get; set; }

    [Column(DbType = "VARCHAR(100)")]
    public string Title { get; set; } = "";

    [Encryption(typeof(ReverseEncryption))]
    [Column(DbType = "VARCHAR(100)")]
    public string Contact { get; set; } = "";

    public DateTime? TouchedAt { get; set; }
}
```
<!-- /snippet -->

## 逻辑删除

属性标 `[LogicDelete(策略)]`。查询、按条件更新、隐式 JOIN、子查询、带出都自动加上"未删除"的条件；删除改写成 UPDATE。

<!-- card: rules/LogicDeleteQuery sql=mysql -->
```csharp
db.Query<Doc>().OrderBy(d => d.Id).Select(d => d.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_doc` t WHERE t.`deleted` = ? ORDER BY t.`id` ASC;
-- params: [false]
```
<!-- /card -->

<!-- card: write/LogicDelete sql=mysql -->
```csharp
var doc = await db.Query<Doc>().FindAsync(2L);
return await db.Delete(doc!).ExecuteAsync();
```

```sql
-- MySQL
SELECT `id`,`title`,`deleted`,`version`,`created_by`,`updated_at` FROM `t_doc` WHERE `deleted` = ? AND `id` = ?;
-- params: [false, 2]
UPDATE `t_doc` SET `deleted` = ?,`version` = ? WHERE `deleted` = ? AND `version` = ? AND `id` = ?;
-- params: [true, 2, false, 1, 2]
```
<!-- /card -->

| 策略 | 列类型 | 未删除 | 删除时写 |
|---|---|---|---|
| `LogicDeleteStrategy.Boolean`（默认） | `bool` | `= false` | `true` |
| `LogicDeleteStrategy.DeletedTime` | `DateTime?` | `IS NULL` | 当前时间 |
| `LogicDeleteStrategy.DeletedTimestamp` | `long` | `= 0` | 当前的 Unix 毫秒数 |
| `LogicDeleteStrategy.Custom` + `StrategyName` | 任意 | 由策略决定 | 由策略决定 |

<!-- snippet: rules-logic-delete -->
```csharp
[Table("t_audit_log")]
public class AuditLog
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [LogicDelete(LogicDeleteStrategy.DeletedTime)]
    public DateTime? DeletedAt { get; set; }              // NULL 表示未删除，删除时写当前时间
}
```
<!-- /snippet -->

- 自定义策略实现 `ILogicDeleteStrategy`（`NotDeleted` 给出未删除的条件，`DeletedValues` 给出删除时写的列和值，可以同时写删除人、删除时间），用 `options.AddLogicDeleteStrategy("名字", 策略)` 注册，实体上写 `[LogicDelete(LogicDeleteStrategy.Custom, StrategyName = "名字")]`。
- 不改实体时用代码配置：`options.ApplyLogicDelete(t => t == typeof(Config), nameof(Config.Deleted), new LogicDeleteAttribute())`。
- 查出已删除的行：查询上 `DisableLogicDelete()`（整条查询）、`DisableTableLogicDelete()`（最近一张表）；导航上 `u.Docs!.DisableLogicDelete()`、对一导航 `Sql.DisableLogicDelete(p.Author)`。
- 真删：删除命令上 `.Hard()`。

<!-- card: rules/LogicDeleteDisabled sql=mysql -->
```csharp
db.Query<Doc>().DisableLogicDelete().CountAsync();
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_doc`;
```
<!-- /card -->

<!-- card: rules/HardDelete sql=mysql -->
```csharp
await db.Delete<Doc>().Where(d => d.Title == "接入指南").IgnoreVersion().ExecuteAsync();
return await db.Delete<Doc>().Where(d => d.Title == "接入指南").IgnoreVersion().Hard().ExecuteAsync();
```

```sql
-- MySQL
UPDATE `t_doc` SET `deleted` = ? WHERE `deleted` = ? AND `title` = ?;
-- params: [true, false, '接入指南']
DELETE FROM `t_doc` WHERE `title` = ?;
-- params: ['接入指南']
```
<!-- /card -->

## 乐观锁

属性标 `[Version]`：整数列每次更新加一，`[Version(VersionStrategy.NewGuid)]` 每次换一个新的 GUID 字符串。按实体更新时 WHERE 带上旧版本号；影响 0 行说明被别人改过，用 `ExecuteAsync(期望行数, 消息)` 让它抛 `ConcurrencyException`：

<!-- card: rules/VersionUpdate sql=mysql -->
```csharp
var doc = await db.Query<Doc>().FindOrThrowAsync(1L);
doc.Title = "接入指南（第二版）";
return await db.Update(doc).ExecuteAsync(1, "文档已被别人修改，请刷新后再试");
```

```sql
-- MySQL
SELECT `id`,`title`,`deleted`,`version`,`created_by`,`updated_at` FROM `t_doc` WHERE `deleted` = ? AND `id` = ?;
-- params: [false, 1]
UPDATE `t_doc` SET `title` = ?,`updated_at` = ?,`version` = ? WHERE `deleted` = ? AND `version` = ? AND `id` = ?;
-- params: ['接入指南（第二版）', '2026-11-01 08:00:00', 2, false, 1, 1]
```
<!-- /card -->

按条件更新、删除不知道当前版本，必须说明：`WithVersion(版本)` 带上版本条件，或 `IgnoreVersion()` 不检查也不改版本号。两个都不写时抛异常：

<!-- card: rules/VersionCondition sql=mysql -->
```csharp
await db.Update<Doc>().Set(d => d.Title, "改个标题").Where(d => d.Id == 1).WithVersion(1).ExecuteAsync();
return await db.Update<Doc>().Set(d => d.Title, "管理端修正").Where(d => d.Id == 1).IgnoreVersion().ExecuteAsync();
```

```sql
-- MySQL
UPDATE `t_doc` SET `title` = ?,`updated_at` = ?,`version` = ? WHERE `deleted` = ? AND `version` = ? AND `id` = ?;
-- params: ['改个标题', '2026-11-01 08:00:00', 2, false, 1, 1]
UPDATE `t_doc` SET `title` = ?,`updated_at` = ? WHERE `deleted` = ? AND `id` = ?;
-- params: ['管理端修正', '2026-11-01 08:00:00', false, 1]
```
<!-- /card -->

## 拦截器

| 基类 | 作用 | 对应 Java |
|---|---|---|
| `QueryFilter<T>` | 查询、按条件更新、删除自动追加条件（多租户、数据权限） | `PredicateFilterInterceptor` |
| `EntityInterceptor<T>` | `BeforeInsert` / `BeforeUpdate` 写入前改实体；`BeforeConditionUpdate` 给按条件更新追加 SET；`BeforeEntityUpdateColumns` 给按实体更新追加列 | `EntityInterceptor`、`UpdateSetInterceptor` |

`T` 可以是实体本身、基类或接口（如 `QueryFilter<ITenantEntity>` 作用于所有租户实体）。用 `options.AddInterceptor(..)` 注册；用依赖注入时容器里登记的 `IInterceptor` 自动加入。示例库的拦截器：

<!-- snippet: sample-interceptors -->
```csharp
/// <summary>工单只看 1 号租户。</summary>
public sealed class TenantFilter : QueryFilter<Ticket>
{
    /// <inheritdoc />
    public override Expression<Func<Ticket, bool>>? Filter() => t => t.TenantId == 1;
}

/// <summary>工单：插入时没写租户就填 1 号；更新时填更新时间。</summary>
public sealed class TicketFill : EntityInterceptor<Ticket>
{
    /// <inheritdoc />
    public override void BeforeInsert(Ticket entity)
    {
        if (entity.TenantId == 0) entity.TenantId = 1;
    }

    /// <inheritdoc />
    public override void BeforeUpdate(Ticket entity) => entity.TouchedAt = Now;

    /// <inheritdoc />
    public override void BeforeConditionUpdate(UpdateSetBuilder<Ticket> sets) => sets.Set(t => t.TouchedAt, Now);
}

/// <summary>文档：更新时填更新时间。</summary>
public sealed class DocTouch : EntityInterceptor<Doc>
{
    /// <inheritdoc />
    public override void BeforeUpdate(Doc entity) => entity.UpdatedAt = Now;

    /// <inheritdoc />
    public override void BeforeConditionUpdate(UpdateSetBuilder<Doc> sets) => sets.Set(d => d.UpdatedAt, Now);
}
```
<!-- /snippet -->

`Filter()` 每次执行时调用，可以读当前上下文（当前租户、当前用户），返回 null 表示这次不加条件。条件同时作用于根表、LEFT JOIN 的导航（写进 ON）、子查询、对象树里的集合，以及按条件的更新和删除：

<!-- card: rules/TenantFilter sql=mysql -->
```csharp
await db.Delete<Ticket>().Where(t => t.Title == "登录不上").ExecuteAsync();
return await db.Query<Ticket>().OrderBy(t => t.Id).Select(t => t.Title).ToListAsync();
```

```sql
-- MySQL
DELETE FROM `t_ticket` WHERE `tenant_id` = ? AND `title` = ?;
-- params: [1, '登录不上']
SELECT t.`title` FROM `t_ticket` t WHERE t.`tenant_id` = ? ORDER BY t.`id` ASC;
-- params: [1]
```
<!-- /card -->

<!-- card: rules/FillBeforeWrite sql=mysql -->
```csharp
await db.Insert(new Ticket { Title = "新工单", Contact = "13800000009" }).ExecuteAsync();
return await db.Update<Ticket>().Set(t => t.Title, "发票抬头写错了（已处理）").Where(t => t.Id == 1).ExecuteAsync();
```

```sql
-- MySQL
INSERT INTO `t_ticket` (`tenant_id`,`title`,`contact`) VALUES (?,?,?);
-- params: [1, '新工单', 'enc:90000000831']
UPDATE `t_ticket` SET `title` = ?,`touched_at` = ? WHERE `tenant_id` = ? AND `id` = ?;
-- params: ['发票抬头写错了（已处理）', '2026-11-01 08:00:00', 1, 1]
```
<!-- /card -->

单条语句上按名字关掉或打开（名字默认是类型名，可以重写 `Name`）：

<!-- card: rules/DisableInterceptor sql=mysql -->
```csharp
db.Query<Ticket>().DisableInterceptor(nameof(SampleDb.TenantFilter)).CountAsync();
```

```sql
-- MySQL
SELECT COUNT(*) FROM `t_ticket`;
```
<!-- /card -->

| 方法 | 作用 |
|---|---|
| `DisableInterceptor("名字")` / `EnableInterceptor("名字")` | 关掉 / 打开某一个 |
| `DisableInterceptors()` / `EnableInterceptors()` | 关掉 / 打开全部（`IsProtected` 为 true 的关不掉，只能按名字关） |
| 拦截器上 `EnabledByDefault => false` | 默认不生效，只在 `EnableInterceptor(名字)` 的语句上生效 |

## 列加密

属性标 `[Encryption(typeof(策略))]`：写入时加密，读出时解密，等值条件的值整体加密后与密文比较：

<!-- card: rules/EncryptedColumn sql=mysql -->
```csharp
db.Query<Ticket>().Where(t => t.Contact == "13800000001").OrderBy(t => t.Id).Select(t => t.Title).ToListAsync();
```

```sql
-- MySQL
SELECT t.`title` FROM `t_ticket` t WHERE t.`tenant_id` = ? AND t.`contact` = ? ORDER BY t.`id` ASC;
-- params: [1, 'enc:10000000831']
```
<!-- /card -->

自带的策略与 Java 版的密文兼容（同一密钥加密的数据两边都能读）：

| 策略 | 说明 |
|---|---|
| `AesBase64EncryptionStrategy` | AES 加密、Base64 存储；子类给出 `Key`、`Iv` |
| `Sm4HexEncryptionStrategy` | 国密 SM4、十六进制存储；子类给出 `Key` |
| 实现 `IEncryptionStrategy` | 任意可逆加密 |

上面两个都继承 `SegmentedEncryptionStrategy`：明文按片段分别加密再拼接，`SupportQueryLike = true` 时 `Contains` / `StartsWith` 这类 LIKE 条件的值也按片段加密，于是能在密文上做模糊查询：

<!-- snippet: rules-encryption -->
```csharp
/// <summary>手机号用 AES 加密、Base64 存储，与 Java 版的密文兼容；密钥从环境变量读，不写进代码。</summary>
public sealed class PhoneEncryption : AesBase64EncryptionStrategy
{
    protected override string Key => Environment.GetEnvironmentVariable("PHONE_AES_KEY")!;

    protected override string Iv => Environment.GetEnvironmentVariable("PHONE_AES_IV")!;
}

[Table("t_member")]
public class Member
{
    [Column(PrimaryKey = true, Generated = true)]
    public long Id { get; set; }

    [Encryption(typeof(PhoneEncryption), SupportQueryLike = true)]
    public string? Phone { get; set; }                    // 分段加密：Phone.Contains("1380") 也能查
}
```
<!-- /snippet -->

需要在数据库里加解密（如 MySQL 的 `AES_ENCRYPT`）时，改用 `[Column(SqlConversion = typeof(…))]`：继承 `ColumnSqlConversion`，给出读（`Read`）、写（`Write`）、条件里列的写法（`Column`）与比较值的写法（`CompareValue`）。

## 限制

- 逻辑删除不处理唯一索引冲突（已删除的行仍占着唯一键），需要时把删除时间放进唯一索引，或用 `DeletedTime` 策略。
- 有版本列的实体，按条件写入时必须写 `WithVersion` 或 `IgnoreVersion`，否则抛异常。
- 分段加密的密文比明文长得多（每个片段一段定长密文），列要留够长度；LIKE 只支持连续片段的匹配。
- 拦截器、加密策略的实例由 `EasyDb` 一直持有，要线程安全；随请求变化的值通过单例的访问器取（如 `IHttpContextAccessor`）。

## 对应 Java

| Java | C# |
|---|---|
| `@LogicDelete(strategy = LogicDeleteStrategyEnum.BOOLEAN / DELETE_LONG_TIMESTAMP / LOCAL_DATE_TIME …)` | `[LogicDelete(LogicDeleteStrategy.Boolean / DeletedTimestamp / DeletedTime)]` |
| `AbstractLogicDeleteStrategy`、`strategyName` | `ILogicDeleteStrategy`、`options.AddLogicDeleteStrategy(..)`、`StrategyName` |
| `AbstractConfigurationLogicDeleteStrategy` | `options.ApplyLogicDelete(..)` |
| `disableLogicDelete()` / `useLogicDelete(false)` | `DisableLogicDelete()` |
| 逻辑删除的实体真删：`disableLogicDelete().allowDeleteStatement(true)` | `.Hard()` |
| `deleteThrow` 配置、命令上 `allowDeleteStatement(true)` | `EasyDbOptions.AllowDeleteStatement = false`、命令上 `AllowDeleteStatement(true)` |
| `@Version`、`VersionIntStrategy` / `VersionUUIDStrategy` | `[Version]`、`[Version(VersionStrategy.NewGuid)]` |
| `withVersion(v)` / `ignoreVersion()` | `WithVersion(v)` / `IgnoreVersion()` |
| `executeRows(1, "msg")` | `ExecuteAsync(1, "msg")`，抛 `ConcurrencyException` |
| `PredicateFilterInterceptor` | `QueryFilter<T>` |
| `EntityInterceptor`（`configureInsert` / `configureUpdate`） | `EntityInterceptor<T>`（`BeforeInsert` / `BeforeUpdate` / `BeforeEntityUpdateColumns`） |
| `UpdateSetInterceptor` | `EntityInterceptor<T>.BeforeConditionUpdate` |
| `noInterceptor(name)` / `useInterceptor(name)` | `DisableInterceptor(name)` / `EnableInterceptor(name)` |
| `@Encryption(strategy, supportQueryLike)` | `[Encryption(typeof(…), SupportQueryLike = true)]` |
| `AbstractSupportEmojiAesBase64EncryptionStrategy` 等分段加密策略、SM4 策略 | `AesBase64EncryptionStrategy`、`Sm4HexEncryptionStrategy`（`SupportEmoji` 对应 SupportEmoji / UnSupportEmoji 两种） |
| `ColumnValueSQLConverter` | `ColumnSqlConversion` |
