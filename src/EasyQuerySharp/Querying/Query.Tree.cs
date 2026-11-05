using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;

namespace EasyQuerySharp.Querying;

/// <summary>树查询（AsTreeCte）的选项，对应 Java 的 TreeCTEConfigurer。</summary>
public sealed class TreeCteOptions
{
    /// <summary>往上走（找全部上级）；默认往下走（找全部下级）。</summary>
    public bool Up { get; set; }

    /// <summary>递归部分用 UNION ALL（默认）；false 时用 UNION（去重，数据里有环也会停下）。</summary>
    public bool UnionAll { get; set; } = true;

    /// <summary>CTE 的名字，默认 <c>as_tree_cte</c>。</summary>
    public string CteTableName { get; set; } = "as_tree_cte";

    /// <summary>层级列的名字（起点是 0），默认 <c>cte_deep</c>。</summary>
    public string DeepColumnName { get; set; } = "cte_deep";

    /// <summary>只取到第几层（<c>WHERE 层级 &lt;= n</c>）；为 null 时不限制。</summary>
    public int? LimitDeep { get; set; }
}

// 树：递归 CTE 取全部下级 / 上级（AsTreeCte），读出后按父子键拼成树（ToTreeAsync）
public sealed partial class Query<T>
{
    /// <summary>
    /// 从当前查询的行出发，沿实体上指向自身的集合导航（如 <c>Children</c>）递归取全部下级（<c>Up</c> 时取全部上级），
    /// 对应 Java 的 asTreeCTE。之后照常 Where / Select / ToListAsync，或 <see cref="ToTreeAsync"/> 拼成树；
    /// <paramref name="childFilter"/> 只作用于递归走到的行（Java 的 setChildFilter）。
    /// <code>await db.Query&lt;Comment&gt;().Where(c =&gt; c.ParentId == "0").AsTreeCte(o =&gt; o.LimitDeep = 2).ToTreeAsync();</code>
    /// </summary>
    public Query<T> AsTreeCte(Action<TreeCteOptions>? configure = null, Expression<Func<T, bool>>? childFilter = null)
    {
        var nav = TreeNavigation(Db.Metadata, Db.Metadata.Get<T>())
                  ?? throw new InvalidOperationException($"{typeof(T).Name} 上没有指向自身的集合导航（如 List<{typeof(T).Name}> Children），AsTreeCte 需要它确定父子键；也可以用 AsTreeCte(self, parent) 指定");
        return Tree(nav.SelfColumns, nav.TargetColumns, configure, childFilter);
    }

    /// <summary>按指定的节点键 <paramref name="self"/> 与上级键 <paramref name="parent"/> 递归（对应 Java 的 asTreeCTECustom）。</summary>
    public Query<T> AsTreeCte<TKey>(Expression<Func<T, TKey>> self, Expression<Func<T, TKey>> parent, Action<TreeCteOptions>? configure = null,
        Expression<Func<T, bool>>? childFilter = null)
    {
        var entity = Db.Metadata.Get<T>();
        return Tree([entity.GetColumn(MemberName(self))], [entity.GetColumn(MemberName(parent))], configure, childFilter);
    }

    private Query<T> Tree(IReadOnlyList<ColumnMetadata> self, IReadOnlyList<ColumnMetadata> parent, Action<TreeCteOptions>? configure, LambdaExpression? childFilter)
    {
        var options = new TreeCteOptions();
        configure?.Invoke(options);
        // 外层沿用起点查询的拦截器选择与逻辑删除开关（Java 的外层从起点查询的上下文扩展而来）
        var start = Open;
        return new(new QueryState(Db, [new TreeCteSource(start, self, parent, options, childFilter)])
        {
            Interceptors = start.Interceptors, DisableLogicDelete = start.DisableLogicDelete,
        });
    }

    private static string MemberName(LambdaExpression key) =>
        Translation.ExpressionTranslator.StripConvert(key.Body) is MemberExpression { Expression: ParameterExpression } m
            ? m.Member.Name
            : throw new ArgumentException($"树的键需要直接的属性，如 c => c.ParentId：{key}", nameof(key));

    // 指向自身类型的集合导航（Java 的 getTreeNavigateMetadata）
    private static NavigationMetadata? TreeNavigation(MetadataRegistry metadata, EntityMetadata entity) =>
        entity.NavigationProperties.Keys.Select(name => metadata.FindNavigation(entity, name))
            .FirstOrDefault(n => n is { IsCollection: true } && n.Target.Type == entity.Type);

    /// <summary>
    /// 读出后按父子键拼成树（对应 Java 的 toTreeList）：子节点挂到实体上指向自身的集合导航里，兄弟之间保持查询的顺序。
    /// 普通查询：上级不在结果里的行是根。AsTreeCte 之后：按层级配对（同一行沿不同路径走到时各自成节点），往下走时起点是根，
    /// 往上走时每个起点沿上级一直挂到最上面的那一级。
    /// </summary>
    public async Task<List<T>> ToTreeAsync(CancellationToken cancellationToken = default)
    {
        var entity = Db.Metadata.Get<T>();
        var nav = TreeNavigation(Db.Metadata, entity)
                  ?? throw new InvalidOperationException($"{typeof(T).Name} 上没有指向自身的集合导航（如 List<{typeof(T).Name}> Children），无法拼成树");
        if (State.Sources is [TreeCteSource tree, ..])
        {
            var compiled = QueryCompiler.Compile<T>(State with { AppendTreeDeep = true });
            var statement = SqlRenderer.Render(Db.Options.Dialect, compiled.Sql);
            var deepOrdinal = compiled.Sql.Columns.Count - 1;
            var rows = await Db.Executor.QueryAsync(statement,
                r => (Row: compiled.Shaper(r), Deep: Convert.ToInt64(r.GetValue(deepOrdinal), System.Globalization.CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false);
            // 投影成 DTO 时按 DTO 上指向自身的导航取键；直接取实体时按树查询的键列
            var (self, parent) = State.HasSelector ? (nav.SelfColumns, nav.TargetColumns) : (tree.SelfColumns, tree.ParentColumns);
            var nodes = rows.Select(x => new TreeNode(x.Row!, Key(self, x.Row!), Key(parent, x.Row!), x.Deep)).ToList();
            return TreeAssembler.Assemble(nodes, nav, tree.Options.Up).Cast<T>().ToList();
        }
        var list = await ToListAsync(cancellationToken).ConfigureAwait(false);
        return TreeAssembler.Assemble(list.Select(x => new TreeNode(x!, Key(nav.SelfColumns, x!), Key(nav.TargetColumns, x!), null)).ToList(), nav, up: false)
            .Cast<T>().ToList();
    }

    private static object?[] Key(IReadOnlyList<ColumnMetadata> columns, object row) => columns.Select(c => c.GetProviderValue(row)).ToArray();
}

/// <summary>拼树的一个节点：自身键、上级键与层级（不是树查询时为 null）。</summary>
internal sealed record TreeNode(object Entity, object?[] Self, object?[] Parent, long? Deep);

/// <summary>按 Java 的 EasyTreeUtil 拼树：节点键带上层级，下行时父 (键, 层级) 对子 (上级键, 层级 - 1)，上行反过来。</summary>
internal static class TreeAssembler
{
    public static List<object> Assemble(List<TreeNode> nodes, NavigationMetadata children, bool up)
    {
        foreach (var node in nodes) SetChildren(children, node, []);
        if (nodes.Count == 0 || nodes[0].Deep is null) return Plain(nodes, children);
        var starts = nodes.Where(n => n.Deep == 0).ToList();
        var pool = nodes.Where(n => n.Deep != 0).ToList();
        if (!up)
        {
            foreach (var root in starts) AttachDown(root, pool, children);
            return starts.Select(n => n.Entity).ToList();
        }
        var roots = new List<TreeNode>();
        foreach (var start in starts) AttachUp(start, pool, roots, children);
        return roots.Select(n => n.Entity).ToList();
    }

    // 普通查询：上级不在结果里的是根
    private static List<object> Plain(List<TreeNode> nodes, NavigationMetadata children)
    {
        var selves = nodes.Select(n => n.Self).ToList();
        var roots = nodes.Where(n => !selves.Any(s => Same(s, n.Parent))).ToList();
        var pool = nodes.Except(roots).ToList();
        foreach (var root in roots) AttachDown(root, pool, children);
        return roots.Select(n => n.Entity).ToList();
    }

    private static void AttachDown(TreeNode parent, List<TreeNode> pool, NavigationMetadata children)
    {
        var found = pool.Where(n => Same(n.Parent, parent.Self) && n.Deep - 1 == parent.Deep).ToList();
        if (found.Count == 0) return;
        foreach (var child in found) pool.Remove(child);
        SetChildren(children, parent, found);
        foreach (var child in found) AttachDown(child, pool, children);
    }

    private static void AttachUp(TreeNode child, List<TreeNode> pool, List<TreeNode> roots, NavigationMetadata children)
    {
        var parent = pool.FirstOrDefault(n => Same(n.Self, child.Parent) && n.Deep - 1 == child.Deep);
        if (parent is null)
        {
            roots.Add(child);
            return;
        }
        pool.Remove(parent);
        SetChildren(children, parent, [child]);
        AttachUp(parent, pool, roots, children);
    }

    private static bool Same(object?[] a, object?[] b) => a.Length == b.Length && a.Zip(b).All(p => Equals(p.First, p.Second));

    private static void SetChildren(NavigationMetadata children, TreeNode parent, List<TreeNode> nodes)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(children.Target.Type))!;
        foreach (var n in nodes) list.Add(n.Entity);
        children.Property.SetValue(parent.Entity, list);
    }
}
