using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

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
