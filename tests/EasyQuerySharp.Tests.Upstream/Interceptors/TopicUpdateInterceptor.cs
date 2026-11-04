using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream.Interceptors;

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
