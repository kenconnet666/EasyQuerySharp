using System.Globalization;
using EasyQuerySharp.Execution;

namespace EasyQuerySharp.TestKit;

/// <summary>
/// 捕获执行过的 SQL（对应 Java 测试里的 ListenerContext）。注册为 EasyDb 的监听器后，
/// 用 <see cref="Listen"/> 开一个作用域，作用域内（同一异步流程）执行的语句都会记下来，并行测试互不干扰。
/// </summary>
/// <example><code>
/// using var listen = SqlCapture.Listen();
/// await db.Query&lt;Topic&gt;().WhereById("1").FirstOrDefaultAsync();
/// listen.AssertSql("SELECT `id`,`stars`,`title`,`create_time` FROM `t_topic` WHERE `id` = ? LIMIT 1");
/// listen.AssertParams("1(String)");
/// </code></example>
public sealed class SqlCapture : IExecutionListener
{
    private static readonly AsyncLocal<Scope?> Current = new();

    /// <summary>共享实例，注册到测试用的 EasyDb 上。</summary>
    public static SqlCapture Instance { get; } = new();

    /// <summary>开一个捕获作用域。</summary>
    public static Scope Listen() => Current.Value = new Scope(Current.Value);

    void IExecutionListener.OnExecuted(SqlExecutionEvent e) => Current.Value?.Add(e);

    /// <summary>一个捕获作用域；释放时恢复外层作用域。</summary>
    public sealed class Scope : IDisposable
    {
        private readonly Scope? _outer;
        private readonly List<SqlExecutionEvent> _events = [];
        private readonly Lock _lock = new();

        internal Scope(Scope? outer) => _outer = outer;

        /// <summary>捕获到的全部执行，按顺序。</summary>
        public IReadOnlyList<SqlExecutionEvent> Events
        {
            get
            {
                lock (_lock) return [.. _events];
            }
        }

        /// <summary>最后一次执行。</summary>
        public SqlExecutionEvent Last => Events.Count > 0 ? Events[^1] : throw new InvalidOperationException("没有捕获到任何 SQL");

        /// <summary>断言最后一条 SQL（<c>?</c> 占位）。</summary>
        public void AssertSql(string expected) => Assert.Equal(expected, Last.Statement.Text);

        /// <summary>断言第 <paramref name="index"/> 条 SQL。</summary>
        public void AssertSql(int index, string expected) => Assert.Equal(expected, Events[index].Statement.Text);

        /// <summary>断言最后一条 SQL 的参数，格式同 Java 的 <c>EasySQLUtil.sqlParameterToString</c>：<c>123xxx(String),1(Integer)</c>。</summary>
        public void AssertParams(string expected) => Assert.Equal(expected, JavaParams.Format(Last.Statement));

        /// <summary>断言第 <paramref name="index"/> 条 SQL 的参数。</summary>
        public void AssertParams(int index, string expected) => Assert.Equal(expected, JavaParams.Format(Events[index].Statement));

        internal void Add(SqlExecutionEvent e)
        {
            lock (_lock) _events.Add(e);
        }

        /// <inheritdoc />
        public void Dispose() => Current.Value = _outer;
    }
}

/// <summary>按 Java 版的格式输出参数文本，使 Java 用例里的期望字符串可以原样使用。</summary>
public static class JavaParams
{
    /// <summary>格式化一条语句的全部参数：<c>值(Java 类型名)</c>，逗号分隔。</summary>
    public static string Format(SqlStatement statement) =>
        string.Join(",", statement.Parameters.Select(p => Format(p.Value, p.ClrType)));

    /// <summary>格式化一个参数。</summary>
    public static string Format(object? value, Type clrType)
    {
        if (value is null) return "null(null)";
        return $"{Text(value)}({JavaTypeName(value.GetType())})";
    }

    private static string Text(object value) => value switch
    {
        bool b => b ? "true" : "false",
        DateTime d => JavaLocalDateTime(d),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    // Java LocalDateTime.toString()：秒与小数都为 0 时只到分钟，小数为 0 时到秒，否则带小数（去掉末尾的 0）
    private static string JavaLocalDateTime(DateTime d)
    {
        var fraction = d.Ticks % TimeSpan.TicksPerSecond;
        if (d.Second == 0 && fraction == 0) return d.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        var text = d.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        return fraction == 0 ? text : text + "." + (fraction * 100).ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
    }

    /// <summary>C# 类型对应的 Java 类型名。</summary>
    public static string JavaTypeName(Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var t when t == typeof(string) => "String",
        var t when t == typeof(int) => "Integer",
        var t when t == typeof(long) => "Long",
        var t when t == typeof(short) => "Short",
        var t when t == typeof(byte) => "Byte",
        var t when t == typeof(bool) => "Boolean",
        var t when t == typeof(decimal) => "BigDecimal",
        var t when t == typeof(double) => "Double",
        var t when t == typeof(float) => "Float",
        var t when t == typeof(DateTime) => "LocalDateTime",
        var t when t == typeof(DateOnly) => "LocalDate",
        var t when t == typeof(TimeOnly) => "LocalTime",
        var t when t == typeof(Guid) => "UUID",
        var t when t == typeof(char) => "Character",
        var t when t == typeof(DateTimeOffset) => "OffsetDateTime",
        var t => t.Name,
    };
}
