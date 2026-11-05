namespace EasyQuerySharp;

public static partial class Sql
{
    /// <summary>
    /// 把一列（JSON 文本、jsonb，或用 <see cref="Metadata.JsonValueConverter{T}"/> 存成 JSON 的对象）当作 JSON 文档取值（对应 Java 的 asJSONObject / asJSONArray）：
    /// <c>Sql.Json(t.ExtraJson).Text("code") == "200"</c>、<c>Sql.Json(t.Items).Get(0).Text("name")</c>、<c>Sql.Json(t.Items).Length() &gt; 1</c>。
    /// MySQL 写 <c>JSON_EXTRACT</c> / <c>JSON_UNQUOTE</c>，PG 写 <c>-&gt;</c> / <c>-&gt;&gt;</c>（列须是 json / jsonb），SQLite 写 <c>json_extract</c>。
    /// </summary>
    public static JsonNode Json(object? document) => throw OnlyInQuery();
}

/// <summary>
/// JSON 文档里的一个节点，只能写在查询表达式里（由 <see cref="Sql.Json"/> 得到）。键与下标直接写进 SQL，必须是常量。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "标记方法：只在表达式树里出现，不会被调用")]
public sealed class JsonNode
{
    private JsonNode()
    {
    }

    /// <summary>对象的成员，仍是 JSON（Java 的 getJSONObject / getJSONArray）：MySQL <c>JSON_EXTRACT(x, '$.key')</c>，PG <c>(x -&gt; 'key')</c>。</summary>
    public JsonNode Get(string key) => throw Sql.OnlyInQuery();

    /// <summary>数组的第 <paramref name="index"/> 个元素（从 0 开始），仍是 JSON：MySQL <c>JSON_EXTRACT(x,'$[0]')</c>，PG <c>(x -&gt; 0)</c>。</summary>
    public JsonNode Get(int index) => throw Sql.OnlyInQuery();

    /// <summary>成员的文本值（字符串去掉引号，Java 的 getField / getString）：MySQL <c>JSON_UNQUOTE(JSON_EXTRACT(x, '$.key'))</c>，PG <c>(x -&gt;&gt; 'key')</c>。</summary>
    public string? Text(string key) => throw Sql.OnlyInQuery();

    /// <summary>数组元素的文本值。</summary>
    public string? Text(int index) => throw Sql.OnlyInQuery();

    /// <summary>
    /// 成员转成 <typeparamref name="T"/>（Java 的 getBoolean / getInteger / getLong / getBigDecimal）：字符串同 <see cref="Text(string)"/>，
    /// 其余类型是 <c>CAST(成员 AS 类型)</c>。可空写 <c>Value&lt;int?&gt;("age")</c>。
    /// </summary>
    public T Value<T>(string key) => throw Sql.OnlyInQuery();

    /// <summary>数组元素转成 <typeparamref name="T"/>。</summary>
    public T Value<T>(int index) => throw Sql.OnlyInQuery();

    /// <summary>数组长度（Java 的 asJSONArray().size()）：MySQL <c>JSON_LENGTH(x)</c>，PG <c>JSONB_ARRAY_LENGTH(x)</c>。</summary>
    public int Length() => throw Sql.OnlyInQuery();

    /// <summary>对象是否有这个键（Java 的 containsKey）：MySQL <c>JSON_CONTAINS_PATH(x, 'one', '$.key')</c>，PG <c>JSONB_EXISTS(x, 'key')</c>。</summary>
    public bool ContainsKey(string key) => throw Sql.OnlyInQuery();
}
