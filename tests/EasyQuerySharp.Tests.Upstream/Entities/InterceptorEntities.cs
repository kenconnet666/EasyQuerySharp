using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：mysql8/entity/InterceptorEntity（受 TestInterceptor 过滤，一对多到 <see cref="Interceptor2Entity"/>）。表由固件按代码优先创建。</summary>
[Table("m8_interceptor")]
public class InterceptorEntity
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(Interceptor2Entity.Aid)])]
    public List<Interceptor2Entity>? List { get; set; }
}

/// <summary>对应 Java：mysql8/entity/Interceptor2Entity。</summary>
[Table("m8_interceptor2")]
public class Interceptor2Entity
{
    public string? Id { get; set; }

    public string? Aid { get; set; }

    public string? Name { get; set; }
}
