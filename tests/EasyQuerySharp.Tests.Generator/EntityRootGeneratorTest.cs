using EasyQuerySharp.Generator;

namespace EasyQuerySharp.Tests.Generator;

/// <summary>
/// 保留使用者类型的生成器（裁剪 / Native AOT，见 0008）：从交给 EasyQuerySharp 的类型出发顺着属性找全，生成能编译的保留代码；
/// 值类型集合与枚举要用到的实例化引用一次。运行时的效果由 tests/EasyQuerySharp.Tests.Aot 在 Native AOT 下核对。
/// </summary>
public sealed class EntityRootGeneratorTest
{
    [Fact]
    public void RootsTypesHandedToTheLibraryAndWhatTheyReach()
    {
        var (generator, errors, sources) = AnalyzerHarness.Generate<EntityRootGenerator>("""
            namespace App
            {
                public enum Level { Low, High }
                public class Address { public string City { get; set; } = ""; }
                public class Shop
                {
                    [Column(PrimaryKey = true)] public long Id { get; set; }
                    [ValueObject] public Address Address { get; set; } = new();
                    public Level Level { get; set; }
                    public List<decimal>? Prices { get; set; }
                }
                public class ShopRow { public string Name { get; set; } = ""; }
                [Table("t_tag")] public class Tag { [Column(PrimaryKey = true)] public long Id { get; set; } }
                public class Unused { public long Id { get; set; } }

                public static class Queries
                {
                    public static object Run(EasyDb db, User user) => new object[]
                    {
                        db.Query<Shop>().Select<ShopRow>(),
                        db.Insert(user),
                    };

                    // 私有的嵌套类型：生成的类看不见它，按名字保留
                    private sealed class Hidden { public long Id { get; set; } }
                    public static object Private(EasyDb db) => db.Query<Hidden>();
                }
            }
            """);
        Assert.Empty(generator);
        Assert.Empty(errors);
        var source = Assert.Single(sources);
        foreach (var rooted in new[] { "App.Shop", "App.Address", "App.ShopRow", "App.Tag", "global::User", "global::Company", "global::Post" })
            Assert.Contains("typeof(" + (rooted.StartsWith("global::", StringComparison.Ordinal) ? rooted : "global::" + rooted) + ")", source);
        Assert.Contains("\"App.Queries.Hidden\", \"Snippet\"", source);
        Assert.DoesNotContain("Unused", source);
        Assert.Contains("new global::System.Collections.Generic.List<decimal>()", source);
        Assert.Contains("new global::EasyQuerySharp.Metadata.EnumNameConverter<global::App.Level>()", source);
        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", source);
    }

    [Fact]
    public void GeneratesNothingWithoutLibraryTypes()
    {
        var (_, errors, sources) = AnalyzerHarness.Generate<EntityRootGenerator>("namespace App { public class Plain { public int Id { get; set; } } }");
        Assert.Empty(errors);
        Assert.Empty(sources);
    }
}
