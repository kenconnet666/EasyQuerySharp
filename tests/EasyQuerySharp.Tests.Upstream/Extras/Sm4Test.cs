using System.Text;
using EasyQuerySharp.Encryption;

namespace EasyQuerySharp.Tests.Upstream.Extras;

/// <summary>SM4 的自带实现：国标 GB/T 32907-2016 附录 A 的测试向量，以及 ECB + PKCS7 + 十六进制的往返（ROADMAP 5.5）。</summary>
public sealed class Sm4Test
{
    private static readonly byte[] StandardKey = Convert.FromHexString("0123456789abcdeffedcba9876543210");

    [Fact]
    public void MatchesTheStandardVector()
    {
        var cipher = Sm4.EncryptBlock(StandardKey, StandardKey);
        Assert.Equal("681edf34d206965e86b3e94f536e4246", Convert.ToHexStringLower(cipher));
        Assert.Equal(StandardKey, Sm4.DecryptBlock(cipher, StandardKey));
    }

    // 附录 A 例 2：同一密钥对明文加密 1,000,000 次
    [Fact]
    public void MatchesTheMillionRoundVector()
    {
        var block = StandardKey.ToArray();
        for (var i = 0; i < 1_000_000; i++) block = Sm4.EncryptBlock(block, StandardKey);
        Assert.Equal("595298c7c6fd271f0402f804c33d3f66", Convert.ToHexStringLower(block));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("浙江省绍兴市越城区城市广场旁边2-102")]
    [InlineData("emoji 😀 与 16 字节整倍数ab")]
    public void RoundTripsHexWithPkcs7(string text)
    {
        var key = Encoding.UTF8.GetBytes("sm4key1234567890");
        var hex = Sm4.EncryptToHex(text, key);
        Assert.Matches("^([0-9a-f]{32})+$", hex);
        Assert.Equal(text, Sm4.DecryptFromHex(hex, key));
    }
}
