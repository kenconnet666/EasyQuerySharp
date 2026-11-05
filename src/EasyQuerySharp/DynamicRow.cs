using System.Linq.Expressions;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Querying;

namespace EasyQuerySharp;

/// <summary>
/// 无实体查询的一行（对应 Java 的 Map 查询）：列名 → 值，列名忽略大小写。
/// 查询里用 <see cref="Get{T}"/> 按列名引用列，读出后同样用它取值并转换类型；Map 投影、原生 SQL 读成字典时得到的也是它。
/// </summary>
/// <example><code>
/// var rows = await db.Table("t_topic").Where(t => t.Get&lt;string&gt;("id") == "123").ToListAsync();   // SELECT * FROM `t_topic` WHERE `id` = ?
/// var title = rows[0].Get&lt;string&gt;("title");
/// await db.Table("t_topic").Select(t => new DynamicRow { { "id", t.Get&lt;string&gt;("id") } }).ToListAsync();   // SELECT `id` AS `id` FROM `t_topic`
/// </code></example>
#pragma warning disable CA1710 // 名字表达"一行"，不以 Dictionary 结尾
public sealed class DynamicRow : Dictionary<string, object?>
#pragma warning restore CA1710
{
    /// <summary>空行。</summary>
    public DynamicRow() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>预留 <paramref name="capacity"/> 列的空行。</summary>
    public DynamicRow(int capacity) : base(capacity, StringComparer.OrdinalIgnoreCase)
    {
    }

    /// <summary>
    /// 列 <paramref name="column"/> 的值，转换成 <typeparamref name="T"/>；查询表达式里翻译成这一列（<c>`column`</c>）。
    /// 读出的行里没有这一列时抛出 <see cref="KeyNotFoundException"/>；值是 NULL 时返回 <c>default</c>。
    /// </summary>
    public T Get<T>(string column) =>
        TryGetValue(column, out var value)
            ? ValueReader.ConvertTo(value, typeof(T)) is T converted ? converted : default!
            : throw new KeyNotFoundException($"这一行里没有列 {column}");
}

/// <summary><see cref="EasyDb.Table"/> 查询上的便捷写法。</summary>
public static class DynamicRowQueries
{
    private static readonly System.Reflection.MethodInfo GetObject = typeof(DynamicRow).GetMethod(nameof(DynamicRow.Get))!.MakeGenericMethod(typeof(object));
    private static readonly System.Reflection.MethodInfo Add = typeof(DynamicRow).GetMethod(nameof(DynamicRow.Add), [typeof(string), typeof(object)])!;

    /// <summary>
    /// 只选这些列，每列写 <c>`列` AS `列`</c>（对应 Java Map 查询的 column("..")）；列名可以来自运行时的列表：
    /// <c>db.Table("aaa").SelectColumns("id", "age")</c> → <c>SELECT `id` AS `id`,`age` AS `age` FROM `aaa`</c>。
    /// </summary>
    public static Query<DynamicRow> SelectColumns(this Query<DynamicRow> query, params IEnumerable<string> columns)
    {
        ArgumentNullException.ThrowIfNull(query);
        var row = Expression.Parameter(typeof(DynamicRow), "t");
        var body = Expression.ListInit(Expression.New(typeof(DynamicRow)),
            columns.Select(c => Expression.ElementInit(Add, Expression.Constant(c), Expression.Call(row, GetObject, Expression.Constant(c)))));
        return query.Select(Expression.Lambda<Func<DynamicRow, DynamicRow>>(body, row));
    }
}
