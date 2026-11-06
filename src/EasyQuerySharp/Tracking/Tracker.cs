using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tracking;

/// <summary>一个被追踪的实体：当前实例，以及追踪时各列的数据库值快照。</summary>
internal sealed class TrackedEntry(EntityMetadata entity, object current)
{
    public EntityMetadata Entity { get; } = entity;

    public object Current { get; } = current;

    /// <summary>列 → 追踪（或上次写入成功）时的数据库值。</summary>
    public Dictionary<ColumnMetadata, object?> Original { get; private set; } = Snapshot(entity, current);

    /// <summary>与快照不同的列（主键除外）。</summary>
    public IEnumerable<ColumnMetadata> ChangedColumns() =>
        Entity.Columns.Where(c => !c.IsPrimaryKey && !Equals(Original[c], c.GetProviderValue(Current)));

    /// <summary>写入成功后以当前值为新快照。</summary>
    public void Refresh() => Original = Snapshot(Entity, Current);

    private static Dictionary<ColumnMetadata, object?> Snapshot(EntityMetadata entity, object instance) =>
        entity.Columns.ToDictionary(c => c, c => c.GetProviderValue(instance));
}

/// <summary>
/// 每个 <see cref="EasyDb"/> 一个，借 <see cref="AsyncLocal{T}"/> 让追踪状态跟随当前异步流（与环境事务同样的方式）。
/// 追踪键是"实体类型 + 主键值"；没有主键的类型（DTO 等）不追踪。
/// </summary>
internal sealed class Tracker(MetadataRegistry metadata)
{
    private readonly AsyncLocal<State?> _current = new();

    /// <summary>当前是否在追踪作用域里。</summary>
    public bool Active => _current.Value is { Depth: > 0 };

    public void Begin()
    {
        var state = _current.Value;
        if (state is null || state.Depth == 0) _current.Value = state = new State();
        lock (state) state.Depth++;
    }

    public void Release()
    {
        if (_current.Value is not { } state) return;
        lock (state)
        {
            if (state.Depth == 0 || --state.Depth != 0) return;
            state.Entries.Clear();
            state.Loaded.Clear();
        }
    }

    /// <summary>类型能否追踪：有主键的实体。</summary>
    public bool CanTrack(Type type) => type.IsClass && !ScalarTypes.IsScalar(type) && metadata.Get(type).KeyColumns.Count > 0;

    /// <summary>
    /// 显式开始追踪（对应 Java 的 addTracking）：不在作用域里返回 false；同一主键已追踪了另一个实例时抛出异常。
    /// </summary>
    public bool Add(object entity)
    {
        if (!Active) return false;
        return ReferenceEquals(Attach(entity), entity)
            ? true
            : throw new InvalidOperationException($"{entity.GetType().Name} 主键 {KeyOf(metadata.Get(entity.GetType()), entity)} 已追踪了另一个实例");
    }

    /// <summary>查询结果的追踪：同一主键已追踪时返回已追踪的实例（查询结果以它为准）。</summary>
    public object AttachQueried(object entity) => Attach(entity);

    public bool Remove(object entity)
    {
        if (_current.Value is not { Depth: > 0 } state) return false;
        lock (state) return state.Entries.Remove(KeyOf(metadata.Get(entity.GetType()), entity));
    }

    /// <summary>实体是否被追踪（按主键找到的正是这个实例）。</summary>
    public TrackedEntry? Find(object entity)
    {
        // 没有主键或主键还为 null（新对象）的不会被追踪
        if (_current.Value is not { Depth: > 0 } state || TrackKeyOf(entity) is not { } key) return null;
        lock (state) return state.Entries.TryGetValue(key, out var entry) && ReferenceEquals(entry.Current, entity) ? entry : null;
    }

    private object Attach(object entity)
    {
        var state = _current.Value ?? throw new InvalidOperationException("不在追踪作用域里");
        var meta = metadata.Get(entity.GetType());
        if (meta.KeyColumns.Count == 0) throw new InvalidOperationException($"{meta.Type.Name} 没有主键，不能追踪");
        var key = KeyOf(meta, entity);
        lock (state)
        {
            if (state.Entries.TryGetValue(key, out var existing)) return existing.Current;
            state.Entries[key] = new TrackedEntry(meta, entity);
            return entity;
        }
    }

    /// <summary>
    /// 记下追踪查询里加载过的导航：父实体 <paramref name="owner"/> 的导航 <paramref name="navigation"/> 加载时的子对象
    /// （对应 Java 的 entityState.getIncludes()；聚合保存据此算出孤儿，见 0004）。不在追踪作用域里时忽略。
    /// </summary>
    public void RecordLoaded(object owner, string navigation, IReadOnlyList<object> children)
    {
        if (_current.Value is not { Depth: > 0 } state) return;
        lock (state)
        {
            if (!state.Loaded.TryGetValue(owner, out var navigations)) state.Loaded[owner] = navigations = new(StringComparer.Ordinal);
            navigations[navigation] = children;
        }
    }

    /// <summary>父实体的导航是否在追踪查询里加载过；加载过时给出当时的子对象。</summary>
    public bool TryGetLoaded(object owner, string navigation, out IReadOnlyList<object> children)
    {
        children = [];
        if (_current.Value is not { Depth: > 0 } state) return false;
        lock (state) return state.Loaded.TryGetValue(owner, out var navigations) && navigations.TryGetValue(navigation, out children!);
    }

    /// <summary>父实体在追踪查询里加载过的导航名，按加载的先后（Java 按 include 的顺序处理导航）。</summary>
    public IReadOnlyList<string> LoadedNavigations(object owner)
    {
        if (_current.Value is not { Depth: > 0 } state) return [];
        lock (state) return state.Loaded.TryGetValue(owner, out var navigations) ? [.. navigations.Keys] : [];
    }

    /// <summary>追踪键（实体类型 + 主键值）；主键为 null 时返回 null。</summary>
    public string? TrackKeyOf(object entity)
    {
        var meta = metadata.Get(entity.GetType());
        return meta.KeyColumns.Count == 0 || meta.KeyColumns.Any(k => k.GetProviderValue(entity) is null) ? null : KeyOf(meta, entity);
    }

    /// <summary>按追踪键找已追踪的实体。</summary>
    public TrackedEntry? FindByKey(string key)
    {
        if (_current.Value is not { Depth: > 0 } state) return null;
        lock (state) return state.Entries.TryGetValue(key, out var entry) ? entry : null;
    }

    // 类型 + 各主键值；主键为 null 的实体没法追踪
    private static string KeyOf(EntityMetadata meta, object entity)
    {
        var values = meta.KeyColumns.Select(k => k.GetProviderValue(entity)
            ?? throw new InvalidOperationException($"{meta.Type.Name} 的主键 {k.PropertyName} 为 null，不能追踪")).ToList();
        return meta.Type.FullName + ":" + string.Join(",", values.Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)));
    }

    // 作用域里的并行查询（Task.WhenAll 等）共用同一个 State，读写都要加锁
    private sealed class State
    {
        public int Depth { get; set; }

        public Dictionary<string, TrackedEntry> Entries { get; } = new(StringComparer.Ordinal);

        /// <summary>父实体（按引用）→ 加载过的导航 → 当时的子对象。</summary>
        public Dictionary<object, Dictionary<string, IReadOnlyList<object>>> Loaded { get; } = new(ReferenceEqualityComparer.Instance);
    }
}
