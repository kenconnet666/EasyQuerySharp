using System.Linq.Expressions;

namespace EasyQuerySharp.Interceptors;

/// <summary>
/// 拦截器的公共部分。<see cref="Name"/> 用于在单条查询上关闭（<c>DisableInterceptor("name")</c>）；
/// <see cref="TargetType"/> 决定作用于哪些实体：实体类型本身、它的基类或它实现的接口都算
/// （例如 <c>QueryFilter&lt;ITenantEntity&gt;</c> 作用于所有租户实体）。
/// </summary>
public interface IInterceptor
{
    /// <summary>名字，默认是类型名。</summary>
    string Name { get; }

    /// <summary>作用的实体类型（或其基类 / 接口）。</summary>
    Type TargetType { get; }

    /// <summary>默认是否生效（对应 Java 的 enable()）；为 false 时只在 <c>EnableInterceptor(name)</c> 的那条语句上生效。</summary>
    bool EnabledByDefault => true;

    /// <summary>受保护（对应 Java 的 ProtectedInterceptor）：<c>DisableInterceptors()</c> 关不掉，只能按名字 <c>DisableInterceptor(name)</c> 关闭。</summary>
    bool IsProtected => false;
}

/// <summary>非泛型的查询过滤入口，供注册表调用。</summary>
internal interface IQueryFilterInterceptor : IInterceptor
{
    LambdaExpression? FilterLambda();
}

/// <summary>非泛型的写入前拦截入口，供注册表调用。</summary>
internal interface IEntityWriteInterceptor : IInterceptor
{
    void BeforeInsert(object entity);

    void BeforeUpdate(object entity);

    void BeforeConditionUpdate(UpdateSetCollector collector);

    void BeforeEntityUpdateColumns(UpdateSetCollector collector);
}
