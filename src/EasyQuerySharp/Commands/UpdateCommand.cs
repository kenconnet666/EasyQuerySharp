using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>按实体更新时写哪些列。</summary>
public enum ColumnStrategy
{
    /// <summary>全部列（默认，与 Java 版一致）。</summary>
    All,

    /// <summary>只写值不为 null 的列（对应 Java 的 ONLY_NOT_NULL_COLUMNS）。</summary>
    OnlyNotNull,

    /// <summary>只写值为 null 的列（对应 Java 的 ONLY_NULL_COLUMNS）。</summary>
    OnlyNull,
}

/// <summary>
/// UPDATE。两种用法：
/// <list type="bullet">
/// <item>按条件：<c>db.Update&lt;Topic&gt;().Set(t => new Topic { Title = "x", Stars = t.Stars + 1 }).Where(t => t.Id == id)</c>；</item>
/// <item>按实体：<c>db.Update(topic)</c>，写全部列（主键、<see cref="UpdateIgnoreAttribute"/> 除外），按主键定位。</item>
/// </list>
/// 实体有逻辑删除列时条件里自动加"未删除"；没有任何条件的按条件更新会被拒绝（除非调用 <c>All()</c>）。
/// </summary>
public sealed class UpdateCommand<T> : WriteCommandBase<UpdateCommand<T>, T> where T : class
{
    private readonly List<SetSpec> _sets = [];
    private ColumnStrategy _strategy = ColumnStrategy.All;

    internal UpdateCommand(EasyDb db, IReadOnlyList<T>? entities) : base(db, entities)
    {
    }

    /// <summary>用成员初始化表达式设置多列，值可以引用原列：<c>t => new Topic { Stars = t.Stars + 1 }</c>。</summary>
    public UpdateCommand<T> Set(Expression<Func<T, T>> setter)
    {
        if (setter.Body is not MemberInitExpression init)
            throw new ArgumentException("Set 需要成员初始化表达式，如 t => new Topic { Title = \"x\" }", nameof(setter));
        foreach (var binding in init.Bindings.Cast<MemberAssignment>())
            _sets.Add(new SetSpec(binding.Member, Expression.Lambda(binding.Expression, setter.Parameters), SetKind.Expression));
        return this;
    }

    /// <summary>把一列设为常量值。</summary>
    public UpdateCommand<T> Set<TValue>(Expression<Func<T, TValue>> column, TValue value)
    {
        _sets.Add(new SetSpec(MemberOf(column), Expression.Lambda(Expression.Constant(value, typeof(TValue)), column.Parameters), SetKind.Expression));
        return this;
    }

    /// <summary>把一列设为表达式的值（可以引用原列）。</summary>
    public UpdateCommand<T> Set<TValue>(Expression<Func<T, TValue>> column, Expression<Func<T, TValue>> value)
    {
        _sets.Add(new SetSpec(MemberOf(column), value, SetKind.Expression));
        return this;
    }

    /// <summary><paramref name="condition"/> 为 true 时才设置这一列。</summary>
    public UpdateCommand<T> SetIf<TValue>(bool condition, Expression<Func<T, TValue>> column, TValue value) => condition ? Set(column, value) : this;

    /// <summary>把一列设为 NULL。</summary>
    public UpdateCommand<T> SetNull<TValue>(Expression<Func<T, TValue>> column) => Set(column, default(TValue)!);

    /// <summary>自增：<c>col = col + ?</c>（对应 Java 的 increment）。</summary>
    public UpdateCommand<T> Increment<TValue>(Expression<Func<T, TValue>> column, TValue by)
    {
        _sets.Add(new SetSpec(MemberOf(column), Expression.Lambda(Expression.Constant(by, typeof(TValue)), column.Parameters), SetKind.Increment));
        return this;
    }

    /// <summary>自减：<c>col = col - ?</c>（对应 Java 的 decrement）。</summary>
    public UpdateCommand<T> Decrement<TValue>(Expression<Func<T, TValue>> column, TValue by)
    {
        _sets.Add(new SetSpec(MemberOf(column), Expression.Lambda(Expression.Constant(by, typeof(TValue)), column.Parameters), SetKind.Decrement));
        return this;
    }

    /// <summary>按实体更新时写哪些列。</summary>
    public UpdateCommand<T> Columns(ColumnStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    /// <summary>渲染第一条语句（按实体更新时是第一个实体的语句），不执行。</summary>
    public string ToSql() => Statements().First().Text;

    /// <summary>执行，返回受影响的总行数。</summary>
    public async Task<long> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (Entities is not null)
            foreach (var e in Entities) Db.Interceptors.BeforeWrite(e, insert: false, DisabledInterceptors);
        return await Db.Executor.SessionAsync(async session =>
        {
            long rows = 0;
            foreach (var statement in Statements()) rows += await session.ExecuteAsync(statement, cancellationToken).ConfigureAwait(false);
            return rows;
        }, cancellationToken).ConfigureAwait(false);
    }

    private IEnumerable<SqlStatement> Statements()
    {
        if (Entities is null)
        {
            yield return Render(null);
            yield break;
        }
        foreach (var entity in Entities) yield return Render(entity);
    }

    private SqlStatement Render(T? entity)
    {
        var table = NewTable();
        var update = new UpdateStatement(table);
        if (entity is not null) AddEntitySets(update, table, entity);
        AddExpressionSets(update, table);
        if (update.Sets.Count == 0) throw new InvalidOperationException($"{typeof(T).Name} 的更新没有要写的列");
        update.Where.AddRange(BuildWhere(table, entity, applyLogicDelete: true));
        return SqlRenderer.Render(Db.Options.Dialect, update);
    }

    private void AddEntitySets(UpdateStatement update, EntityTableSource table, T entity)
    {
        var explicitly = _sets.Select(s => s.Member.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var column in Entity.Columns)
        {
            if (column.IsPrimaryKey || column.UpdateIgnore || column.IsGenerated || explicitly.Contains(column.PropertyName)) continue;
            var value = column.GetProviderValue(entity);
            if (_strategy == ColumnStrategy.OnlyNotNull && value is null) continue;
            if (_strategy == ColumnStrategy.OnlyNull && value is not null) continue;
            update.Sets.Add((table.Column(column), ExpressionTranslator.Parameter(value, column.Converter?.ProviderType ?? column.ClrType)));
        }
    }

    private void AddExpressionSets(UpdateStatement update, EntityTableSource table)
    {
        foreach (var set in _sets)
        {
            var column = table.Column(Entity.GetColumn(set.Member.Name));
            var translator = new ExpressionTranslator([]);
            translator.Bind(set.Value, [new EntityBinding(table)]);
            var value = ExpressionTranslator.ApplyConverter(translator.Translate(set.Value.Body), column);
            update.Sets.Add((column, set.Kind switch
            {
                // 自增 / 自减不加括号：`star` = `star` + ?（Java 版的写法）
                SetKind.Increment => new FragmentExpr("{0} + {1}", [column, value]),
                SetKind.Decrement => new FragmentExpr("{0} - {1}", [column, value]),
                _ => value,
            }));
        }
    }

    private static MemberInfo MemberOf(LambdaExpression column) =>
        ExpressionTranslator.StripConvert(column.Body) is MemberExpression m
            ? m.Member
            : throw new ArgumentException($"需要直接的属性访问，如 t => t.Title：{column}", nameof(column));

    private enum SetKind
    {
        Expression,
        Increment,
        Decrement,
    }

    private sealed record SetSpec(MemberInfo Member, LambdaExpression Value, SetKind Kind);
}
