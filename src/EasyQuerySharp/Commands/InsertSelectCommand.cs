using EasyQuerySharp.Querying;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 把查询结果整批写进一张表（INSERT … SELECT，对应 Java fork 的 InsertSelect）：来源查询的各列写进目标表的同名列，
/// 可以再追加固定值的列。目标表默认是实体自己的表，<c>InsertInto("归档表")</c> 换成别的表。
/// </summary>
/// <example><code>
/// await db.Query&lt;Post&gt;().Where(p =&gt; p.UserId == "u1")
///     .InsertInto("t_post_archive").Column("archived_at", DateTime.Now).ExecuteAsync();
/// </code></example>
public sealed class InsertSelectCommand<T>
{
    private readonly QueryState _source;
    private readonly string? _table;
    private readonly List<(string Name, SqlExpr Value)> _extra = [];

    internal InsertSelectCommand(QueryState source, string? table)
    {
        _source = source;
        _table = table;
    }

    /// <summary>再写一列固定值（列名按数据库里的写法）。</summary>
    public InsertSelectCommand<T> Column(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _extra.Add((name, ExpressionTranslator.Parameter(value, value?.GetType() ?? typeof(object))));
        return this;
    }

    /// <summary>生成的语句。</summary>
    public SqlStatement ToStatement()
    {
        var table = new EntityTableSource(_source.Db.Metadata.Get<T>()) { TableNameOverride = _table };
        var statement = new InsertSelectStatement(table, QueryCompiler.CompileSql(_source), [.. _extra.Select(e => e.Name)], [.. _extra.Select(e => e.Value)]);
        return SqlRenderer.Render(_source.Db.Options.Dialect, statement);
    }

    /// <summary>生成的 SQL 文本。</summary>
    public string ToSql() => ToStatement().Text;

    /// <summary>执行，返回写入的行数。目标表或来源查询是分片实体时不支持（各节点的行要分别写进各自的表）。</summary>
    public Task<long> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var db = _source.Db;
        if ((_table is null && db.Shards.RuleOf(typeof(T)) is not null) || Sharding.ShardPlanner.HasSharded(db, QueryCompiler.CompileSql(_source)))
            throw new NotSupportedException($"INSERT … SELECT 不支持分片实体（{typeof(T).Name}）：查出来后用 db.Insert(..) 写入，各行按分片键落到各自的表");
        return db.Executor.ExecuteAsync(ToStatement(), cancellationToken);
    }
}
