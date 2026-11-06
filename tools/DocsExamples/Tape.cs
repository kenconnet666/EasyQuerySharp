using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;

namespace EasyQuerySharp.Docs;

/// <summary>
/// 卡片执行的"磁带"：录制时包住真实的 SQLite 数据源，按顺序记下每条语句的结果；回放时不连库，
/// 按同样的顺序把记下的结果还给另一种方言发出的语句。这样每种方言都能走完整条卡片，记下它实际会发出的 SQL。
/// </summary>
internal sealed class Tape
{
    private readonly List<object?> _results = [];
    private readonly Tape? _recording;
    private int _next;

    private Tape(SqliteConnection? sqlite, Tape? recording)
    {
        Sqlite = sqlite;
        _recording = recording;
    }

    /// <summary>录制：结果来自 <paramref name="sqlite"/>。</summary>
    public static Tape Record(SqliteConnection sqlite) => new(sqlite, null);

    /// <summary>从头回放这卷录制（每次回放各自计数，可以给多个方言用）。</summary>
    public Tape Replay() => new(null, this);

    /// <summary>录制时的真实连接；回放时为 null。</summary>
    public SqliteConnection? Sqlite { get; }

    /// <summary>作为 EasyDb 的数据源。</summary>
    public DbDataSource DataSource => new TapeDataSource(this);

    internal object? Execute(SqliteCommand command, Func<SqliteCommand, object?> run)
    {
        if (_recording is null)
        {
            var result = run(command);
            _results.Add(result);
            return result;
        }
        if (_next >= _recording._results.Count)
            throw new InvalidOperationException("这个方言发出的语句比 SQLite 上多，回放的结果不够");
        return _recording._results[_next++];
    }

    private sealed class TapeDataSource(Tape tape) : DbDataSource
    {
        public override string ConnectionString => "tape";

        protected override DbConnection CreateDbConnection() => new TapeConnection(tape);
    }
}

/// <summary>磁带上的连接：录制时用真实的 SQLite 连接，回放时什么也不连。</summary>
[SuppressMessage("Reliability", "CA2213", Justification = "录制用的 SQLite 连接由卡片运行器统一释放")]
internal sealed class TapeConnection(Tape tape) : DbConnection
{
    private ConnectionState _state;

    [AllowNull]
    public override string ConnectionString { get; set; } = "tape";

    public override string Database => "tape";

    public override string DataSource => "tape";

    public override string ServerVersion => "tape";

    public override ConnectionState State => _state;

    public override void Open() => _state = ConnectionState.Open;

    public override void Close() => _state = ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName)
    {
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        new TapeTransaction(this, tape.Sqlite?.BeginTransaction(isolationLevel == IsolationLevel.Unspecified ? IsolationLevel.Serializable : isolationLevel));

    // 参数集合借用一个 SqliteCommand（回放时它挂在未打开的连接上，从不执行）
    protected override DbCommand CreateDbCommand() => new TapeCommand(tape, tape.Sqlite?.CreateCommand() ?? new SqliteCommand()) { Connection = this };
}

/// <summary>磁带上的事务：录制时包住真实事务。</summary>
internal sealed class TapeTransaction(DbConnection connection, SqliteTransaction? inner) : DbTransaction
{
    public SqliteTransaction? Inner => inner;

    public override IsolationLevel IsolationLevel => inner?.IsolationLevel ?? IsolationLevel.Unspecified;

    protected override DbConnection DbConnection => connection;

    public override void Commit() => inner?.Commit();

    public override void Rollback() => inner?.Rollback();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>磁带上的命令：执行结果都经过磁带（录制或回放）。</summary>
internal sealed class TapeCommand(Tape tape, SqliteCommand inner) : DbCommand
{
    private DbTransaction? _transaction;

    [AllowNull]
    public override string CommandText
    {
        get => inner.CommandText;
        set => inner.CommandText = value;
    }

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }

    protected override DbParameterCollection DbParameterCollection => inner.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _transaction;
        set
        {
            _transaction = value;
            inner.Transaction = (value as TapeTransaction)?.Inner;
        }
    }

    public override void Cancel()
    {
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => inner.CreateParameter();

    public override int ExecuteNonQuery() => (int)tape.Execute(inner, c => c.ExecuteNonQuery())!;

    public override object? ExecuteScalar() => tape.Execute(inner, c => c.ExecuteScalar());

    // 结果整个读进 DataTable，录制与回放都从它读
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var table = (DataTable)tape.Execute(inner, c =>
        {
            using var reader = c.ExecuteReader();
            return ReadTable(reader);
        })!;
        return table.CreateDataReader();
    }

    // 列的类型取第一个非 NULL 的值的类型：SQLite 的表达式列没有声明类型，第一行是 NULL 时 GetFieldType 报的是 byte[]，
    // DataTable.Load 按它建列，后面的整数就装不进去了
    private static DataTable ReadTable(SqliteDataReader reader)
    {
        var rows = new List<object[]>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }
        var table = new DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture };
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var type = rows.Select(r => r[i]).FirstOrDefault(v => v is not DBNull)?.GetType() ?? reader.GetFieldType(i);
            var name = reader.GetName(i);
            table.Columns.Add(table.Columns.Contains(name) ? name + "_" + i : name, type);
        }
        foreach (var row in rows) table.Rows.Add(row);
        return table;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
