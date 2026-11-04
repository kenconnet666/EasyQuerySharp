using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 插入、更新、删除共有的部分：目标表（AsTable / AsSchema）与拦截器的开关。
/// 写入命令是一次性的可变构建器（与不可变的查询不同），链式调用返回自身。
/// </summary>
public abstract class EntityCommandBase<TSelf, T>
    where TSelf : EntityCommandBase<TSelf, T>
    where T : class
{
    private string? _tableName;
    private string? _schema;
    private string? _alias;

    private protected EntityCommandBase(EasyDb db)
    {
        Db = db;
        Entity = db.Metadata.Get<T>();
    }

    /// <summary>所属的 <see cref="EasyDb"/>。</summary>
    public EasyDb Db { get; }

    private protected EntityMetadata Entity { get; }

    private protected InterceptorSelection Interceptors { get; private set; } = InterceptorSelection.Default;

    private protected TSelf Self => (TSelf)this;

    /// <summary>写入到另一张同结构的表（对应 Java 的 asTable）。</summary>
    public TSelf AsTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        _tableName = tableName;
        return Self;
    }

    /// <summary>按当前表名算出新表名（对应 Java 的 asTable(Function)），如 <c>.AsTable(name => name + "_2024")</c>。</summary>
    public TSelf AsTable(Func<string, string> rename)
    {
        ArgumentNullException.ThrowIfNull(rename);
        return AsTable(rename(_tableName ?? Entity.TableName));
    }

    /// <summary>写入到另一个 schema（对应 Java 的 asSchema）。</summary>
    public TSelf AsSchema(string schema)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        _schema = schema;
        return Self;
    }

    /// <summary>按当前 schema 算出新 schema（对应 Java 的 asSchema(Function)）；实体没有 schema 时传入空串。</summary>
    public TSelf AsSchema(Func<string, string> rename)
    {
        ArgumentNullException.ThrowIfNull(rename);
        return AsSchema(rename(_schema ?? Entity.Schema ?? ""));
    }

    /// <summary>给目标表起别名（对应 Java 的 asAlias），列引用随之带上别名：<c>UPDATE `t_blog` xx SET xx.`deleted` = ?</c>。</summary>
    public TSelf AsAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrEmpty(alias);
        _alias = alias;
        return Self;
    }

    /// <summary>不应用名为 <paramref name="name"/> 的拦截器（对应 Java 的 noInterceptor(name)）。</summary>
    public TSelf DisableInterceptor(string name)
    {
        Interceptors = Interceptors.Disable(name);
        return Self;
    }

    /// <summary>不应用任何拦截器（对应 Java 的 noInterceptor()）；之后再 EnableInterceptor 的除外。</summary>
    public TSelf DisableInterceptors()
    {
        Interceptors = Interceptors.DisableAll();
        return Self;
    }

    /// <summary>应用名为 <paramref name="name"/> 的拦截器，包括默认关闭的（对应 Java 的 useInterceptor(name)）。</summary>
    public TSelf EnableInterceptor(string name)
    {
        Interceptors = Interceptors.Enable(name);
        return Self;
    }

    /// <summary>恢复默认生效的全部拦截器（对应 Java 的 useInterceptor()）。</summary>
    public TSelf EnableInterceptors()
    {
        Interceptors = Interceptors.EnableAll();
        return Self;
    }

    private protected EntityTableSource NewTable() => new(Entity) { TableNameOverride = _tableName, SchemaOverride = _schema, ExplicitAlias = _alias };
}

/// <summary>更新、删除的公共部分：条件、关闭逻辑删除、无条件写入的保护。</summary>
public abstract class WriteCommandBase<TSelf, T> : EntityCommandBase<TSelf, T>
    where TSelf : WriteCommandBase<TSelf, T>
    where T : class
{
    private readonly List<LambdaExpression> _wheres = [];
    private bool _ignoreVersion;
    private object? _version;

    private protected WriteCommandBase(EasyDb db, IReadOnlyList<T>? entities) : base(db) => Entities = entities;

    // 按实体写入时的实体；按条件写入时为 null
    private protected IReadOnlyList<T>? Entities { get; }

    private protected bool AllowAll { get; private set; }

    private protected bool LogicDeleteDisabled { get; private set; }

    /// <summary>追加条件（多个之间是 AND）。</summary>
    public TSelf Where(Expression<Func<T, bool>> predicate)
    {
        _wheres.Add(predicate);
        return Self;
    }

    /// <summary><paramref name="condition"/> 为 true 时才追加条件。</summary>
    public TSelf WhereIf(bool condition, Expression<Func<T, bool>> predicate) => condition ? Where(predicate) : Self;

    /// <summary>按主键等值。</summary>
    public TSelf WhereById(object id) => Where(KeyPredicates.ById<T>(Entity, id));

    /// <summary>按主键 IN。</summary>
    public TSelf WhereByIds(IEnumerable ids) => Where(KeyPredicates.ByIds<T>(Entity, ids));

    /// <summary>明确允许没有条件的整表写入（否则会被拒绝，防止误操作）。</summary>
    public TSelf All()
    {
        AllowAll = true;
        return Self;
    }

    /// <summary>不加逻辑删除条件；删除时改为物理删除。</summary>
    public TSelf DisableLogicDelete()
    {
        LogicDeleteDisabled = true;
        return Self;
    }

    /// <summary>不做乐观锁版本检查（对应 Java 的 ignoreVersion）：条件里不加版本、SET 里不写新版本。</summary>
    public TSelf IgnoreVersion(bool ignore = true)
    {
        _ignoreVersion = ignore;
        return Self;
    }

    /// <summary>按条件写入时的当前版本（对应 Java 的 withVersion）：条件里加 <c>`version` = ?</c>，SET 里写下一个版本。按实体写入时不用它，版本取实体上的值。</summary>
    public TSelf WithVersion(object version) => WithVersion(true, version);

    /// <summary><paramref name="condition"/> 为 true 时才设置当前版本。</summary>
    public TSelf WithVersion(bool condition, object version)
    {
        if (!condition) return Self;
        ArgumentNullException.ThrowIfNull(version);
        _version = version;
        return Self;
    }

    /// <summary>执行，返回受影响的总行数。</summary>
    public abstract Task<long> ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 这次写入的版本列与当前版本：实体没有版本列或调用了 <see cref="IgnoreVersion"/> 时为 null。
    /// 按实体写入取实体上的值，按条件写入取 <see cref="WithVersion(object)"/>，后者没给时报错（与 Java 一致）。
    /// </summary>
    private protected (ColumnMetadata Column, object? Current)? VersionOf(T? entity)
    {
        if (Entity.VersionColumn is not { } column || _ignoreVersion) return null;
        if (entity is not null) return (column, column.Getter(entity));
        if (_version is null)
            throw new InvalidOperationException($"{typeof(T).Name} 有版本列 {column.PropertyName}：按条件更新 / 删除需要 WithVersion(当前版本)，或 IgnoreVersion() 不做版本检查");
        var type = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
        return (column, type.IsInstanceOfType(_version) ? _version : Convert.ChangeType(_version, type, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>SET 里写下一个版本：<c>`version` = ?</c>；返回新版本，供执行成功后写回实体。</summary>
    private protected object? AddVersionSet(UpdateStatement update, EntityTableSource table, T? entity)
    {
        if (VersionOf(entity) is not var (column, current)) return null;
        var next = Entity.NextVersion(current);
        update.Sets.Add((table.Column(column), ExpressionTranslator.Parameter(next, column.ClrType)));
        return next;
    }

    /// <summary>
    /// 执行，影响的行数不等于 <paramref name="expectedRows"/> 时抛出 <see cref="ConcurrencyException"/>（对应 Java 的 executeRows(expectRows, msg)）。
    /// 在事务里使用时，抛出异常会让事务回滚。
    /// </summary>
    public async Task<long> ExecuteAsync(long expectedRows, string? message = null, CancellationToken cancellationToken = default)
    {
        var rows = await ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return rows == expectedRows
            ? rows
            : throw new ConcurrencyException(message ?? $"{typeof(T).Name} 期望影响 {expectedRows} 行，实际 {rows} 行", expectedRows, rows);
    }

    /// <summary>按实体写入时定位这一行的条件，默认是主键等值。</summary>
    private protected virtual IEnumerable<SqlExpr> EntityPredicates(EntityTableSource table, T entity) => KeyPredicates.ForEntity(table, entity);

    /// <summary>
    /// 条件：逻辑删除（未关闭时）、乐观锁版本、拦截器过滤、按实体写入时该实体的定位条件、用户条件（顺序与 Java 一致）。
    /// 按条件写入且没有任何用户条件时，除非调用过 <see cref="All"/>，否则拒绝。
    /// </summary>
    private protected List<SqlExpr> BuildWhere(EntityTableSource table, T? entity, bool applyLogicDelete)
    {
        if (entity is null && _wheres.Count == 0 && !AllowAll)
            throw new InvalidOperationException($"{typeof(T).Name} 的更新 / 删除没有条件；确认要整表操作请调用 All()");

        var result = new List<SqlExpr>();
        if (applyLogicDelete && !LogicDeleteDisabled && QueryCompiler.LogicDeleteFilter(table) is { } ld) result.Add(ld);
        if (VersionOf(entity) is var (versionColumn, current))
            result.Add(new BinaryExpr(table.Column(versionColumn), SqlBinaryOperator.Equal, ExpressionTranslator.Parameter(current, versionColumn.ClrType)));
        result.AddRange(Db.Interceptors.QueryFilters(table, Interceptors));
        if (entity is not null) result.AddRange(EntityPredicates(table, entity));
        if (_wheres.Count == 0) return result;

        // 条件里的集合导航翻译成关联子查询，需要翻译上下文；对一导航要 JOIN 到目标表，UPDATE / DELETE 还不支持
        var scratch = new SelectQuery(table);
        var context = new TranslationContext(Db.Metadata,
            (t, logicDelete) => [.. (logicDelete && QueryCompiler.LogicDeleteFilter(t) is { } filter ? [filter] : Array.Empty<SqlExpr>()), .. Db.Interceptors.QueryFilters(t, Interceptors)],
            scratch) { Db = Db };
        foreach (var where in _wheres)
        {
            var translator = new ExpressionTranslator([]) { Context = context };
            translator.Bind(where, [new EntityBinding(table)]);
            result.Add(translator.TranslatePredicate(where.Body));
        }
        if (scratch.Joins.Count > 0)
            throw new NotSupportedException($"{typeof(T).Name} 的更新 / 删除条件里用到了对一导航（需要 JOIN），还不支持；请改写成子查询");
        return result;
    }
}
