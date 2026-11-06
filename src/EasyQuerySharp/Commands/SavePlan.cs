using System.Collections;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 一次聚合保存的计划（规则见 0004，对应 Java 的 AutoTrackSaveProvider + BasicSaveCommand）：先按对象图与追踪快照逐层记下
/// 每条导航要插入、更新、删除、改外键的对象，再按"删除从深到浅 → 根 → 插入与更新从浅到深"的顺序执行。
/// </summary>
internal sealed class SavePlan(EasyDb db, SaveBehavior behavior, List<HashSet<string>>? paths, Type rootType)
{
    private readonly List<object> _rootInserts = [];
    private readonly List<object> _rootUpdates = [];
    private readonly List<object> _rootDeletes = [];
    private readonly HashSet<object> _roots = new(ReferenceEqualityComparer.Instance);

    // 第 i 项是深度 i+1 的各导航（按首次出现的顺序）
    private readonly List<Dictionary<NavigationMetadata, NavigationWork>> _levels = [];

    // 级联 Delete 的孤儿（对象、所在导航、深度）：整张图规划完再处理——同一实例挂到了别处的改成外键更新（Java 的 CHANGE），其余删除
    private readonly List<(object Orphan, NavigationWork Work, int Depth)> _orphans = [];

    // 插入的子对象 → 所在导航（被确认为"从别处移过来"时改成外键更新）
    private readonly Dictionary<object, NavigationWork> _inserted = new(ReferenceEqualityComparer.Instance);

    // RemoveRoot 的根：它已加载的子对象在规划完后按级联删除
    private readonly List<object> _removedRoots = [];

    // 留在原父对象上的子对象：同一实例不能再挂到别处
    private readonly HashSet<object> _kept = new(ReferenceEqualityComparer.Instance);

    private void Keep(object child)
    {
        if (_inserted.ContainsKey(child))
            throw new InvalidOperationException($"{child.GetType().Name} 的同一个对象既挂到了新的父对象，又留在原来的父对象上：一个子对象只能属于一个父对象");
        _kept.Add(child);
    }

    private bool Has(SaveBehavior flag) => (behavior & flag) != 0;

    public void AddRoot(object root, bool remove)
    {
        if (!_roots.Add(root)) throw new InvalidOperationException($"{root.GetType().Name} 的同一个对象在这次保存里出现了两次");
        var tracked = db.Tracker.Find(root) is not null;
        if (remove)
        {
            if (!tracked) throw new InvalidOperationException($"要删除的根 {root.GetType().Name} 没有被追踪：先用追踪查询加载它");
            _removedRoots.Add(root);
            _rootDeletes.Add(root);
            return;
        }
        if (!Has(SaveBehavior.IgnoreRoot))
        {
            if (!tracked) _rootInserts.Add(root);
            else if (!Has(SaveBehavior.IgnoreRootUpdate)) _rootUpdates.Add(root);
        }
        SaveChildren(root, tracked, depth: 1);
    }

    // ---------- 规划 ----------

    // 一个对象的各条导航：已追踪的只看追踪查询里加载过的导航，新对象看全部导航
    private void SaveChildren(object entity, bool tracked, int depth)
    {
        var meta = db.Metadata.Get(entity.GetType());
        foreach (var name in NavigationOrder(entity, meta, tracked))
        {
            var nav = db.Metadata.FindNavigation(meta, name)!;
            var kind = KindOf(nav, meta);
            if (kind == NavigationKind.Other) continue;
            var value = nav.Property.GetValue(entity);
            // 防环：根以下的对象不再沿回到根类型的导航保存（Java 的 circulateCheck，根自己的这类导航照常保存）；
            // 里面有新对象时明确报错，不悄悄丢掉
            if (nav.Target.Type == rootType && depth > 1)
            {
                if (Items(value).Any(child => db.Tracker.Find(child) is null))
                    throw new NotSupportedException($"{meta.Type.Name}.{name} 回到聚合根的类型 {rootType.Name}，只有根上的这条导航随聚合保存：更深层的新对象要单独保存");
                continue;
            }
            if (paths is not null)
            {
                if (depth > paths.Count || !paths[depth - 1].Contains(name)) continue;
                if (kind != NavigationKind.ValueObject)
                    throw new InvalidOperationException($"SavePath 里的 {meta.Type.Name}.{name} 是别的聚合根（{kind}），只能列出聚合内的导航");
            }
            IReadOnlyList<object> original = [];
            if (tracked && !db.Tracker.TryGetLoaded(entity, name, out original))
            {
                // 已追踪的对象上没加载过的导航：值来源不明，不能当成差异（Java 的"导航没有被追踪"）；
                // SavePath 明确列出的导航当作原来没有子对象处理
                if (paths is null)
                {
                    if (HasValue(value)) throw new InvalidOperationException($"导航 {meta.Type.Name}.{name} 没有被追踪：在追踪查询里 Include 它再保存，或用 SavePath 只保存列出的导航");
                    continue;
                }
                original = [];
            }
            if (nav.DirectPath.Count > 0)
            {
                if (HasValue(value)) throw new NotSupportedException($"聚合保存不支持直达导航 {meta.Type.Name}.{name}");
                continue;
            }
            if (kind == NavigationKind.AggregateRoot) SaveReference(entity, nav, value, tracked);
            else if (nav.Mapping is not null) { if (!(tracked && Skipped(nav, value))) SaveMappings(entity, nav, Items(value), original, depth); }
            else SaveOwned(entity, nav, value, original, tracked, depth);
        }
    }

    // 已追踪的对象按加载导航的先后（与 Java 按 include 的顺序一致），没加载的排在后面（只用来检查）；新对象按声明顺序
    private IEnumerable<string> NavigationOrder(object entity, EntityMetadata meta, bool tracked)
    {
        if (!tracked) return meta.NavigationProperties.Keys;
        var loaded = db.Tracker.LoadedNavigations(entity);
        return loaded.Concat(meta.NavigationProperties.Keys.Except(loaded, StringComparer.Ordinal));
    }

    // 多对一（别的聚合根）：从不级联，只把目标的键回填到本对象的外键
    private static void SaveReference(object entity, NavigationMetadata nav, object? target, bool tracked)
    {
        if (target is not null)
        {
            for (var i = 0; i < nav.SelfColumns.Count; i++)
            {
                var value = nav.TargetColumns[i].Property.GetValue(target)
                            ?? throw new InvalidOperationException($"{nav} 的目标 {nav.Target.Type.Name}.{nav.TargetColumns[i].PropertyName} 为 null：引用的对象要先保存");
                nav.SelfColumns[i].Property.SetValue(entity, value);
            }
            return;
        }
        if (!tracked) return;
        switch (nav.Cascade)
        {
            case Cascade.Auto or Cascade.SetNull:
                foreach (var column in nav.SelfColumns) column.Property.SetValue(entity, null);
                break;
            case Cascade.Delete:
                throw new InvalidOperationException($"{nav} 的级联是 Delete，不允许从当前聚合根上断开");
        }
    }

    // 一对一、一对多的子对象：按主键（或 [SaveKey]）与加载时的子对象配对
    private void SaveOwned(object parent, NavigationMetadata nav, object? value, IReadOnlyList<object> original, bool parentTracked, int depth)
    {
        if (nav.Cascade == Cascade.NoAction) return;
        if (parentTracked && Skipped(nav, value)) return;
        var current = Items(value);

        var work = Work(depth, nav);
        var originalByKey = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var child in original)
            if (db.Tracker.TrackKeyOf(child) is { } key) originalByKey[key] = child;
        var matched = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var item in current)
        {
            var child = Canonical(item);
            var existing = Match(child, original, originalByKey);
            if (existing is not null)
            {
                // 两个新对象配上了同一个原对象（业务键相同）：分不清该保留哪个
                if (!matched.Add(existing))
                    throw new InvalidOperationException($"{nav} 里有多个对象的 [SaveKey] 相同，配到了同一个原有对象");
                Keep(existing);
                if (nav.Cascade != Cascade.Delete) continue;
                // 新建的同主键对象：把值合并到已追踪的实例上，按快照差异更新
                if (!ReferenceEquals(existing, child)) Merge(child, existing);
                work.Updates.Add(existing);
                SaveChildren(existing, tracked: true, depth + 1);
                continue;
            }
            Attach(parent, nav, child, work, depth);
        }

        foreach (var orphan in original)
        {
            if (matched.Contains(orphan)) continue;
            if (nav.Cascade == Cascade.Delete) _orphans.Add((orphan, work, depth));
            else
            {
                if (nav.TargetColumns.Any(c => c.IsPrimaryKey))
                    throw new InvalidOperationException($"{nav} 的目标列含主键，级联 {nav.Cascade} 不能把外键置 NULL");
                work.SetNulls.Add(orphan);
            }
        }
    }

    // 已追踪父对象上的 null 导航（IgnoreNull）、空集合（IgnoreEmpty）按开关跳过，不把原来的子对象当孤儿
    private bool Skipped(NavigationMetadata nav, object? value) =>
        value is null ? Has(SaveBehavior.IgnoreNull) : nav.IsCollection && !HasValue(value) && Has(SaveBehavior.IgnoreEmpty);

    // 挂上一个原来不在这条导航里的子对象：Delete 级联时插入（或是从别处移过来的改外键），引用时只改外键
    private void Attach(object parent, NavigationMetadata nav, object child, NavigationWork work, int depth)
    {
        var tracked = db.Tracker.Find(child) is not null;
        CheckOwnership(parent, nav, child);
        if (nav.Cascade != Cascade.Delete)
        {
            if (!tracked) throw new InvalidOperationException($"{nav} 是引用（级联 {nav.Cascade}），挂上的 {child.GetType().Name} 要先被追踪");
            work.ForeignKeyUpdates.Add((child, parent));
            return;
        }
        if (_kept.Contains(child))
            throw new InvalidOperationException($"{child.GetType().Name} 的同一个对象既留在原来的父对象上，又挂到了 {nav}：一个子对象只能属于一个父对象");
        work.Inserts.Add((child, parent));
        _inserted[child] = work;
        SaveChildren(child, tracked, depth + 1);
    }

    // 子对象的外键已指向别的父对象：默认报错，AllowOwnershipChange 时允许改写
    private void CheckOwnership(object parent, NavigationMetadata nav, object child)
    {
        if (Has(SaveBehavior.AllowOwnershipChange)) return;
        for (var i = 0; i < nav.TargetColumns.Count; i++)
        {
            var current = nav.TargetColumns[i].Property.GetValue(child);
            var should = nav.SelfColumns[i].Property.GetValue(parent);
            if (current is not null && should is not null && !Equals(current, should))
                throw new InvalidOperationException(
                    $"{child.GetType().Name}.{nav.TargetColumns[i].PropertyName} 的值 {current} 不等于 {nav}，应为 {should}：要把子对象挂到别的父对象，请加 SaveBehavior.AllowOwnershipChange");
        }
    }

    // 多对多：目标是别的聚合根，只维护中间表行；目标要已经存在
    private void SaveMappings(object parent, NavigationMetadata nav, List<object> current, IReadOnlyList<object> original, int depth)
    {
        if (nav.Cascade == Cascade.NoAction) return;
        var work = Work(depth, nav);
        var originalKeys = original.Select(TargetKey).ToHashSet(StringComparer.Ordinal);
        var currentKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in current)
        {
            var key = TargetKey(target);
            currentKeys.Add(key);
            if (!originalKeys.Contains(key) && nav.Cascade == Cascade.Delete) work.MappingInserts.Add((parent, target));
        }
        foreach (var target in original)
            if (!currentKeys.Contains(TargetKey(target)))
                (nav.Cascade == Cascade.Delete ? work.MappingDeletes : work.MappingSetNulls).Add((parent, target));

        string TargetKey(object target) => string.Join("|", nav.TargetColumns.Select(c => c.Property.GetValue(target)
            ?? throw new InvalidOperationException($"{nav} 的目标 {target.GetType().Name} 还没有主键：要先保存它再建立关系")));
    }

    // 删除一个对象时，它已加载的、级联 Delete 的子对象一并删除（从深到浅执行）
    private void DeleteChildren(object entity, int depth)
    {
        var meta = db.Metadata.Get(entity.GetType());
        foreach (var name in db.Tracker.LoadedNavigations(entity))
        {
            var nav = db.Metadata.FindNavigation(meta, name)!;
            if (KindOf(nav, meta) != NavigationKind.ValueObject || nav.Cascade != Cascade.Delete || nav.DirectPath.Count > 0) continue;
            if (!db.Tracker.TryGetLoaded(entity, name, out var loaded)) continue;
            var work = Work(depth, nav);
            foreach (var child in loaded)
            {
                if (nav.Mapping is not null) work.MappingDeletes.Add((entity, child));
                // 挂到了别处的子对象不删，改成外键更新
                else if (!MovedElsewhere(child)) Delete(child, work, depth);
            }
        }
    }

    // 主键匹配；主键为空时按 [SaveKey] 的业务键匹配
    private object? Match(object child, IReadOnlyList<object> original, Dictionary<string, object> originalByKey)
    {
        if (db.Tracker.TrackKeyOf(child) is { } key) return originalByKey.GetValueOrDefault(key);
        var saveKeys = db.Metadata.Get(child.GetType()).Columns.Where(c => c.Property.IsDefined(typeof(SaveKeyAttribute), inherit: true)).ToList();
        if (saveKeys.Count == 0) return null;
        var values = saveKeys.Select(c => c.Property.GetValue(child) ?? throw new InvalidOperationException($"{child.GetType().Name} 的业务键 {c.PropertyName} 为 null")).ToList();
        var found = original.Where(o => saveKeys.Select((c, i) => Equals(c.Property.GetValue(o), values[i])).All(x => x)).ToList();
        return found.Count switch
        {
            0 => null,
            1 => found[0],
            _ => throw new InvalidOperationException($"{child.GetType().Name} 有多个对象的 [SaveKey] 相同"),
        };
    }

    // 新建的对象沿用了已追踪实体的主键（可能来自聚合里的任何位置）：换成那个已追踪的实例，
    // 于是它按快照差异更新、挂到了别处时改外键，而不是再插入一次（与 Java 按主键认对象一致）
    private object Canonical(object child)
    {
        if (db.Tracker.Find(child) is not null || db.Tracker.TrackKeyOf(child) is not { } key || db.Tracker.FindByKey(key) is not { } entry)
            return child;
        Merge(child, entry.Current);
        return entry.Current;
    }

    // 新建的对象（同主键或同业务键）的列值与导航合并到已追踪的实例上：新对象代表这个实体现在的完整状态
    private void Merge(object from, object into)
    {
        var meta = db.Metadata.Get(into.GetType());
        foreach (var column in meta.Columns)
            if (!column.IsPrimaryKey) column.Property.SetValue(into, column.Property.GetValue(from));
        foreach (var (property, _) in meta.NavigationProperties.Values)
            property.SetValue(into, property.GetValue(from));
    }

    private NavigationWork Work(int depth, NavigationMetadata nav)
    {
        while (_levels.Count < depth) _levels.Add([]);
        var level = _levels[depth - 1];
        if (!level.TryGetValue(nav, out var work)) level[nav] = work = new NavigationWork(nav);
        return work;
    }

    // ---------- 执行 ----------

    public async Task ExecuteAsync(CancellationToken ct)
    {
        ResolveOrphans();
        // 1. 删除：从最深一层往上
        for (var i = _levels.Count - 1; i >= 0; i--)
            foreach (var work in _levels[i].Values)
            {
                foreach (var child in work.Deletes) await EntityWrites.DeleteAsync(db, child, Has(SaveBehavior.PhysicalDelete), ct).ConfigureAwait(false);
                foreach (var (parent, target) in work.MappingDeletes) await EntityWrites.DeleteMappingAsync(db, work.Navigation, parent, target, setNull: false, ct).ConfigureAwait(false);
                foreach (var (parent, target) in work.MappingSetNulls) await EntityWrites.DeleteMappingAsync(db, work.Navigation, parent, target, setNull: true, ct).ConfigureAwait(false);
            }
        // 2. 根
        foreach (var root in _rootDeletes) await EntityWrites.DeleteAsync(db, root, Has(SaveBehavior.PhysicalDelete), ct).ConfigureAwait(false);
        foreach (var root in _rootInserts) await EntityWrites.InsertAsync(db, root, ct).ConfigureAwait(false);
        foreach (var root in _rootUpdates) await EntityWrites.UpdateAsync(db, root, ct).ConfigureAwait(false);
        // 3. 插入与更新：从浅到深，插入前才回填父对象的键（于是孙辈拿得到子对象的生成键）
        foreach (var level in _levels)
            foreach (var work in level.Values)
            {
                var nav = work.Navigation;
                foreach (var (child, parent) in work.Inserts)
                {
                    CopyKey(nav, parent, child);
                    await EntityWrites.InsertAsync(db, child, ct).ConfigureAwait(false);
                }
                foreach (var (parent, target) in work.MappingInserts) await EntityWrites.InsertMappingAsync(db, nav, parent, target, ct).ConfigureAwait(false);
                foreach (var orphan in work.SetNulls)
                {
                    foreach (var column in nav.TargetColumns) column.Property.SetValue(orphan, null);
                    await EntityWrites.UpdateAsync(db, orphan, ct).ConfigureAwait(false);
                }
                foreach (var (child, parent) in work.ForeignKeyUpdates)
                {
                    CopyKey(nav, parent, child);
                    await EntityWrites.UpdateAsync(db, child, ct).ConfigureAwait(false);
                }
                foreach (var child in work.Updates) await EntityWrites.UpdateAsync(db, child, ct).ConfigureAwait(false);
            }
    }

    // 整张图规划完后处理孤儿：同一实例既从 A 拿掉又挂到 B 的，按插入的先后改成外键更新；其余删除，连同它已加载的子对象
    private void ResolveOrphans()
    {
        var orphans = _orphans.Select(o => o.Orphan).ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var level in _levels)
            foreach (var work in level.Values)
                for (var i = 0; i < work.Inserts.Count; i++)
                {
                    var (child, parent) = work.Inserts[i];
                    if (!orphans.Remove(child)) continue;
                    work.Inserts.RemoveAt(i--);
                    _inserted.Remove(child);
                    work.ForeignKeyUpdates.Add((child, parent));
                }
        foreach (var (orphan, work, depth) in _orphans)
            if (orphans.Contains(orphan)) Delete(orphan, work, depth);
        foreach (var root in _removedRoots) DeleteChildren(root, depth: 1);
    }

    private void Delete(object entity, NavigationWork work, int depth)
    {
        work.Deletes.Add(entity);
        DeleteChildren(entity, depth + 1);
    }

    // 被删的子树里有对象挂到了别处：把那次插入改成外键更新
    private bool MovedElsewhere(object child)
    {
        if (!_inserted.Remove(child, out var work)) return false;
        var index = work.Inserts.FindIndex(i => ReferenceEquals(i.Child, child));
        var parent = work.Inserts[index].Parent;
        work.Inserts.RemoveAt(index);
        work.ForeignKeyUpdates.Add((child, parent));
        return true;
    }

    private static void CopyKey(NavigationMetadata nav, object parent, object child)
    {
        for (var i = 0; i < nav.SelfColumns.Count; i++)
            nav.TargetColumns[i].Property.SetValue(child, nav.SelfColumns[i].Property.GetValue(parent)
                ?? throw new InvalidOperationException($"{nav} 的父对象键 {nav.SelfColumns[i].PropertyName} 为 null，无法回填子对象的外键"));
    }

    // ---------- 分类 ----------

    private enum NavigationKind
    {
        ValueObject,
        AggregateRoot,
        Other,
    }

    // 值对象：自身列是主键（一对一、一对多、有中间表的多对多）；聚合根：目标列是主键（多对一）；其余不处理（与 Java 的 getTargetValueType 一致）
    private static NavigationKind KindOf(NavigationMetadata nav, EntityMetadata self)
    {
        var selfIsKey = SameColumns(nav.SelfColumns, self.KeyColumns);
        if (nav.Relation == RelationType.ManyToMany) return nav.Mapping is not null && selfIsKey ? NavigationKind.ValueObject : NavigationKind.Other;
        if (selfIsKey) return NavigationKind.ValueObject;
        return SameColumns(nav.TargetColumns, nav.Target.KeyColumns) ? NavigationKind.AggregateRoot : NavigationKind.Other;
    }

    private static bool SameColumns(IReadOnlyList<ColumnMetadata> a, IReadOnlyList<ColumnMetadata> b) =>
        a.Count > 0 && a.Count == b.Count && a.All(b.Contains);

    private static bool HasValue(object? value) => value is not null && (value is not IEnumerable items || value is string || items.Cast<object>().Any());

    private static List<object> Items(object? value) => value switch
    {
        null => [],
        IEnumerable items and not string => items.Cast<object>().Where(x => x is not null).ToList(),
        _ => [value],
    };

    /// <summary>一条导航在一层里要做的事。</summary>
    private sealed class NavigationWork(NavigationMetadata navigation)
    {
        public NavigationMetadata Navigation { get; } = navigation;

        public List<(object Child, object Parent)> Inserts { get; } = [];

        public List<object> Updates { get; } = [];

        public List<(object Child, object Parent)> ForeignKeyUpdates { get; } = [];

        public List<object> Deletes { get; } = [];

        public List<object> SetNulls { get; } = [];

        public List<(object Parent, object Target)> MappingInserts { get; } = [];

        public List<(object Parent, object Target)> MappingDeletes { get; } = [];

        public List<(object Parent, object Target)> MappingSetNulls { get; } = [];
    }
}
