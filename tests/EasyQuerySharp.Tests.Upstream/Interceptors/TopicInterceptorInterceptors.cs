using System.Linq.Expressions;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Tests.Upstream.Entities;
using EasyQuerySharp.Tests.Upstream.LogicDelete;

namespace EasyQuerySharp.Tests.Upstream.Interceptors;

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
