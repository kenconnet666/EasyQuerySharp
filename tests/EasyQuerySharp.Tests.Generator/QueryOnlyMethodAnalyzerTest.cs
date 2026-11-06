using EasyQuerySharp.Generator;

namespace EasyQuerySharp.Tests.Generator;

/// <summary>EQ0007（只能在查询表达式里调用的方法）：正例和反例。</summary>
public sealed class QueryOnlyMethodAnalyzerTest
{
    private static Task<System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(string body) =>
        AnalyzerHarness.AnalyzeAsync<QueryOnlyMethodAnalyzer>(body);

    [Fact]
    public async Task ReportsSqlAndSqlFunctionCallsOutsideExpressionTrees()
    {
        var diagnostics = await Analyze("""
            [SqlFunction("LEFT({0},3)")] static string Left3(string s) => throw new InvalidOperationException();
            void M(string name, List<string> names)
            {
                var like = name.Like("a%");
                var window = Sql.Over.RowNumber(w => w);
                var left = Left3(name);
                // Func<…> 的 lambda 会真的执行
                names.Where(n => n.In("a", "b")).ToList();
            }
            """);
        Assert.Equal(["Sql.Like", "Over.RowNumber", "Snippet.Left3", "Sql.In"], diagnostics.Select(d => d.GetMessage().Split(' ')[0]));
        Assert.All(diagnostics, d => Assert.Equal("EQ0007", d.Id));
    }

    [Fact]
    public async Task AllowsQueryExpressionsProjectionsAndIdentifier()
    {
        var diagnostics = await Analyze("""
            [SqlFunction("LEFT({0},3)")] static string Left3(string s) => throw new InvalidOperationException();
            static System.Linq.Expressions.Expression<Func<User, bool>> Named => u => u.Name.Like("a%");
            [Projection] static string Short(User u) => Left3(u.Name);
            void M()
            {
                db.Query<User>().Where(u => u.Name.Like("a%") && Left3(u.Name) == "abc").OrderBy(u => Sql.Random());
                // 嵌套在 Func 里、外层是表达式树：整体仍是表达式树
                db.Query<User>().Include(u => u.Posts, q => q.Where(p => p.Title.In("a", "b")));
                var q2 = from u in db.Query<User>() where u.Name.Like("b%") select u;
                var id = Sql.Identifier("t_user");
            }
            """);
        Assert.Empty(diagnostics);
    }
}
