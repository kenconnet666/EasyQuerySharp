using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Tests.Upstream.Entities;

namespace EasyQuerySharp.Tests.Upstream.Interceptors;

/// <summary>对应 Java：interceptor/Topic1Interceptor。默认关闭，只在 EnableInterceptor("Topic1Interceptor") 的插入上把标题清空。</summary>
public sealed class Topic1Interceptor : EntityInterceptor<TopicAuto>
{
    public override bool EnabledByDefault => false;

    public override void BeforeInsert(TopicAuto entity) => entity.Title = null;
}
