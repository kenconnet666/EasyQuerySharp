namespace EasyQuerySharp.Execution;

/// <summary>
/// 写入监听（对应 Java 的 TriggerEvent / addTriggerListener，见 0007）：经本库成功写入后调用，用于让缓存失效、发领域事件等。
/// 一次执行（一个 <c>ExecuteAsync</c>、一次聚合保存）里同一张表的写入合并成一个事件；在环境事务里推迟到提交成功之后，回滚不调用。
/// 不覆盖原生 SQL（<c>ExecuteSqlAsync</c>）、触发器与级联、别的服务的写入。
/// </summary>
/// <example><code>
/// var db = new EasyDb(o =&gt; o.UseMySql(dataSource).AddWriteListener(new AuditListener()));
/// </code></example>
public interface IWriteListener
{
    /// <summary>
    /// 写入成功后调用；在事务里推迟到提交成功之后，回滚不调用。<paramref name="cancellationToken"/> 是这次写入调用方的令牌。
    /// 抛出的异常包成 <see cref="EasyQueryException"/> 交给调用方（这时数据库已经写入），不会吞掉。
    /// </summary>
    ValueTask OnWrittenAsync(WriteEvent e, CancellationToken cancellationToken);
}

/// <summary>写入的种类，可以组合：upsert 是 <see cref="Insert"/> | <see cref="Update"/>，逻辑删除是 <see cref="Update"/>。</summary>
[Flags]
public enum WriteKind
{
    /// <summary>INSERT（含 INSERT … SELECT）。</summary>
    Insert = 1,

    /// <summary>UPDATE（含逻辑删除）。</summary>
    Update = 2,

    /// <summary>物理 DELETE。</summary>
    Delete = 4,
}

/// <summary>一张表的一组写入，交给 <see cref="IWriteListener"/>。</summary>
public sealed class WriteEvent
{
    internal WriteEvent(EasyDb db, string? schema, string table, Type? entityType, WriteKind kind, IReadOnlyList<object>? keys)
    {
        Db = db;
        Schema = schema;
        Table = table;
        EntityType = entityType;
        Kind = kind;
        Keys = keys;
    }

    /// <summary>所属的 <see cref="EasyDb"/>。</summary>
    public EasyDb Db { get; }

    /// <summary>实际写入的 schema；没有时为 null。</summary>
    public string? Schema { get; }

    /// <summary>实际写入的表（<c>AsTable</c> 之后的表名）。</summary>
    public string Table { get; }

    /// <summary>写入的实体类型；按字典写入（<c>InsertMap</c> / <c>UpdateMap</c>），或同一张表由不同实体写入时为 null。</summary>
    public Type? EntityType { get; }

    /// <summary>这一组写入的种类（各条语句的并集）。</summary>
    public WriteKind Kind { get; }

    /// <summary>
    /// 受影响行的主键（属性类型，经值转换器还原），去重；可能多于实际受影响的行（条件里还有别的限制时）。
    /// 不能确定时为 null：自增主键的插入、INSERT … SELECT、条件里没有单列主键的 <c>=</c> / <c>IN</c>、改了主键、复合主键、
    /// 冲突时可能更新主键以外的行的 upsert，这组里只要有一条不能确定就是 null。
    /// </summary>
    public IReadOnlyList<object>? Keys { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Kind} {(Schema is null ? "" : Schema + ".")}{Table} keys={(Keys is null ? "?" : string.Join(",", Keys))}";
}
