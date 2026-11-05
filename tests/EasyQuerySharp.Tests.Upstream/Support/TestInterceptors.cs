using System.Linq.Expressions;
using EasyQuerySharp.Interceptors;

namespace EasyQuerySharp.Tests.Upstream.Support;

/// <summary>对应 Java：interceptor/Topic1Interceptor。默认关闭，只在 EnableInterceptor("Topic1Interceptor") 的插入上把标题清空。</summary>
public sealed class Topic1Interceptor : EntityInterceptor<TopicAuto>
{
    public override bool EnabledByDefault => false;

    public override void BeforeInsert(TopicAuto entity) => entity.Title = null;
}

/// <summary>
/// 对应 Java：interceptor/MyEntityInterceptor。插入时补齐创建 / 修改人与时间，按实体更新时刷新修改人与时间；
/// 按条件更新时追加 <c>update_by</c>、<c>update_time</c>（用户已经 Set 过的跳过）。
/// </summary>
public sealed class MyEntityInterceptor : EntityInterceptor<TopicInterceptor>
{
    public override void BeforeInsert(TopicInterceptor entity)
    {
        entity.CreateTime ??= DateTime.Now;
        entity.CreateBy ??= CurrentUserHelper.UserId;
        entity.UpdateTime ??= DateTime.Now;
        entity.UpdateBy ??= CurrentUserHelper.UserId;
    }

    public override void BeforeUpdate(TopicInterceptor entity)
    {
        entity.UpdateTime = DateTime.Now;
        entity.UpdateBy = CurrentUserHelper.UserId;
    }

    public override void BeforeConditionUpdate(UpdateSetBuilder<TopicInterceptor> sets) =>
        sets.Set(e => e.UpdateBy, CurrentUserHelper.UserId).Set(e => e.UpdateTime, DateTime.Now);
}

/// <summary>
/// 对应 Java：interceptor/MyTenantInterceptor 的条件部分：有当前用户时按租户过滤查询、更新、删除。
/// Java 的同一个类还在插入时补租户，C# 拆成 <see cref="MyTenantInsertInterceptor"/>，两者同名，按名字关闭时一起关闭。
/// </summary>
public sealed class MyTenantInterceptor : QueryFilter<TopicInterceptor>
{
    public override Expression<Func<TopicInterceptor, bool>>? Filter()
    {
        if (CurrentUserHelper.UserId is null) return null;
        var tenantId = CurrentUserHelper.TenantId;
        return e => e.TenantId == tenantId;
    }
}

/// <summary>对应 Java：interceptor/MyTenantInterceptor 的插入部分：没有租户时填当前租户。</summary>
public sealed class MyTenantInsertInterceptor : EntityInterceptor<TopicInterceptor>
{
    public override string Name => nameof(MyTenantInterceptor);

    public override void BeforeInsert(TopicInterceptor entity) => entity.TenantId ??= CurrentUserHelper.TenantId;
}

/// <summary>
/// 对应 Java：common/TopicUpdateInterceptor（名字 123123）。<see cref="TopicUpdate"/> 的每次更新都写 <c>create_time</c>：
/// 按实体更新时把它改成 2024-01-01 01:01，限定了列时补上这一列；按条件更新时追加 <c>create_time = 当前时间</c>。
/// </summary>
public sealed class TopicUpdateInterceptor : EntityInterceptor<TopicUpdate>
{
    public override string Name => "123123";

    public override void BeforeUpdate(TopicUpdate entity) => entity.CreateTime = new DateTime(2024, 1, 1, 1, 1, 0);

    public override void BeforeEntityUpdateColumns(UpdateColumnsBuilder<TopicUpdate> columns) => columns.Include(e => e.CreateTime);

    public override void BeforeConditionUpdate(UpdateSetBuilder<TopicUpdate> sets) => sets.Set(e => e.CreateTime, DateTime.Now);
}

/// <summary>
/// 对应 Java：common/TestInterceptor 的 <see cref="InterceptorEntity"/> 部分：查询总是追加原生条件 <c>1=1</c>。
/// Java 的同一个类对 <see cref="Interceptor2Entity"/> 追加 <c>2=2</c>，C# 拆成 <see cref="TestInterceptor2"/>，两者同名，按名字关闭时一起关闭。
/// </summary>
public sealed class TestInterceptor : QueryFilter<InterceptorEntity>
{
    public override Expression<Func<InterceptorEntity, bool>>? Filter() => _ => Sql.Raw<bool>("1=1");
}

/// <summary>对应 Java：common/TestInterceptor 的 <see cref="Interceptor2Entity"/> 部分：追加 <c>2=2</c>。</summary>
public sealed class TestInterceptor2 : QueryFilter<Interceptor2Entity>
{
    public override string Name => nameof(TestInterceptor);

    public override Expression<Func<Interceptor2Entity, bool>>? Filter() => _ => Sql.Raw<bool>("2=2");
}
