using System.Data.Common;
using System.Text;

namespace EasyQuerySharp.TestKit.Databases;

/// <summary>执行建表脚本：按分号切成单条语句（引号、反引号、注释里的分号不算）。</summary>
public static class SqlScript
{
    /// <summary>把脚本切成语句，去掉空语句与纯注释。</summary>
    public static IReadOnlyList<string> Split(string script)
    {
        var statements = new List<string>();
        var sb = new StringBuilder();
        char? quote = null;
        for (var i = 0; i < script.Length; i++)
        {
            var c = script[i];
            if (quote is not null)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < script.Length) sb.Append(script[++i]);
                else if (c == quote) quote = null;
                continue;
            }
            if (c is '\'' or '"' or '`') quote = c;
            else if (c == '-' && i + 1 < script.Length && script[i + 1] == '-')
            {
                while (i < script.Length && script[i] != '\n') i++;
                continue;
            }
            else if (c == ';')
            {
                Flush();
                continue;
            }
            sb.Append(c);
        }
        Flush();
        return statements;

        void Flush()
        {
            var text = sb.ToString().Trim();
            if (text.Length > 0) statements.Add(text);
            sb.Clear();
        }
    }

    /// <summary>在连接上依次执行脚本里的语句；<paramref name="skip"/> 返回 true 的语句跳过。</summary>
    public static async Task ExecuteAsync(DbConnection connection, string script, Func<string, bool>? skip = null, CancellationToken ct = default)
    {
        foreach (var statement in Split(script))
        {
            if (skip?.Invoke(statement) == true) continue;
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            try
            {
                await command.ExecuteNonQueryAsync(ct);
            }
            catch (DbException ex)
            {
                throw new InvalidOperationException($"执行脚本语句失败：{ex.Message}{Environment.NewLine}{statement}", ex);
            }
        }
    }
}
