using System.Collections;
using System.Collections.Immutable;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 更新、删除的公共部分：条件、关闭逻辑删除或拦截器、无条件写入的保护。
/// 写入命令是一次性的可变构建器（与不可变的查询不同），链式调用返回自身。
/// </summary>
public abstract class WriteCommandBase<TSelf, T>
    where TSelf : WriteCommandBase<TSelf, T>
    where T : class
{
    private readonly List<LambdaExpression> _wheres = [];

    private protected WriteCommandBase(EasyDb db, IReadOnlyList<T>? entities)
    {
        Db = db;
        Entities = entities;
        Entity = db.Metadata.Get<T>();
    }

    /// <summary>所属的 <see cref="EasyDb"/>。</summary>
    public EasyDb Db { get; }

    private protected EntityMetadata Entity { get; }

    // 按实体写入时的实体；按条件写入时为 null
    private protected IReadOnlyList<T>? Entities { get; }

    private protected bool AllowAll { get; private set; }

    private protected bool LogicDeleteDisabled { get; private set; }

    private protected ImmutableHashSet<string> DisabledInterceptors { get; private set; } = [];

    private protected string? TableName { get; private set; }

    private TSelf Self => (TSelf)this;

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

    /// <summary>不应用名为 <paramref name="name"/> 的拦截器。</summary>
    public TSelf DisableInterceptor(string name)
    {
        DisabledInterceptors = DisabledInterceptors.Add(name);
        return Self;
    }

    /// <summary>不应用任何拦截器。</summary>
    public TSelf DisableInterceptors() => DisableInterceptor("*");

    /// <summary>写入到另一张同结构的表（对应 Java 的 asTable）。</summary>
    public TSelf AsTable(string tableName)
    {
        TableName = tableName;
        return Self;
    }

    private protected EntityTableSource NewTable() => new(Entity) { TableNameOverride = TableName };

    /// <summary>按实体写入时定位这一行的条件，默认是主键等值。</summary>
    private protected virtual IEnumerable<SqlExpr> EntityPredicates(EntityTableSource table, T entity) => KeyPredicates.ForEntity(table, entity);

    /// <summary>
    /// 条件：逻辑删除（未关闭时）、拦截器过滤、用户条件；按实体写入时再加该实体的主键条件。
    /// 按条件写入且没有任何用户条件时，除非调用过 <see cref="All"/>，否则拒绝。
    /// </summary>
    private protected List<SqlExpr> BuildWhere(EntityTableSource table, T? entity, bool applyLogicDelete)
    {
        if (entity is null && _wheres.Count == 0 && !AllowAll)
            throw new InvalidOperationException($"{typeof(T).Name} 的更新 / 删除没有条件；确认要整表操作请调用 All()");

        var result = new List<SqlExpr>();
        if (applyLogicDelete && !LogicDeleteDisabled && QueryCompiler.LogicDeleteFilter(table) is { } ld) result.Add(ld);
        result.AddRange(Db.Interceptors.QueryFilters(table, DisabledInterceptors));
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
