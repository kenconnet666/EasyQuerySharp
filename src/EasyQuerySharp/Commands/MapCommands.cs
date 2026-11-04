using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 按字典更新时，WHERE 列的值要改成新值：<c>["id"] = new MapValue("123", "1")</c> 渲染成 <c>SET `id` = '123' … WHERE `id` = '1'</c>（对应 Java 的 MapValue）。
/// </summary>
/// <param name="Value">要写入的新值。</param>
/// <param name="WhereValue">定位行用的旧值。</param>
public sealed record MapValue(object? Value, object? WhereValue);

/// <summary>字典写入的公共部分：目标表、写哪些列、列的 SQL 模板。键是数据库列名，按字典的枚举顺序写。</summary>
public abstract class MapCommandBase<TSelf> where TSelf : MapCommandBase<TSelf>
{
    private protected MapCommandBase(EasyDb db, string tableName, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, ColumnStrategy strategy)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        Db = db;
        Rows = rows;
        Strategy = strategy;
        // 没有实体类型：用只有表名的元数据承载表来源
        Table = new EntityTableSource(new EntityMetadata(typeof(IReadOnlyDictionary<string, object?>), tableName, null, null, []));
    }

    /// <summary>所属的 <see cref="EasyDb"/>。</summary>
    public EasyDb Db { get; }

    private protected IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; }

    private protected EntityTableSource Table { get; }

    private protected ColumnStrategy Strategy { get; private set; }

    private protected Dictionary<string, string> ColumnTemplates { get; } = new(StringComparer.Ordinal);

    private TSelf Self => (TSelf)this;

    /// <summary>写哪些列（对应 Java 的 setSQLStrategy）。</summary>
    public TSelf Columns(ColumnStrategy strategy)
    {
        Strategy = strategy;
        return Self;
    }

    /// <summary>这一列的值用 SQL 模板包一层：<c>{0}</c> 是列，<c>{1}</c> 是值（对应 Java 的 columnConfigure）。</summary>
    public TSelf ColumnSql(string column, string template)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);
        ColumnTemplates[column] = template;
        return Self;
    }

    /// <summary>渲染第一行的语句，不执行。</summary>
    public string ToSql() => Render(Rows.Count > 0 ? Rows[0] : throw new InvalidOperationException("没有要写入的行")).Text;

    /// <summary>执行，返回受影响的总行数（每行一条语句，在同一连接上依次执行）。</summary>
    public Task<long> ExecuteAsync(CancellationToken cancellationToken = default) =>
        Db.Executor.SessionAsync(async session =>
        {
            long total = 0;
            foreach (var row in Rows) total += await session.ExecuteAsync(Render(row), cancellationToken).ConfigureAwait(false);
            return total;
        }, cancellationToken);

    private protected abstract SqlStatement Render(IReadOnlyDictionary<string, object?> row);

    private protected bool Skip(object? value) =>
        (Strategy == ColumnStrategy.OnlyNotNull && value is null) || (Strategy == ColumnStrategy.OnlyNull && value is not null);

    private protected ColumnExpr Column(string name) => new(Table, name);

    private protected SqlExpr Value(string column, object? value)
    {
        SqlExpr parameter = new ParameterExpr(value, value?.GetType() ?? typeof(object));
        return ColumnTemplates.TryGetValue(column, out var template) ? new FragmentExpr(template, [Column(column), parameter]) : parameter;
    }
}

/// <summary>按字典插入（对应 Java 的 mapInsertable）：<c>db.InsertMap("t_user", row)</c>。默认只写非 null 的列。</summary>
public sealed class MapInsertCommand : MapCommandBase<MapInsertCommand>
{
    internal MapInsertCommand(EasyDb db, string tableName, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
        : base(db, tableName, rows, ColumnStrategy.OnlyNotNull)
    {
    }

    private protected override SqlStatement Render(IReadOnlyDictionary<string, object?> row)
    {
        var columns = new List<ColumnExpr>();
        var values = new List<SqlExpr>();
        foreach (var (name, value) in row)
        {
            if (Skip(value)) continue;
            columns.Add(Column(name));
            values.Add(Value(name, value));
        }
        return SqlRenderer.Render(Db.Options.Dialect, new InsertStatement(Table, columns, [values]));
    }
}

/// <summary>
/// 按字典更新（对应 Java 的 mapUpdatable）：<c>db.UpdateMap("t_user", row).WhereColumns("id")</c>。
/// 默认写字典里除 WHERE 列外的全部列；WHERE 列的值是 <see cref="MapValue"/> 时同时改写它。
/// </summary>
public sealed class MapUpdateCommand : MapCommandBase<MapUpdateCommand>
{
    private string[] _whereColumns = [];

    internal MapUpdateCommand(EasyDb db, string tableName, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
        : base(db, tableName, rows, ColumnStrategy.All)
    {
    }

    /// <summary>用这些列定位行（必填）；列名区分大小写，字典里必须有。</summary>
    public MapUpdateCommand WhereColumns(params string[] columns)
    {
        _whereColumns = columns;
        return this;
    }

    private protected override SqlStatement Render(IReadOnlyDictionary<string, object?> row)
    {
        if (_whereColumns.Length == 0) throw new InvalidOperationException("按字典更新必须用 WhereColumns 指定定位列");
        var update = new UpdateStatement(Table);
        foreach (var (name, raw) in row)
        {
            var isWhere = _whereColumns.Contains(name, StringComparer.Ordinal);
            if (isWhere && raw is not MapValue) continue;
            var value = raw is MapValue change ? change.Value : raw;
            if (Skip(value)) continue;
            update.Sets.Add((Column(name), Value(name, value)));
        }
        foreach (var name in _whereColumns)
        {
            if (!row.TryGetValue(name, out var raw)) throw new InvalidOperationException($"WHERE 列 {name} 在字典里不存在");
            var value = raw is MapValue change ? change.WhereValue : raw;
            update.Where.Add(value is null
                ? new IsNullExpr(Column(name), false)
                : new BinaryExpr(Column(name), SqlBinaryOperator.Equal, new ParameterExpr(value, value.GetType())));
        }
        if (update.Sets.Count == 0) throw new InvalidOperationException("按字典更新没有要写的列");
        return SqlRenderer.Render(Db.Options.Dialect, update);
    }
}
