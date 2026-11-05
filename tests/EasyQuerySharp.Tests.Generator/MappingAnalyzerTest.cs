using System.Collections.Immutable;
using EasyQuerySharp.Generator;
using Microsoft.CodeAnalysis;

namespace EasyQuerySharp.Tests.Generator;

/// <summary>EQ0003（DTO 成员对不上）、EQ0004（ExtraFilter 引用不对）、EQ0005（可空对一映射到非空成员）：每条诊断有正例和反例。</summary>
public sealed class MappingAnalyzerTest
{
    private static Task<ImmutableArray<Diagnostic>> Analyze(string types) => AnalyzerHarness.AnalyzeTypesAsync<MappingAnalyzer>(types);

    [Fact]
    public async Task ReportsDtoMembersWithoutCounterpart()
    {
        var diagnostics = await Analyze("""
            [MapFrom<User>] public class UserVo
            {
                public long Id { get; set; }
                public string Nmae { get; set; } = "";
                [MapPath("Company.Title")] public string? CompanyTitle { get; set; }
            }
            """);
        Assert.Equal(["EQ0003", "EQ0003"], diagnostics.Select(d => d.Id));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("Nmae"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("Company 没有 Title"));
    }

    [Fact]
    public async Task AcceptsNamesPathsExpressionsAndIgnoredMembers()
    {
        var diagnostics = await Analyze("""
            public class PostVo { public string Title { get; set; } = ""; }
            [MapFrom<User>] public class UserVo
            {
                public long id { get; set; }
                [MapPath("Company.Name")] public string? CompanyName { get; set; }
                [MapPath("Posts.Title")] public List<string>? Titles { get; set; }
                [MapExpression("X")] public int PostCount { get; set; }
                [ColumnIgnore] public string? Extra { get; set; }
                public List<PostVo>? Posts { get; set; }
                public string Computed => "";
            }
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsNonNullableDtoForOptionalToOne()
    {
        var diagnostics = await Analyze("""
            public class CompanyVo { public string Name { get; set; } = ""; }
            [MapFrom<User>] public class UserVo { public CompanyVo Company { get; set; } = new(); }
            [MapFrom<User>] public class UserVo2 { public CompanyVo? Company { get; set; } }
            """);
        var d = Assert.Single(diagnostics);
        Assert.Equal("EQ0005", d.Id);
        Assert.Contains("UserVo.Company", d.GetMessage());
    }

    [Fact]
    public async Task ReportsBadExtraFilterReferences()
    {
        var diagnostics = await Analyze("""
            public class Tag { [Column(PrimaryKey = true)] public long Id { get; set; } public long OwnerId { get; set; } public bool Active { get; set; } }
            public class Owner
            {
                [Column(PrimaryKey = true)] public long Id { get; set; }
                static Expression<Func<Tag, bool>> ActiveOnly => t => t.Active;
                Expression<Func<Tag, bool>> NotStatic => t => t.Active;
                static Expression<Func<Owner, bool>> WrongType => o => o.Id > 0;

                [Navigate(RelationType.OneToMany, TargetProperties = ["OwnerId"], ExtraFilter = nameof(ActiveOnly))] public List<Tag>? Good { get; set; }
                [Navigate(RelationType.OneToMany, TargetProperties = ["OwnerId"], ExtraFilter = "Absent")] public List<Tag>? Missing { get; set; }
                [Navigate(RelationType.OneToMany, TargetProperties = ["OwnerId"], ExtraFilter = nameof(NotStatic))] public List<Tag>? Instance { get; set; }
                [Navigate(RelationType.OneToMany, TargetProperties = ["OwnerId"], ExtraFilter = nameof(WrongType))] public List<Tag>? Wrong { get; set; }
            }
            """);
        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("EQ0004", d.Id));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("没有这个成员"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("静态"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("Expression<Func<Tag, bool>>"));
    }
}
