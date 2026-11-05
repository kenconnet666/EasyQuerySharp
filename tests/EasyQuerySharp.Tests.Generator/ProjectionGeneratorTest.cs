using EasyQuerySharp.Generator;

namespace EasyQuerySharp.Tests.Generator;

/// <summary>[Projection] 方法的生成器：产出能编译的登记代码；写法不受支持时报 EQ0006。</summary>
public sealed class ProjectionGeneratorTest
{
    [Fact]
    public void GeneratesRegistrationThatCompiles()
    {
        var (generator, errors, sources) = AnalyzerHarness.Generate<ProjectionGenerator>("""
            namespace App
            {
                public record CompanyVo(long Id, string Name);
                public static partial class Vos
                {
                    [Projection] public static CompanyVo ToVo(this Company c) => new(c.Id, c.Name);
                    [Projection] public static string Label(User u, string prefix) { return prefix + u.Name; }
                }
            }
            """);
        Assert.Empty(generator);
        Assert.Empty(errors);
        Assert.Equal(2, sources.Length);
        Assert.All(sources, s => Assert.Contains("ProjectionRegistry.Register", s));
    }

    [Theory]
    [InlineData("public static class Vos { [Projection] public static string N(User u) => u.Name; }", "要声明为 partial")]
    [InlineData("public static partial class Vos { [Projection] public static string N(User u) { var n = u.Name; return n; } }", "只能有一个表达式")]
    [InlineData("public partial class Vos { [Projection] public string N(User u) => u.Name; }", "必须是静态方法")]
    [InlineData("public static partial class Vos { [Projection] public static T N<T>(T u) => u; }", "不能是泛型方法")]
    [InlineData("public static partial class Vos<T> { [Projection] public static string N(User u) => u.Name; }", "不能是泛型")]
    public void ReportsUnsupportedShapes(string types, string problem)
    {
        var (generator, _, sources) = AnalyzerHarness.Generate<ProjectionGenerator>(types);
        var d = Assert.Single(generator);
        Assert.Equal("EQ0006", d.Id);
        Assert.Contains(problem, d.GetMessage());
        Assert.Empty(sources);
    }
}
