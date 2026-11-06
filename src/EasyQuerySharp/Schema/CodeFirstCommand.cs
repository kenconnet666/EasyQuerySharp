namespace EasyQuerySharp.Schema;

/// <summary>
/// 一组 code-first 语句（对应 Java 的 CodeFirstCommand）：先用 <see cref="ToSql"/> 审阅，再 <see cref="ExecuteAsync"/>。
/// 由 <see cref="CodeFirst.PlanSyncAsync(IEnumerable{Type}, CancellationToken)"/> 得出。
/// </summary>
/// <example><code>
/// var sync = await db.CodeFirst.PlanSyncAsync(typeof(Topic), typeof(Blog));
/// Console.WriteLine(sync.ToSql());
/// foreach (var difference in sync.Differences) Console.WriteLine(difference);
/// await sync.ExecuteAsync();
/// </code></example>
public sealed class CodeFirstCommand
{
    private readonly EasyDb _db;
    // 每条语句在哪个数据源上执行（分库的实体按节点所在的数据源）；null 是默认数据源
    private readonly IReadOnlyList<string?> _dataSources;

    internal CodeFirstCommand(EasyDb db, IReadOnlyList<(string? DataSource, string Sql)> statements, IReadOnlyList<string> differences)
    {
        _db = db;
        Statements = [.. statements.Select(s => s.Sql)];
        _dataSources = [.. statements.Select(s => s.DataSource)];
        Differences = differences;
    }

    /// <summary>要依次执行的语句；一条可能含几句（如 PostgreSQL 的建表语句后面接着注释），作为一次命令执行。</summary>
    public IReadOnlyList<string> Statements { get; }

    /// <summary>
    /// 同步不会自动处理的差异，每条一句话：库里多出来的列、可空性不一致、不能给已有的表加外键等。
    /// 同步只增不删、不改类型，这些差异由人决定怎么迁移。
    /// </summary>
    public IReadOnlyList<string> Differences { get; }

    /// <summary>全部语句，一条一行（与 Java 版 <c>getSQL()</c> 的语句文本一致，只是开头不带换行）；没有要执行的语句时是空串。</summary>
    public string ToSql() => string.Join("\n", Statements);

    /// <summary>
    /// 依次执行全部语句（在环境事务里时加入它；要整体回滚请自己开事务，MySQL、Oracle 的 DDL 会隐式提交）。
    /// 分库的实体的语句在各节点所在的数据源上执行。
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < Statements.Count; i++)
            await _db.Executor.ExecuteAsync(new SqlStatement(Statements[i], Statements[i], []) { DataSource = _dataSources[i] }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override string ToString() => ToSql();
}
