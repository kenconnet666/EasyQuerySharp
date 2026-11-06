using System.Linq.Expressions;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>聚合保存的行为开关（对应 Java 的 SaveBehaviorEnum，见 0004），可以组合。</summary>
[Flags]
public enum SaveBehavior
{
    /// <summary>默认。</summary>
    None = 0,

    /// <summary>根不增不改，只处理子对象。</summary>
    IgnoreRoot = 1,

    /// <summary>新的根照常插入，已追踪的根不更新。</summary>
    IgnoreRootUpdate = 2,

    /// <summary>允许把子对象挂到别的父对象（改写外键）；默认外键已指向别的父对象时报错。</summary>
    AllowOwnershipChange = 4,

    /// <summary>已追踪父对象上为 null 的导航（对一或集合）跳过，不把原来的子对象当孤儿。</summary>
    IgnoreNull = 8,

    /// <summary>空集合跳过，不把原来的子对象当孤儿。</summary>
    IgnoreEmpty = 16,

    /// <summary>孤儿物理删除；默认实体有逻辑删除列时按逻辑删除。</summary>
    PhysicalDelete = 32,
}

/// <summary>
/// 聚合保存（对应 Java 的 savable(root).executeCommand()，规则见 0004）：按追踪快照的差异一次写出整个聚合——
/// 新对象插入、改过的更新、从对象图里拿掉的子对象按导航的 <see cref="Metadata.Cascade"/> 删除或断开，多对多维护中间表。
/// 必须在追踪作用域（<see cref="EasyDb.BeginTracking"/>）与事务里执行。
/// </summary>
/// <example><code>
/// using var tracking = db.BeginTracking();
/// await using var tx = db.BeginTransaction();
/// var order = await db.Query&lt;Order&gt;().Include(o =&gt; o.Lines).WhereById(1).SingleAsync();
/// order.Lines!.Add(new OrderLine { Sku = "x" });
/// await db.Save(order).ExecuteAsync();
/// await tx.CommitAsync();
/// </code></example>
public sealed class SaveCommand<T> where T : class
{
    private readonly EasyDb _db;
    private readonly IReadOnlyList<T> _roots;
    private SaveBehavior _behavior;
    private List<HashSet<string>>? _paths;
    private bool _removeRoot;

    internal SaveCommand(EasyDb db, IReadOnlyList<T> roots)
    {
        _db = db;
        _roots = roots;
    }

    /// <summary>叠加行为开关。</summary>
    public SaveCommand<T> With(SaveBehavior behavior)
    {
        _behavior |= behavior;
        return this;
    }

    /// <summary>
    /// 只级联列出的导航（对应 Java 的 savePath）：<c>r =&gt; r.Lines</c>、<c>r =&gt; r.Lines.Select(l =&gt; l.Details)</c>。
    /// 按深度与导航名匹配；列出的导航必须是聚合内的（值对象）导航。
    /// </summary>
    public SaveCommand<T> SavePath(params Expression<Func<T, object?>>[] paths)
    {
        _paths ??= [];
        foreach (var path in paths)
        {
            var names = NavigationPath(path.Body);
            for (var depth = 0; depth < names.Count; depth++)
            {
                while (_paths.Count <= depth) _paths.Add(new HashSet<string>(StringComparer.Ordinal));
                _paths[depth].Add(names[depth]);
            }
        }
        return this;
    }

    /// <summary>删除根（对应 Java 的 removeRoot）：它已加载的子对象按级联规则一并删除或断开。</summary>
    public SaveCommand<T> RemoveRoot()
    {
        _removeRoot = true;
        return this;
    }

    /// <summary>执行保存。</summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (_roots.Count == 0) return;
        if (!_db.Tracker.Active) throw new InvalidOperationException("聚合保存要在追踪作用域里执行：先调用 db.BeginTracking()，并用追踪查询加载要保存的对象");
        if (Execution.EasyTransaction.Current(_db) is null) throw new InvalidOperationException("聚合保存会执行多条语句，必须在事务里：先调用 db.BeginTransaction()");
        var plan = new SavePlan(_db, _behavior, _paths, typeof(T));
        foreach (var root in _roots) plan.AddRoot(root ?? throw new ArgumentException("保存的对象不能为 null"), _removeRoot);
        await plan.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    // r => r.A.B、r => r.Lines.Select(l => l.Details)：依次经过的导航名
    private static List<string> NavigationPath(Expression body)
    {
        var names = new List<string>();
        var current = ExpressionTranslator.StripConvert(body);
        while (true)
        {
            switch (current)
            {
                case MemberExpression member:
                    names.Insert(0, member.Member.Name);
                    current = member.Expression;
                    continue;
                case MethodCallExpression { Method.Name: nameof(Enumerable.Select), Arguments.Count: 2 } select:
                    names.InsertRange(0, NavigationPath(ExpressionTranslator.StripConvert(select.Arguments[1]) is LambdaExpression inner ? inner.Body : select.Arguments[1]));
                    current = select.Arguments[0];
                    continue;
                case ParameterExpression:
                    return names;
                default:
                    throw new NotSupportedException($"SavePath 只能写导航路径，如 r => r.Lines 或 r => r.Lines.Select(l => l.Details)：{body}");
            }
        }
    }
}
