using System.Text;
using EasyQuerySharp.Encryption;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Features;

/// <summary>
/// 对应 Java：GenericTest2.java（不连库的单元测试）。命名约定、分段加密的往返在 C# 里有对应；
/// 其余测的是 Java 自己的工具类（Base64、Bean、Introspector、druid 格式化、数据源等），不适用。
/// </summary>
public sealed class GenericTest2
{
    private const string JavaUtility = "测的是 Java 内部工具类，C# 用 BCL 或没有对应的公共 API";
    private const string NoSm4 = "SM4 不在 .NET 的 BCL 里，核心不引入第三方加密库；需要时由使用方实现 IEncryptionStrategy";

    [Fact, Upstream("GenericTest2.java#SqlRangeTest1", NotApplicable = JavaUtility)] public void SqlRangeTest1() { }
    [Fact, Upstream("GenericTest2.java#base64Test", NotApplicable = JavaUtility)] public void Base64Test() { }
    [Fact, Upstream("GenericTest2.java#aesTest2", NotApplicable = JavaUtility)] public void AesTest2() { }
    [Fact, Upstream("GenericTest2.java#behavior", NotApplicable = JavaUtility)] public void Behavior() { }
    [Fact, Upstream("GenericTest2.java#executeMethodBehaviorTest1", NotApplicable = JavaUtility)] public void ExecuteMethodBehaviorTest1() { }
    [Fact, Upstream("GenericTest2.java#linkedCaseInsensitiveMapTest1", NotApplicable = JavaUtility), Upstream("GenericTest2.java#testLinkedCaseInsensitiveMap1", NotApplicable = JavaUtility)]
    public void LinkedCaseInsensitiveMapTest1() { }
    [Fact, Upstream("GenericTest2.java#createTest1", NotApplicable = JavaUtility)] public void CreateTest1() { }
    [Fact, Upstream("GenericTest2.java#StringCharSegmentTest1", NotApplicable = "测的是 Java 的 EasyStringUtil 分段工具；C# 的分段是加密策略的内部实现，由往返用例覆盖")] public void StringCharSegmentTest1() { }
    [Fact, Upstream("GenericTest2.java#check", NotApplicable = JavaUtility)] public void Check() { }
    [Fact, Upstream("GenericTest2.java#test1", NotApplicable = "测的是 Java 的 Introspector 反射")] public void Test1() { }
    [Fact, Upstream("GenericTest2.java#sqlFormat", NotApplicable = "测的是 druid 的 SQL 格式化工具")] public void SqlFormat() { }
    [Fact, Upstream("GenericTest2.java#testAgroalGetUrl", NotApplicable = "测的是 Agroal 数据源")] public void TestAgroalGetUrl() { }
    [Fact, Upstream("GenericTest2.java#sm4StandardVectorTest", NotApplicable = NoSm4)] public void Sm4StandardVectorTest() { }
    [Fact, Upstream("GenericTest2.java#easySm4EncryptionTest", NotApplicable = NoSm4)] public void EasySm4EncryptionTest() { }
    [Fact, Upstream("GenericTest2.java#StringCharSegmentSm4Test", NotApplicable = NoSm4)] public void StringCharSegmentSm4Test() { }
    [Fact, Upstream("GenericTest2.java#StringCharSegmentSm4Test1", NotApplicable = NoSm4)] public void StringCharSegmentSm4Test1() { }
    [Fact, Upstream("GenericTest2.java#StringCharSegmentSm4Test2", NotApplicable = NoSm4)] public void StringCharSegmentSm4Test2() { }
    [Fact, Upstream("GenericTest2.java#easySm4EncryptionIdCardTest", NotApplicable = NoSm4)] public void EasySm4EncryptionIdCardTest() { }
    [Fact, Upstream("GenericTest2.java#EnumValueTest1", NotApplicable = "测的是 Java 测试工程自己的枚举反序列化工具；C# 的枚举按名字 / 数值的转换由 EnumNameConverter 等覆盖"),
     Upstream("GenericTest2.java#EnumValueTest2", NotApplicable = "同 EnumValueTest1")]
    public void EnumValueTest1() { }

    // 四种命名约定（Java 的 LowerCamelCase / UpperCamelCase / Underlined / UpperUnderlined NameConversion）
    [Fact, Upstream("GenericTest2.java#nameConversionTest1")]
    public void NameConversionTest1()
    {
        foreach (var name in new[] { "NameConversion", "nameConversion" })
        {
            Assert.Equal("nameConversion", NameConversion.LowerCamelCase.Convert(name));
            Assert.Equal("NameConversion", NameConversion.UpperCamelCase.Convert(name));
            Assert.Equal("name_conversion", NameConversion.Underlined.Convert(name));
            Assert.Equal("NAME_CONVERSION", NameConversion.UpperUnderlined.Convert(name));
        }
    }

    private static void AssertRoundTrip(IEncryptionStrategy strategy, string plaintext) =>
        Assert.Equal(plaintext, strategy.Decrypt(typeof(object), "", strategy.Encrypt(typeof(object), "", plaintext)));

    [Fact, Upstream("GenericTest2.java#easyEncryptionTest")]
    public void EasyEncryptionTest()
    {
        var strategy = new DefaultAesEasyEncryptionStrategy();
        AssertRoundTrip(strategy, "❤️❤️❤️❤️❤️❤️❤️❤️❤️❤️❤️垚垚垚垚垚垚");
        AssertRoundTrip(strategy, "18888888888");
    }

    // 分段加密：前缀的密文是整串密文的前缀（LIKE 查询靠这一点）
    [Fact, Upstream("GenericTest2.java#easyEncryptionTest1")]
    public void EasyEncryptionTest1()
    {
        var strategy = new DefaultAesEasyEncryptionStrategy();
        AssertRoundTrip(strategy, "188888881212");
        var whole = (string)strategy.Encrypt(typeof(object), "", "188888881212")!;
        var prefix = (string)strategy.Encrypt(typeof(object), "", "1888888812")!;
        Assert.StartsWith(prefix, whole, StringComparison.Ordinal);
    }

    // 随机串：ASCII、中文、全角符号；emoji 只给按完整字符分段的策略
    private static string RandomText(Random random, int length, bool emoji)
    {
        string[] pool = ["a", "Z", "9", "_", "%", "\\", "'", "中", "文", "字", "符", "※", "·", "√", "ェ", "‍", "️"];
        string[] emojis = ["❤", "💊", "👨", "🤡", "😈"];
        var sb = new StringBuilder();
        for (var i = 0; i < length; i++)
            sb.Append(emoji && random.Next(5) == 0 ? emojis[random.Next(emojis.Length)] : pool[random.Next(pool.Length)]);
        return sb.ToString();
    }

    [Fact, Upstream("GenericTest2.java#StringCharSegmentTest2"), Upstream("GenericTest2.java#StringCharSegmentTest3")]
    public void StringCharSegmentTest2()
    {
        var strategy = new DefaultAesEasyEncryptionStrategy();
        var random = new Random(2);
        for (var i = 0; i < 2000; i++) AssertRoundTrip(strategy, RandomText(random, random.Next(70) + 1, emoji: false));
    }

    [Fact, Upstream("GenericTest2.java#StringCharSegmentTest7")]
    public void StringCharSegmentTest7()
    {
        var strategy = new DefaultSafeAesEasyEncryptionStrategy();
        var random = new Random(7);
        for (var i = 0; i < 2000; i++) AssertRoundTrip(strategy, RandomText(random, random.Next(60) + 11, emoji: true));
    }

    // Java 用例里出过错的几段固定字符串
    [Fact, Upstream("GenericTest2.java#StringCharSegmentTest4"), Upstream("GenericTest2.java#StringCharSegmentTest5")]
    public void StringCharSegmentTest4()
    {
        var strategy = new DefaultAesEasyEncryptionStrategy();
        AssertRoundTrip(strategy, "qO)3\\{*h️�MbjT");
        AssertRoundTrip(strategy, Encoding.UTF8.GetString(Convert.FromBase64String("NSxcLu+4j35ocjElWTEpTyUr5Y+ve+WKoDdiIFlkLj85L2An77iPTkM5UEI=")));
    }

    [Fact, Upstream("GenericTest2.java#StringCharSegmentTest6"), Upstream("GenericTest2.java#StringCharSegmentTest8"), Upstream("GenericTest2.java#StringCharSegmentTest10")]
    public void StringCharSegmentTest6()
    {
        var strategy = new DefaultSafeAesEasyEncryptionStrategy();
        AssertRoundTrip(strategy, Encoding.UTF8.GetString(Convert.FromBase64String("5Yqg5LulVOWtl1tfcSB9RmdfekIj4oOj5YqgXlzlk4g/diLkuK1eauKAjV9P5ZOIP3vwn5KKcVhe")));
        AssertRoundTrip(strategy, Encoding.UTF8.GetString(Convert.FromBase64String("OTpIT+KDo+KdpDYq5Z6aIzMzek1A77iPMXlG8J+RqFNjXF5PXumHjDBY5ZyoMA==")));
        AssertRoundTrip(strategy, "√在-`N*以\\字中d\\~_yb2❤USY💊👨🤡👨5G*😈符T*9L^9oP可符F※,x·Z∝这4ェ(v‍");
    }
}
