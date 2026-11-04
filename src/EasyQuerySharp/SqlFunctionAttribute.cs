namespace EasyQuerySharp;

/// <summary>
/// 把一个静态方法（可以是扩展方法）声明成 SQL 模板函数，对应 Java 里用 rawSQLCommand / rawSQLStatement 封装的自定义函数。
/// 方法只是表达式里的标记，不会被调用；翻译时按 <see cref="Template"/> 展开，<c>{0}</c>、<c>{1}</c>… 依次是方法的参数
/// （列照写，值参数化）；返回 bool 的方法作为条件使用。
/// </summary>
/// <example><code>
/// [SqlFunction("FIND_IN_SET({0},{1})")]
/// public static bool FindInSet(string value, string? set) =&gt; throw new InvalidOperationException("只能在查询里使用");
///
/// db.Query&lt;Blog&gt;().Where(b =&gt; FindInSet("123", b.Content))   // WHERE FIND_IN_SET(?,`content`)
/// </code></example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlFunctionAttribute(string template) : Attribute
{
    /// <summary>SQL 模板，<c>{n}</c> 是第 n 个参数。</summary>
    public string Template { get; } = template;
}
