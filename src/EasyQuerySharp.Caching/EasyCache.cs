using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using Microsoft.Extensions.Caching.Hybrid;

namespace EasyQuerySharp.Caching;

/// <summary>
/// 二级缓存（对应 Java 的 sql-cache，见 0007）：按主键（<see cref="CacheClient.Kv{T}"/>）与按条件的主键索引（<see cref="CacheClient.All{T}"/>）
/// 显式地缓存实体，存储是 <see cref="HybridCache"/>（本机层加可选的分布式层）。它也是写入监听器：经本库写入登记过的实体的表时失效，
/// 事务里在提交之后。通常整个应用一个实例（按数据库），用 <see cref="EasyDbOptionsCacheExtensions.UseCache"/> 接到 <see cref="EasyDb"/> 上。
/// </summary>
/// <example><code>
/// var cache = new EasyCache(hybridCache, o =&gt; o.Cache&lt;User&gt;().Cache&lt;Province&gt;());
/// var db = new EasyDb(o =&gt; o.UseMySql(dataSource).UseCache(cache));
/// var user = await cache.Of(db).Kv&lt;User&gt;().FindAsync(7);
/// var provinces = await cache.Of(db).All&lt;Province&gt;().OrderBy(p =&gt; p.Code).ToListAsync();
/// </code></example>
public sealed class EasyCache : IWriteListener
{
    private const string NotAotReady = "实体用 System.Text.Json 的反射序列化，不支持裁剪与 Native AOT（与核心一致，见 BACKLOG）";

    private readonly EasyCacheOptions _options;
    private readonly HashSet<Type> _entities;
    private readonly ConditionalWeakTable<EasyDb, DbState> _states = new();

    /// <summary>用 HybridCache 与配置函数创建；配置里用 <see cref="EasyCacheOptions.Cache{T}"/> 登记要缓存的实体。</summary>
    [RequiresUnreferencedCode(NotAotReady)]
    [RequiresDynamicCode(NotAotReady)]
    public EasyCache(HybridCache store, Action<EasyCacheOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(configure);
        _options = new EasyCacheOptions();
        configure(_options);
        _options.Validate();
        _entities = [.. _options.Entities];
        Store = new CacheStore(store, _options);
    }

    internal CacheStore Store { get; }

    /// <summary>
    /// 绑定一个库：缓存没命中时用它查库。这个库必须用 <c>options.UseCache(cache)</c> 接上了这个缓存（否则写入不会失效），没有时抛出异常。
    /// </summary>
    public CacheClient Of(EasyDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (!db.Options.WriteListeners.Contains(this))
            throw new InvalidOperationException("这个 EasyDb 没有接上缓存的失效：创建时调用 options.UseCache(cache)，否则经它的写入不会让缓存失效");
        // 分片实体的写入事件里是各分片的实际表名（t_topic_0），按逻辑表登记的缓存对不上，会一直读到旧值
        if (_entities.FirstOrDefault(t => db.GetShardRule(t) is not null) is { } sharded)
            throw new NotSupportedException($"{sharded.Name} 是分片实体：二级缓存不支持分片实体（写入落在各分片的实际表上，缓存的失效对不上）");
        return new CacheClient(this, db);
    }

    /// <summary>
    /// 写入登记过的实体所在的表时失效（0007 §7）。失效不因调用方取消而中断（写入已经提交，半途而废会留下旧值）；
    /// 配了 <see cref="EasyCacheOptions.LateEvictDelay"/> 时在后台隔一会儿再失效一次。
    /// </summary>
    async ValueTask IWriteListener.OnWrittenAsync(WriteEvent e, CancellationToken cancellationToken)
    {
        var state = State(e.Db);
        var table = TableId(e.Schema, e.Table);
        if (!state.Tables.TryGetValue(table, out var key)) return;
        var keys = KeyTexts(e, key);
        await Store.InvalidateAsync(table, e.Kind, keys, CancellationToken.None).ConfigureAwait(false);
        if (_options.LateEvictDelay is { } delay) _ = EvictLaterAsync(table, e.Kind, keys, delay);
    }

    internal bool IsCached(Type type) => _entities.Contains(type);

    internal DbState State(EasyDb db) => _states.GetValue(db, d => new DbState(this, d));

    internal static string TableId(string? schema, string table) => schema is null ? table : $"{schema}.{table}";

    internal static string TableId(EntityMetadata entity) => TableId(entity.Schema, entity.TableName);

    /// <summary>主键的文本：先经转换器变成数据库值，再按不变区域格式化；与 <see cref="KeyOf"/> 互逆。</summary>
    internal static string KeyText(ColumnMetadata key, object value)
    {
        var provider = key.Converter is { } converter ? converter.ToProvider(value) : value;
        return provider switch
        {
            null => throw new ArgumentNullException(nameof(value), $"{key} 的主键值不能为 null"),
            string s => s,
            DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => provider.ToString()!,
        };
    }

    /// <summary>主键文本还原成属性类型的值。</summary>
    internal static object KeyOf(ColumnMetadata key, string text) =>
        (key.Converter is { } converter
            ? converter.FromProvider(ValueReader.ConvertTo(text, converter.ProviderType))
            : ValueReader.ConvertTo(text, key.ClrType))!;

    /// <summary>用户给的主键（object）转成属性类型。</summary>
    internal static object ToKey(ColumnMetadata key, object? id) =>
        ValueReader.ConvertTo(id ?? throw new ArgumentNullException(nameof(id), "主键不能为 null"), key.ClrType)!;

    /// <summary>
    /// 变体键：实体类型全名 + 条件查询 SQL 与参数的 SHA-256 前 16 字节。条件含拦截器加的部分（租户等），不同条件各缓存各的。
    /// </summary>
    internal static string Variant(Type entity, SqlStatement statement)
    {
        var text = new StringBuilder(statement.Text);
        foreach (var p in statement.Parameters)
            text.Append('\n').Append(p.ClrType.FullName).Append('=').Append(p.Value switch
            {
                null => "null",
                byte[] bytes => Convert.ToBase64String(bytes),
                DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                var other => other.ToString(),
            });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return $"{entity.FullName}|{Convert.ToHexStringLower(hash.AsSpan(0, 16))}";
    }

    // 事件的主键换成文本；同表不同实体时按事件的实体（没有时按登记的实体）的主键列换算，换算不了当作主键不明
    private static List<string>? KeyTexts(WriteEvent e, ColumnMetadata tableKey)
    {
        if (e.Keys is null) return null;
        var key = e.EntityType is { } type && e.Db.Metadata.Get(type).KeyColumns is [var own] ? own : tableKey;
        try
        {
            return [.. e.Keys.Select(k => KeyText(key, k)).Distinct(StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException)
        {
            return null;
        }
    }

    // 延迟的第二次失效只是兜底，没有调用方可以报告失败，由过期时间兜底
    private async Task EvictLaterAsync(string table, WriteKind kind, List<string>? keys, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay).ConfigureAwait(false);
            await Store.InvalidateAsync(table, kind, keys, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 见上
        }
    }

    /// <summary>
    /// 一个库上的派生信息：登记的实体落在哪些表（表 → 主键列）、只序列化实体自己的列的 JSON 选项。
    /// 表名与列随库的命名约定变化，所以按库算。
    /// </summary>
    internal sealed class DbState
    {
        public DbState(EasyCache cache, EasyDb db)
        {
            Tables = new Dictionary<string, ColumnMetadata>(StringComparer.Ordinal);
            var columns = new Dictionary<Type, HashSet<string>>();
            foreach (var type in cache._entities)
            {
                var entity = db.Metadata.Get(type);
                if (entity.KeyColumns is [var key]) Tables.TryAdd(TableId(entity), key);
                // 实体自己的列：普通列是属性本身，值对象里的列取从实体出发的第一个属性
                columns[type] = entity.Columns.Select(c => c.OwnerPath.Count > 0 ? c.OwnerPath[0].Name : c.Property.Name).ToHashSet(StringComparer.Ordinal);
            }
            var json = cache._options.Json ?? JsonSerializerOptions.Default;
            Json = new JsonSerializerOptions(json)
            {
                TypeInfoResolver = (json.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(info =>
                {
                    if (info.Kind != JsonTypeInfoKind.Object || !columns.TryGetValue(info.Type, out var keep)) return;
                    for (var i = info.Properties.Count - 1; i >= 0; i--)
                        if (info.Properties[i].AttributeProvider is not MemberInfo member || !keep.Contains(member.Name))
                            info.Properties.RemoveAt(i);
                }),
            };
        }

        /// <summary>登记的实体所在的表（schema.表）→ 主键列；同表的几个实体取第一个登记的。复合主键的实体不在里面。</summary>
        public Dictionary<string, ColumnMetadata> Tables { get; }

        public JsonSerializerOptions Json { get; }
    }
}

/// <summary><see cref="EasyDbOptions"/> 上接入二级缓存。</summary>
public static class EasyDbOptionsCacheExtensions
{
    /// <summary>把缓存登记为写入监听器：经这个库写入缓存的实体时失效（事务里在提交之后）。</summary>
    public static EasyDbOptions UseCache(this EasyDbOptions options, EasyCache cache)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cache);
        return options.AddWriteListener(cache);
    }
}
