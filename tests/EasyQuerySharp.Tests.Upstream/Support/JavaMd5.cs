using System.Security.Cryptography;
using System.Text;

namespace EasyQuerySharp.Tests.Upstream.Support;

/// <summary>
/// Java 用例的 MD5Util.getMD5Hash：UTF-8 文本的 MD5，小写十六进制。code-first 的用例断言的是建表 SQL 的 MD5，
/// 这里只用来比对文本，不涉及安全。Java 的 <c>CodeFirstCommand.getSQL()</c> 在每条语句前加一个换行，用 <see cref="OfCodeFirst"/>。
/// </summary>
internal static class JavaMd5
{
#pragma warning disable CA5351 // 比对 Java 用例里的文本摘要，不是加密用途
    public static string Of(string text) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text)));
#pragma warning restore CA5351

    /// <summary>Java 的 <c>getSQL()</c>：每条语句前加一个换行再拼起来。</summary>
    public static string OfCodeFirst(Schema.CodeFirstCommand command) => Of(string.Concat(command.Statements.Select(s => "\n" + s)));
}
