using System.Collections.Immutable;
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

    /// <summary>某实体适用、且没有被关闭的拦截器；<paramref name="disabled"/> 含 <c>*</c> 时全部关闭。</summary>
    internal IEnumerable<T> For<T>(Type entityType, ImmutableHashSet<string> disabled) where T : IInterceptor =>
        disabled.Contains("*")
            ? []
            : _interceptors.OfType<T>().Where(i => i.TargetType.IsAssignableFrom(entityType) && !disabled.Contains(i.Name));

    /// <summary>翻译作用于某张实体表的全部查询过滤条件。</summary>
    internal IEnumerable<SqlExpr> QueryFilters(EntityTableSource table, ImmutableHashSet<string> disabled)
    {
        foreach (var interceptor in For<IQueryFilterInterceptor>(table.Entity.Type, disabled))
        {
            if (interceptor.FilterLambda() is not { } lambda) continue;
            var translator = new ExpressionTranslator([]);
            translator.Bind(lambda, [new EntityBinding(table)]);
            yield return translator.TranslatePredicate(lambda.Body);
        }
    }

    /// <summary>对实体调用写入前的拦截。</summary>
    internal void BeforeWrite(object entity, bool insert, ImmutableHashSet<string> disabled)
    {
        foreach (var interceptor in For<IEntityWriteInterceptor>(entity.GetType(), disabled))
        {
            if (insert) interceptor.BeforeInsert(entity);
            else interceptor.BeforeUpdate(entity);
        }
    }
}
