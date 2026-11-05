using EasyQuerySharp.Generator;

namespace EasyQuerySharp.Tests.Generator;

/// <summary>EQ0001（不能翻译的方法）与 EQ0002（?.）：每条诊断有正例和反例。</summary>
public sealed class QueryExpressionAnalyzerTest
{
    private static Task<System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(string body) =>
        AnalyzerHarness.AnalyzeAsync<QueryExpressionAnalyzer>(body);

    [Fact]
    public async Task ReportsUserMethodsThatUseQueryParameters()
    {
        var diagnostics = await Analyze("""
            static string Shorten(string s) => s[..3];
            void M() => db.Query<User>().Where(u => Shorten(u.Name) == "abc");
            """);
        var d = Assert.Single(diagnostics);
        Assert.Equal("EQ0001", d.Id);
        Assert.Contains("Snippet.Shorten", d.GetMessage());
    }

    [Fact]
    public async Task ReportsInsideNestedCollectionLambdasOnce()
    {
        var diagnostics = await Analyze("""
            static string Shorten(string s) => s[..3];
            void M() => db.Query<User>().Where(u => u.Posts!.Any(p => Shorten(p.Title) == "abc"));
            """);
        Assert.Equal("EQ0001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task AllowsTranslatableAndClientEvaluatedCalls()
    {
        var diagnostics = await Analyze("""
            [SqlFunction("LEFT({0},3)")] static string Left3(string s) => throw new InvalidOperationException();
            static string Normalize(string s) => s.Trim();
            void M(string input)
            {
                // [SqlFunction] 方法、BCL 方法、Sql.* 都能翻译；不引用查询参数的调用在客户端先算
                db.Query<User>().Where(u => Left3(u.Name) == "abc" && u.Name.ToUpper().StartsWith("A") && u.Name == Normalize(input));
                db.Query<User>().OrderBy(u => Sql.Random());
                // 不是查询表达式：普通的 LINQ to Objects 不检查
                new List<string>().Where(s => Normalize(s) == "");
            }
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task SuggestsNullForgivingInsteadOfNullPropagation()
    {
        var diagnostics = await Analyze("""
            void M() => db.Query<User>().Select(u => u.Company?.Name);
            """);
        var d = Assert.Single(diagnostics);
        Assert.Equal("EQ0002", d.Id);
        Assert.Contains("u.Company!.Name", d.GetMessage());
    }

    [Fact]
    public async Task RewritesNestedNullPropagationInOneSuggestion()
    {
        var diagnostics = await Analyze("""
            void M() => db.Query<User>().Select(u => u.Company?.Name?.Length);
            """);
        Assert.Contains("u.Company!.Name!.Length", Assert.Single(diagnostics).GetMessage());
    }
}
