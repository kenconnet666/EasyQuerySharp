# 从 Java 版、TS 版迁移

EasyQuerySharp 与 easy-query（Java）、easy-query-ts 是同一套思路：实体加导航、lambda 里写条件、隐式 JOIN、隐式子查询、对象树结果、聚合保存。生成的 SQL 以 Java 版为准（Java 的 2404 个用例逐条移植，SQL 逐字比对，见[能力对照总表](../parity.md)），API 按 C# 的习惯写。

## 写法上的几个大差别

```java
// Java
List<SysUser> users = easyEntityQuery.queryable(SysUser.class)
    .where(u -> {
        u.age().ge(18);
        u.company().name().like("Acme");
    })
    .orderBy(u -> u.name().asc())
    .toList();
```

```ts
// TypeScript
const users = await db
  .queryable(SysUser)
  .where((u) => [u.age.ge(18), u.company.name.like('Acme')])
  .orderBy((u) => u.name.asc())
  .toList();
```

C# 的写法（示例模型上的同一类查询）：

<!-- card: start/FirstQuery sql=mysql -->
```csharp
db.Query<Employee>()
    .Where(e => e.Age >= 30 && e.Company!.City == "杭州")
    .OrderBy(e => e.Name)
    .Select(e => new EmployeeRow(e.Name, e.Age, e.Company!.Name))
    .ToListAsync();
```

```sql
-- MySQL
SELECT t.`name`,t.`age`,t1.`name` AS `company` FROM `t_user` t LEFT JOIN `t_company` t1 ON t1.`id` = t.`company_id` WHERE t.`age` >= ? AND t1.`city` = ? ORDER BY t.`name` ASC;
-- params: [30, '杭州']
```
<!-- /card -->

- **表达式树代替代理对象**：lambda 里写的是普通 C# 表达式（`>=`、`&&`、`==`），作用在实体本身上；没有 Java 的 APT 代理类（`SysUserProxy`），也没有 TS 的引用对象。
- **条件是一个布尔表达式**：Java 的语句块里多条条件是 AND，C# 用 `&&` 或多个 `Where`；OR 直接写 `||`。动态条件用 `WhereIf(条件, ..)`。
- **导航用 `!` 断言**：表达式树里不能写 `?.`，`u.Company!.Name` 取不到时就是 NULL（LEFT JOIN）。
- **全异步**：终结方法都是 `…Async(CancellationToken)`，写入一律以 `ExecuteAsync()` 结束；没有同步版本。
- **查询对象不可变**：每一步返回新的查询，可以存进变量接着用，不用 `cloneQueryable()`。
- **环境事务与追踪作用域**：挂在 AsyncLocal 上（`await using var tx = db.BeginTransaction()`、`using var tracking = db.BeginTracking()`），不靠 ThreadLocal。
- **SQL 函数是 BCL 方法或扩展方法**：`u.Name.Contains("a")`、`d.AddDays(1)`、`Math.Round(x, 2)`，不是列对象上的方法链。

## Java → C#

### 配置与实体

| Java | C# | 在哪一页 |
|---|---|---|
| `EasyQueryBootstrapper.defaultBuilderConfiguration().setDefaultDataSource(ds).useDatabaseConfigure(new MySQLDatabaseConfiguration())` | `new EasyDb(o => o.UseMySql(dataSource))` | [入门](getting-started.md) |
| Spring Boot starter、Solon 插件 | `services.AddEasyQuery(..)` | [依赖注入](dependency-injection.md) |
| `@EntityProxy`、APT 生成的代理 | 不需要 | — |
| `@Table` / `@Column` / `@ColumnIgnore` / `@InsertIgnore` / `@UpdateIgnore` | `[Table]` / `[Column]` / `[ColumnIgnore]` / `[InsertIgnore]` / `[UpdateIgnore]` | [实体与映射](entities.md) |
| `@Column(conversion = …)`、`ValueConverter` | `[Column(Converter = typeof(…))]`、`ValueConverter<TModel, TProvider>` | [实体与映射](entities.md#枚举与值转换) |
| `@Column(sqlConversion = …)`、`ColumnValueSQLConverter` | `[Column(SqlConversion = …)]`、`ColumnSqlConversion`；虚拟列用 `[Computed]` | [实体与映射](entities.md#计算列) |
| `@Navigate` | `[Navigate]` | [联表与导航](joins-and-navigations.md) |
| `@LogicDelete` / `@Version` / `@Encryption` | `[LogicDelete]` / `[Version]` / `[Encryption]` | [列规则](rules.md) |
| `PredicateFilterInterceptor` / `EntityInterceptor` / `UpdateSetInterceptor` | `QueryFilter<T>` / `EntityInterceptor<T>` | [列规则](rules.md#拦截器) |
| `NameConversion` | `EasyDbOptions.NameConversion` | [实体与映射](entities.md#命名约定) |
| `JdbcExecutorListener` | `IExecutionListener` | [原生 SQL](raw-sql.md) |
| `getDatabaseCodeFirst()`、`@Column(dbType, comment, dbDefault, oldName)`、`@TableIndex` | `db.CodeFirst`、`[Column(DbType, Comment, DbDefault)]`、`[RenamedFrom]`、`[TableIndex]` | [建表与同步](code-first.md) |
| `@ShardingTableKey` / `@ShardingDataSourceKey` 与路由规则 | `options.Shard<T>(..)`、`IShardRule` | [分库分表](sharding.md) |
| `sql-cache` 的 `kvStorage` / `allStorage` | `EasyCache` 的 `Kv<T>()` / `All<T>()` | [二级缓存](caching.md) |

### 查询

| Java | C# | 在哪一页 |
|---|---|---|
| `queryable(T.class)` | `db.Query<T>()` | [查询](queries.md) |
| `where(o -> o.a().eq(1))` / `where(条件, ..)` | `Where(o => o.A == 1)` / `WhereIf(条件, ..)` | [查询](queries.md#条件) |
| `whereById` / `whereByIds` | `WhereById` / `WhereByIds` | |
| `whereObject(obj)` / `orderByObject(obj)` | `FilterBy(obj)` / `SortBy(obj)` | [动态筛选](filter.md) |
| `filterConfigure(NotNullOrEmptyValueFilter.DEFAULT)` | `UseValueFilter(ValueFilter.NotNullOrBlank)` | [查询](queries.md#动态条件) |
| `like` / `contains` / `startsWith` / `endsWith` | `Contains` / `LikeContains` / `LikeStartsWith` / `LikeEndsWith` | [函数](functions.md#字符串) |
| `in` / `notIn` / `rangeClosed` / `isNull` | `x.In(..)` / `NotIn` / `RangeClosed` / `== null` | [查询](queries.md#likein区间) |
| `exists` / `notExists` / `subQuery` | `Sql.Exists` / `Sql.NotExists` / `Sql.SubQuery` | [查询](queries.md#子查询) |
| `orderBy(o -> o.a().asc())` / `orderByAsc` | `OrderBy(o => o.A)` / `ThenBy` | [查询](queries.md#排序与分页) |
| `limit(offset, rows)` | `Skip(offset).Take(rows)` | |
| `select(o -> Select.DRAFT.of(o.a(), o.b()))` | `Select(o => new Draft<..>(o.A, o.B))` 或 record | [查询](queries.md#投影) |
| `select(Dto.class)` / `selectAutoInclude(Dto.class)` | `Select<TDto>()` | [带出与对象树](include-and-trees.md#按-dto-结构自动带出) |
| `@NavigateFlat` / `@NavigateJoin` | `[MapPath("A.B")]` / `[MapPath("A.B", Join = true)]` | [查询](queries.md#映射到-dto) |
| `EXTRA_AUTO_INCLUDE_CONFIGURE` | `IAutoIncludeConfigure<T>`、`[MapExpression]` | |
| `toList()` / `firstOrNull()` / `firstNotNull()` | `ToListAsync()` / `FirstOrDefaultAsync()` / `FirstAsync()` | [查询](queries.md#终结方法) |
| `singleOrNull()` / `singleNotNull()` | `SingleOrDefaultAsync()` / `SingleAsync()` | |
| `findOrNull(id)` / `findNotNull(id)` | `FindAsync(id)` / `FindOrThrowAsync(id)` | |
| `any()` / `required()` / `count()` | `AnyAsync()` / `EnsureAnyAsync()` / `CountAsync()` | |
| `sumOrNull` / `maxOrNull` / `avgOrNull` | `SumAsync` / `MaxAsync` / `AverageAsync` | [分组与聚合](grouping.md#整表聚合) |
| `toPageResult(page, size)` / `toPageSelectResult(..)` | `ToPageAsync(page, size)` / `ToPageAsync(page, size, q => q.Select<TDto>())` | |
| `toStreamResult` / `toChunk` / `offsetChunk` | `AsAsyncEnumerable()` / `ToChunksAsync(n)` / `OffsetChunkAsync(..)` | [查询](queries.md#流式读取) |
| `toResultSet` | `ReadAsync(..)` | [原生 SQL](raw-sql.md) |
| `toSQL()` | `ToSql()` | |
| `leftJoin(B.class, (a, b) -> ..)` / `innerJoin` / `rightJoin` | `LeftJoin<B>((a, b) => ..)` / `InnerJoin` / `RightJoin` | [联表与导航](joins-and-navigations.md) |
| `groupBy(o -> GroupKeys.of(..))` / `having` | `GroupBy(o => new { .. })` / `Having` | [分组与聚合](grouping.md) |
| `union` / `unionAll` / `toCteAs` | `Union` / `UnionAll` / `AsCte` | [查询](queries.md#合并cte派生表) |
| `forUpdate()` | `ForUpdate()` | [事务与行锁](transactions.md) |
| `asTracking()` / `asNoTracking()` | `AsTracking()` / `AsNoTracking()` | [追踪与聚合保存](save-and-tracking.md) |
| `disableLogicDelete()` / `noInterceptor(name)` / `useInterceptor(name)` | `DisableLogicDelete()` / `DisableInterceptor(name)` / `EnableInterceptor(name)` | [列规则](rules.md) |
| `asTable` / `asSchema` / `asTableLink` / `asTableSegment` / `asAlias` | `AsTable` / `InSchema`（查询）、`AsSchema`（写入）/ `WithTableLink` / `AsTableSegment` / `AsAlias` | [换表](tables.md) |
| `mapQueryable` / `toMap()` | `db.Table("…")` / `Select<Dictionary<string, object?>>()` | |

### 导航与对象树

| Java | C# | 在哪一页 |
|---|---|---|
| `o.user().name()` | `o.User!.Name` | [联表与导航](joins-and-navigations.md#对一导航隐式-join) |
| `u.bankCards().any(..)` / `none(..)` / `count()` / `sum(..)` | `u.BankCards!.Any(..)` / `!u.BankCards!.Any(..)` / `Count()` / `Sum(..)` | [联表与导航](joins-and-navigations.md#集合导航隐式子查询) |
| `u.bankCards().where(..).first()` / `element(n)` | `u.BankCards!.Where(..).First()` / `ElementAt(n)` | |
| `elements(i, j)` | `OrderBy(..).Skip(i).Take(j - i + 1)` | |
| `flatElement()` | 条件里写成 `Any(..)`；展开成行用 `SelectMany` | |
| `joining(..)` | `string.Join(",", u.BankCards!.Select(..))` | |
| `subQueryToGroupJoin(..)` | `GroupJoin(..)` / `[Navigate(GroupJoin = true)]` | |
| `include(..)` / `includes(..)` | `Include(..)` | [带出与对象树](include-and-trees.md) |
| `fillMany` / `fillOne` | `FillMany` / `FillOne` | |
| `asTreeCTE` / `toTreeList` | `AsTreeCte` / `ToTreeAsync` | [带出与对象树](include-and-trees.md#树) |

### 函数

| Java | C# |
|---|---|
| `o.name().length()` / `toUpper()` / `subString(i, n)` / `trim()` | `o.Name.Length` / `ToUpper()` / `Substring(i, n)` / `Trim()` |
| `concat` / `stringFormat` | `+`、插值字符串 |
| `format("yyyy-MM")` / `plusDays(n)` / `duration(a, b)` | `ToString("yyyy-MM")` / `AddDays(n)` / `(b - a).TotalDays` |
| `nullOrDefault(x)` | `?? x` |
| `caseWhen(..).then(..).elseEnd(..)` | 三元表达式 |
| `toStr()` / `toNumber(Long.class)` | `ToString()` / `long.Parse(..)` |
| `rowNumberOver()` / `offset(..).prev(n)` | `Sql.Over.RowNumber(..)` / `Sql.Over.Lag(..)` |
| `sqlNativeSegment` / `expression().sql(..)` | `Sql.Raw<T>(..)`、`[SqlFunction]` |

完整的函数对照见[函数](functions.md#对应-java)。

### 写入、保存、事务

| Java | C# | 在哪一页 |
|---|---|---|
| `insertable(e).executeRows()` / `executeRows(true)` | `db.Insert(e).ExecuteAsync()` / `.FillGeneratedKeys().ExecuteAsync()` | [写入](writes.md) |
| `onConflictThen(..)` | `OnConflictUpdate(..)` / `OnConflictIgnore(..)` | [写入](writes.md#upsert冲突时忽略或更新) |
| `updatable(T.class).setColumns(..).where(..)` | `db.Update<T>().Set(..).Where(..)` | |
| `updatable(e)` / `setColumns` / `whereColumns` | `db.Update(e)` / `SetColumns` / `WhereColumns` | |
| `deletable(T.class).where(..)` / `allowDeleteStatement(true)` | `db.Delete<T>().Where(..)` / `.Hard()`、`AllowDeleteStatement(true)` | |
| `batch()` / `setSQLStrategy(..)` | `.Batch()` / `.Columns(ColumnStrategy.X)` | |
| `withVersion` / `ignoreVersion` / `executeRows(1, msg)` | `WithVersion` / `IgnoreVersion` / `ExecuteAsync(1, msg)` | [列规则](rules.md#乐观锁) |
| `mapInsertable` / `mapUpdatable` | `db.InsertMap` / `db.UpdateMap` | |
| `TrackManager.begin()` / `addTracking` | `db.BeginTracking()` / `db.AddTracking` | [追踪与聚合保存](save-and-tracking.md) |
| `savable(root).executeCommand()` | `db.Save(root).ExecuteAsync()` | |
| `beginTransaction()` / `commit()` | `db.BeginTransaction()` / `CommitAsync()` | [事务与行锁](transactions.md) |
| `sqlQuery` / `sqlExecute` | `db.SqlQueryAsync<T>` / `db.ExecuteSqlAsync` | [原生 SQL](raw-sql.md) |

### Java 有、C# 不做或换了做法

| Java | C# |
|---|---|
| search 扩展（`sql-search`） | 不做：用 `FilterBy` / `SortBy` / `TableRequest` |
| SQL Server 2008 的 ROW_NUMBER 分页 | 不做 |
| H2 | 没有 .NET 驱动，不支持 |
| 同步 API | 只有异步 |
| `cloneQueryable()` | 不需要：查询不可变 |
| `@ValueObject`（已弃用） | `[ValueObject]`（TS 的 `embed()` 语义） |

## TS → C#

| TypeScript | C# |
|---|---|
| `entity('t_user', { id: col.bigint().primaryKey().generated(), … })` | `[Table("t_user")] class User { [Column(PrimaryKey = true, Generated = true)] long Id … }` |
| `defineSchema(..)`、`r.one` / `r.many` / `r.manyToMany` | 实体上的 `[Navigate(RelationType.X, ..)]` |
| `createDb({ schema, adapter })` | `new EasyDb(o => o.UseXxx(dataSource))` |
| `db.queryable(User)` | `db.Query<User>()` |
| `.where((u) => [a, b])` | `.Where(u => a && b)` |
| `.orderBy((u) => u.name.asc())` | `.OrderBy(u => u.Name)` |
| `.limit(n)` / `.offset(n)` | `.Take(n)` / `.Skip(n)` |
| `.toList()` / `.first()` / `.firstOrThrow()` | `ToListAsync()` / `FirstOrDefaultAsync()` / `FirstAsync()` |
| `.find(id)` / `.findOrThrow(id)` / `.whereKey(id)` / `.whereKeys(ids)` | `FindAsync(id)` / `FindOrThrowAsync(id)` / `WhereById(id)` / `WhereByIds(ids)` |
| `.toPage(page, size)` / `.count()` / `.any()` | `ToPageAsync(page, size)` / `CountAsync()` / `AnyAsync()` |
| `.select((u) => ({ name: u.name, posts: u.posts.toList(..) }))` | `.Select(u => new UserVo(u.Name, u.Posts!.Select(..).ToList()))` |
| `defineView` | `[Projection]` 方法、`Project(map)`、`Select<TDto>()` |
| `u.posts.at(i)` / `u.posts.first` | `u.Posts!.ElementAt(i)` / `First()` |
| `groupJoin()` | `GroupJoin(..)` |
| `embed()` | `[ValueObject]` |
| `defineComputed` | `[Computed]` |
| `filterBy` / `sortBy` / `table(request)`、`parseTableRequest` | `FilterBy` / `SortBy` / `ToPageAsync(TableRequest.Parse(..))` |
| `mapTables` / `inSchema` / `withTableLink` / `tableHint` | `MapTable<T>` / `InSchema` / `WithTableLink` / `AsTableSegment` |
| `db.with(..)` / `withRecursive` / `descendantsOf` / `ancestorsOf` | `AsCte(..)` / `AsTreeCte(..)`（`Up = true` 取上级） |
| `fillMany` / `fillOne` | `FillMany` / `FillOne` |
| `` sql`…${v}…` `` | `Sql.Raw<T>(..)`；整条 SQL 用 `db.SqlQueryAsync<T>($"…{v}…")` |
| `db.insert(X).values(..)` / `.returnId()` | `db.Insert(..)` / `.FillGeneratedKeys()` |
| `.onConflict(..).merge()` / `.ignore()` | `OnConflictUpdate(..)` / `OnConflictIgnore(..)` |
| `db.update(X).set(..).where(..)` / `.entity(row)` | `db.Update<X>().Set(..).Where(..)` / `db.Update(row)` |
| `changes(before, after)` | `db.Update(after).Changes(before)` |
| `db.delete(X).where(..)` / `.hard()` | `db.Delete<X>().Where(..)` / `.Hard()` |
| `.returning(..)` | `.Returning(..)` |
| `.expectRows(n)` | `ExecuteAsync(n, 消息)` |
| `db.save(..)` | `db.Save(..).ExecuteAsync()`（必须在追踪作用域与事务里） |
| `db.transaction(async () => ..)` / `db.begin()` | `db.TransactionAsync(async () => ..)` / `db.BeginTransaction()` |
| `forUpdate()` / `forShare()` | `ForUpdate()` / 不支持 |
| `withSignal(signal)` | `CancellationToken` 参数 |
| `onExecute` | `IExecutionListener` |
| `createTables` / `createTablesSql` | `db.CodeFirst.CreateTablesAsync(..)` / `CreateTableSql(type)`；同步、迁移开发中 |

TS 版独有、C# 还没有的：多态实体（`entity.union`）、只读库（`db.readonly()`）、读写分离（`replicas`）、N+1 检测、`mergeSchemas` / `db.mount`、`eqts pull` / `eqts snapshot`、`forShare` / `advisoryLock`；其中有计划的记在 [BACKLOG](../BACKLOG.md)。
