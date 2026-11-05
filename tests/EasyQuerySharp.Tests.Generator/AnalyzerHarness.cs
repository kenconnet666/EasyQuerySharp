using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EasyQuerySharp.Tests.Generator;

/// <summary>把一段代码连同核心库编译，跑分析器，返回它报出的诊断。片段里可以直接用 <see cref="Prelude"/> 定义的模型。</summary>
internal static class AnalyzerHarness
{
    /// <summary>片段共用的模型与入口：<c>db</c>、<c>User</c>（导航到 <c>Company</c>、<c>Posts</c>）。</summary>
    private const string Prelude = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using EasyQuerySharp;
        using EasyQuerySharp.Metadata;

        public class Company { [Column(PrimaryKey = true)] public long Id { get; set; } public string Name { get; set; } = ""; }
        public class Post { [Column(PrimaryKey = true)] public long Id { get; set; } public long UserId { get; set; } public string Title { get; set; } = ""; }
        public class User
        {
            [Column(PrimaryKey = true)] public long Id { get; set; }
            public string Name { get; set; } = "";
            public long? CompanyId { get; set; }
            [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(CompanyId)])] public Company? Company { get; set; }
            [Navigate(RelationType.OneToMany, TargetProperties = [nameof(Post.UserId)])] public List<Post>? Posts { get; set; }
        }

        """;

    private static readonly ImmutableArray<MetadataReference> References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p)),
        MetadataReference.CreateFromFile(typeof(EasyDb).Assembly.Location),
    ];

    /// <summary>编译 <paramref name="types"/>（顶层类型声明），返回 <typeparamref name="TAnalyzer"/> 报出的诊断。</summary>
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeTypesAsync<TAnalyzer>(string types) where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var compilation = CSharpCompilation.Create("Snippet", [CSharpSyntaxTree.ParseText("using System.Linq.Expressions;\n" + Prelude + types)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return await compilation.WithAnalyzers([new TAnalyzer()]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 编译 <paramref name="types"/>（顶层类型声明）并运行生成器 <typeparamref name="TGenerator"/>：返回生成器报出的诊断、
    /// 加入生成代码之后的编译错误，以及生成的源码。
    /// </summary>
    public static (ImmutableArray<Diagnostic> Generator, ImmutableArray<Diagnostic> Errors, string[] Sources) Generate<TGenerator>(string types)
        where TGenerator : IIncrementalGenerator, new()
    {
        var compilation = CSharpCompilation.Create("Snippet", [CSharpSyntaxTree.ParseText(Prelude + types, path: "Snippet.cs")], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(new TGenerator()).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics,
            TestContext.Current.CancellationToken);
        var sources = driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()).ToArray();
        return (diagnostics, [.. output.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error)], sources);
    }

    /// <summary>编译 <paramref name="body"/>（类成员，放在 <c>Snippet</c> 类里），返回 <typeparamref name="TAnalyzer"/> 报出的诊断。</summary>
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync<TAnalyzer>(string body) where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var source = Prelude + "public class Snippet\n{\n    private readonly EasyDb db = null!;\n" + body + "\n}\n";
        var compilation = CSharpCompilation.Create("Snippet", [CSharpSyntaxTree.ParseText(source)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return await compilation.WithAnalyzers([new TAnalyzer()]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }
}
