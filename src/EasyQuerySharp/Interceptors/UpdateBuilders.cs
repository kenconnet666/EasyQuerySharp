using System.Linq.Expressions;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Interceptors;

/// <summary>
/// 拦截器追加的列：属性名 → 值（按条件更新的 SET）；按实体更新补列时只用属性名。
/// 已经要写的列（用户指定过、前面的拦截器加过）不再追加。列用属性访问表达式指定，按属性名对应到实体的列。
/// </summary>
internal sealed class UpdateSetCollector(IEnumerable<string> assigned)
{
    private readonly HashSet<string> _assigned = new(assigned, StringComparer.Ordinal);

    /// <summary>按追加顺序的 SET。</summary>
    public List<(string Property, object? Value)> Sets { get; } = [];

    public bool IsSet(LambdaExpression column) => _assigned.Contains(ExpressionTranslator.MemberOf(column).Name);

    public void Set(LambdaExpression column, object? value)
    {
        var property = ExpressionTranslator.MemberOf(column).Name;
        if (_assigned.Add(property)) Sets.Add((property, value));
    }
}

/// <summary>
/// 按条件更新（<c>db.Update&lt;T&gt;().Set(..)</c>）时追加 SET 的入口，见 <see cref="EntityInterceptor{TEntity}.BeforeConditionUpdate"/>。
/// <typeparamref name="TEntity"/> 可以是接口（按属性名对应到实体的列）。
/// </summary>
public sealed class UpdateSetBuilder<TEntity>
{
    private readonly UpdateSetCollector _collector;

    internal UpdateSetBuilder(UpdateSetCollector collector) => _collector = collector;

    /// <summary>这一列是否已经要写（用户 Set 过，或前面的拦截器加过）。</summary>
    public bool IsSet<TValue>(Expression<Func<TEntity, TValue>> column) => _collector.IsSet(column);

    /// <summary>追加 <c>列 = 值</c>；这一列已经要写时忽略（与 Java 的"是否已经 set 了"检查一致）。</summary>
    public UpdateSetBuilder<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> column, TValue value)
    {
        _collector.Set(column, value);
        return this;
    }
}

/// <summary>
/// 按实体更新且用 <c>SetColumns</c> 限定了列时补列的入口（对应 Java 的 UpdateEntityColumnInterceptor），
/// 见 <see cref="EntityInterceptor{TEntity}.BeforeEntityUpdateColumns"/>；补的列写在用户指定的列之后，值取实体上的。
/// </summary>
public sealed class UpdateColumnsBuilder<TEntity>
{
    private readonly UpdateSetCollector _collector;

    internal UpdateColumnsBuilder(UpdateSetCollector collector) => _collector = collector;

    /// <summary>这一列是否已经要写。</summary>
    public bool IsIncluded<TValue>(Expression<Func<TEntity, TValue>> column) => _collector.IsSet(column);

    /// <summary>也写这一列；已经要写时忽略。</summary>
    public UpdateColumnsBuilder<TEntity> Include<TValue>(Expression<Func<TEntity, TValue>> column)
    {
        _collector.Set(column, null);
        return this;
    }
}
