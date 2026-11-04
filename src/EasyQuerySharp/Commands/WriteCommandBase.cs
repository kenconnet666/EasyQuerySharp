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

    /// <summary>执行，返回受影响的总行数。</summary>
    public abstract Task<long> ExecuteAsync(CancellationToken cancellationToken = default);

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
    /// 条件：逻辑删除（未关闭时）、拦截器过滤、按实体写入时该实体的定位条件、用户条件。
    /// 按条件写入且没有任何用户条件时，除非调用过 <see cref="All"/>，否则拒绝。
    /// </summary>
    private protected List<SqlExpr> BuildWhere(EntityTableSource table, T? entity, bool applyLogicDelete)
    {
        if (entity is null && _wheres.Count == 0 && !AllowAll)
            throw new InvalidOperationException($"{typeof(T).Name} 的更新 / 删除没有条件；确认要整表操作请调用 All()");

        var result = new List<SqlExpr>();
        if (applyLogicDelete && !LogicDeleteDisabled && QueryCompiler.LogicDeleteFilter(table) is { } ld) result.Add(ld);
        result.AddRange(Db.Interceptors.QueryFilters(table, Interceptors));
        if (entity is not null) result.AddRange(EntityPredicates(table, entity));
        foreach (var where in _wheres)
        {
            var translator = new ExpressionTranslator([]);
            translator.Bind(where, [new EntityBinding(table)]);
            result.Add(translator.TranslatePredicate(where.Body));
        }
        return result;
    }
}
