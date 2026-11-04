using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Interceptors;

/// <summary>已注册的拦截器，按实体类型筛选并翻译它们的条件。</summary>
public sealed class InterceptorRegistry
{
    private readonly List<IInterceptor> _interceptors = [];

    /// <summary>注册一个拦截器。</summary>
    public InterceptorRegistry Add(IInterceptor interceptor)
    {
        _interceptors.Add(interceptor);
        return this;
    }

    /// <summary>已注册的拦截器。</summary>
    public IReadOnlyList<IInterceptor> All => _interceptors;

    /// <summary>某实体适用、且在 <paramref name="selection"/> 下生效的拦截器。</summary>
    internal IEnumerable<T> For<T>(Type entityType, InterceptorSelection selection) where T : IInterceptor =>
        _interceptors.OfType<T>().Where(i => i.TargetType.IsAssignableFrom(entityType) && selection.Applies(i));

    /// <summary>翻译作用于某张实体表的全部查询过滤条件。</summary>
    internal IEnumerable<SqlExpr> QueryFilters(EntityTableSource table, InterceptorSelection selection)
    {
        foreach (var interceptor in For<IQueryFilterInterceptor>(table.Entity.Type, selection))
        {
            if (interceptor.FilterLambda() is not { } lambda) continue;
            var translator = new ExpressionTranslator([]);
            translator.Bind(lambda, [new EntityBinding(table)]);
            yield return translator.TranslatePredicate(lambda.Body);
        }
    }

    /// <summary>按条件更新时各拦截器追加的 SET（属性名 → 值）；<paramref name="assigned"/> 是用户已经 Set 的属性。</summary>
    internal List<(string Property, object? Value)> ConditionUpdateSets(Type entityType, IEnumerable<string> assigned, InterceptorSelection selection)
    {
        var collector = new UpdateSetCollector(assigned);
        foreach (var interceptor in For<IEntityWriteInterceptor>(entityType, selection)) interceptor.BeforeConditionUpdate(collector);
        return collector.Sets;
    }

    /// <summary>对实体调用写入前的拦截。</summary>
    internal void BeforeWrite(object entity, bool insert, InterceptorSelection selection)
    {
        foreach (var interceptor in For<IEntityWriteInterceptor>(entity.GetType(), selection))
        {
            if (insert) interceptor.BeforeInsert(entity);
            else interceptor.BeforeUpdate(entity);
        }
    }
}
