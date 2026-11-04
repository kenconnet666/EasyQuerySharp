using System.Text;

namespace EasyQuerySharp.Metadata;

/// <summary>由类名、属性名推出表名、列名的命名约定。</summary>
public abstract class NameConversion
{
    /// <summary>默认约定：驼峰转下划线小写，<c>CreateTime</c> → <c>create_time</c>（与 Java 版默认一致）。</summary>
    public static NameConversion Underlined { get; } = new UnderlinedNameConversion();

    /// <summary>名字原样使用。</summary>
    public static NameConversion AsIs { get; } = new AsIsNameConversion();

    /// <summary>把一个 C# 名字转成数据库名字。</summary>
    public abstract string Convert(string name);

    private sealed class UnderlinedNameConversion : NameConversion
    {
        public override string Convert(string name)
        {
            var sb = new StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && name[i - 1] != '_') sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }

    private sealed class AsIsNameConversion : NameConversion
    {
        public override string Convert(string name) => name;
    }
}
