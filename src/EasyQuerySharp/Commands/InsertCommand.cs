using System.Linq.Expressions;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>
/// INSERT。每个实体一条语句（与 Java 版一致），在同一连接上依次执行。默认只写值不为 null 的列；
/// 数据库生成的列（自增）不写，调用 <see cref="FillGeneratedKeys"/> 后插入完回填到实体。
/// 冲突处理（upsert）见 <see cref="OnConflictIgnore"/> 与 <see cref="OnConflictUpdate"/>。
/// </summary>
public sealed class InsertCommand<T> : EntityCommandBase<InsertCommand<T>, T> where T : class
{
    private readonly IReadOnlyList<T> _entities;
    private readonly Dictionary<string, string> _columnSql = new(StringComparer.Ordinal);
    private bool _fillKeys;
    private ColumnStrategy _strategy = ColumnStrategy.OnlyNotNull;
    private ConflictSpec? _conflict;

    internal InsertCommand(EasyDb db, IReadOnlyList<T> entities) : base(db) => _entities = entities;

    /// <summary>插入后把数据库生成的主键（自增）写回实体（对应 Java 的 executeRows(true)）。</summary>
    public InsertCommand<T> FillGeneratedKeys()
    {
        _fillKeys = true;
        return this;
    }

    /// <summary>写哪些列，默认只写非 null 列。</summary>
    public InsertCommand<T> Columns(ColumnStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    /// <summary>
    /// 这一列的值用 SQL 模板包一层（对应 Java 的 columnConfigure）：<c>{0}</c> 是列，<c>{1}</c> 是实体上的值，
    /// 如 <c>.ColumnSql(t => t.Id, "sde.next_rowid('sde',{1})")</c>。
    /// </summary>
    public InsertCommand<T> ColumnSql<TValue>(Expression<Func<T, TValue>> column, string template)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);
        var member = ExpressionTranslator.StripConvert(column.Body) as MemberExpression
                     ?? throw new ArgumentException($"需要直接的属性访问，如 t => t.Title：{column}", nameof(column));
        _columnSql[member.Member.Name] = template;
        return this;
    }

    /// <summary>
    /// 主键或唯一约束冲突时忽略这一行（对应 Java 的 onConflictThen(null)）：MySQL 的 <c>INSERT IGNORE</c>，
    /// PostgreSQL / SQLite 的 <c>ON CONFLICT [(列)] DO NOTHING</c>。<paramref name="conflictColumns"/> 指定约束列（MySQL 不需要）。
    /// </summary>
    public InsertCommand<T> OnConflictIgnore(Expression<Func<T, object?>>? conflictColumns = null)
    {
        _conflict = new ConflictSpec(Ignore: true, Names(conflictColumns), null);
        return this;
    }

    /// <summary>
    /// 冲突时改为更新这些列（对应 Java 的 onConflictThen(cols, keys)）：MySQL 的 <c>ON DUPLICATE KEY UPDATE c = VALUES(c)</c>，
    /// PostgreSQL / SQLite 的 <c>ON CONFLICT (约束列) DO UPDATE SET c = EXCLUDED.c</c>。
    /// <paramref name="updateColumns"/> 为 null 时更新全部写入的列；主键与约束列不会被更新。约束列默认是非自增的主键。
    /// </summary>
    /// <example><code>db.Insert(blog).OnConflictUpdate(b => new { b.Star, b.Content })</code></example>
    public InsertCommand<T> OnConflictUpdate(Expression<Func<T, object?>>? updateColumns = null, Expression<Func<T, object?>>? conflictColumns = null)
    {
        _conflict = new ConflictSpec(Ignore: false, Names(conflictColumns), updateColumns is null ? null : ExpressionTranslator.MemberNames(updateColumns));
        return this;
    }

    /// <summary>渲染第一个实体的语句，不执行（不调用拦截器）。</summary>
    public string ToSql() => ToStatement().Text;

    /// <summary>同 <see cref="ToSql"/>，连同参数。</summary>
    public SqlStatement ToStatement() => Render(_entities.Count > 0 ? _entities[0] : throw new InvalidOperationException("没有要插入的实体"));

    /// <summary>执行，返回受影响的行数（冲突更新时 MySQL 对更新的行计 2，与驱动的返回值一致）。</summary>
    public async Task<long> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        foreach (var e in _entities)
        {
            FillKeys(e);
            Db.Interceptors.BeforeWrite(e, insert: true, Interceptors);
        }
        // 分片实体按 NodeOf(实体) 落到各自的表与数据源
        var rendered = _entities.Select(e => (Entity: e, Statement: Render(e, routed: true))).ToList();
        return await ExecuteRoutedAsync(rendered, r => r.Statement, async (session, items) =>
        {
            long rows = 0;
            foreach (var batch in WriteBatches.Of(items, r => r.Statement, UseBatch))
            {
                List<SqlStatement> statements = [.. batch.Select(r => r.Statement)];
                if (FilledKey is not { } keyColumn)
                {
                    rows += (await session.ExecuteBatchAsync(statements, cancellationToken).ConfigureAwait(false)).Total;
                    continue;
                }
                var (affected, keys) = await session.InsertBatchAsync(statements, Db.Options.Dialect.LastInsertIdQuery, cancellationToken).ConfigureAwait(false);
                for (var i = 0; i < batch.Count; i++) keyColumn.Setter?.Invoke(batch[i].Entity, ValueReader.ConvertTo(keys[i], keyColumn.ClrType));
                rows += affected;
            }
            return rows;
        }, cancellationToken).ConfigureAwait(false);
    }

    // 需要回填的生成键列，不回填时为 null
    private ColumnMetadata? FilledKey => _fillKeys ? Entity.KeyColumns.FirstOrDefault(k => k.IsGenerated) : null;

    // 主键为 null 且配置了生成器时先生成主键（与 Java 一致：在执行之前写回实体）
    private void FillKeys(T entity)
    {
        foreach (var key in Entity.KeyColumns)
            if (key.KeyGenerator is { } generator && key.Getter(entity) is null) key.Setter?.Invoke(entity, generator.NewKey(key));
    }

    // routed 为 false 时写逻辑表名（ToSql），为 true 时分片实体写它落到的分表
    private SqlStatement Render(T entity, bool routed = false)
    {
        var table = NewTable();
        var written = new List<ColumnMetadata>();
        var columns = new List<ColumnExpr>();
        var values = new List<SqlExpr>();
        foreach (var column in Entity.Columns)
        {
            if (column.InsertIgnore) continue;
            // 由 SQL 生成值的列（如序列函数）总是写，值忽略
            if (column.GeneratedSql is { } generated)
            {
                written.Add(column);
                columns.Add(table.Column(column));
                values.Add(new FragmentExpr(generated, []));
                continue;
            }
            if (column.IsGenerated) continue;
            var value = column.GetProviderValue(entity);
            if (_strategy == ColumnStrategy.OnlyNotNull && value is null) continue;
            if (_strategy == ColumnStrategy.OnlyNull && value is not null) continue;
            var target = table.Column(column);
            var parameter = ExpressionTranslator.WriteValue(column, value);
            if (_columnSql.TryGetValue(column.PropertyName, out var template)) parameter = new FragmentExpr(template, [target, parameter]);
            written.Add(column);
            columns.Add(target);
            values.Add(parameter);
        }

        if (columns.Count == 0) throw new InvalidOperationException($"{typeof(T).Name} 的插入没有要写的列");
        var insert = new InsertStatement(table, columns, [values])
        {
            Returning = FilledKey is { } keyColumn && Db.Options.Dialect.LastInsertIdQuery is null ? [table.Column(keyColumn)] : [],
            Conflict = _conflict is null ? null : BuildConflict(table, written),
        };
        return routed
            ? Routed(table, [], [], [], entity, routes => SqlRenderer.Render(Db.Options.Dialect, insert, routes)).Single()
            : SqlRenderer.Render(Db.Options.Dialect, insert);
    }

    // 约束列：显式指定且写入了的（自增主键不写入，不算），否则是非自增主键（忽略时不指定就不写约束列）；更新列：写入的列去掉主键和约束列（与 Java 一致）
    private InsertConflict BuildConflict(EntityTableSource table, List<ColumnMetadata> written)
    {
        var spec = _conflict!;
        var conflictOn = spec.ConflictColumns is { } names
            ? Entity.Columns.Where(c => names.Contains(c.PropertyName) && written.Contains(c)).ToList()
            : spec.Ignore ? [] : Entity.KeyColumns.Where(k => !k.IsGenerated).ToList();
        if (spec.Ignore) return new InsertConflict(conflictOn.Select(table.Column).ToList(), null);
        var update = written.Where(c => !c.IsPrimaryKey && !conflictOn.Contains(c) && (spec.UpdateColumns is null || spec.UpdateColumns.Contains(c.PropertyName)));
        return new InsertConflict(conflictOn.Select(table.Column).ToList(), update.Select(table.Column).ToList());
    }

    private static List<string>? Names(Expression<Func<T, object?>>? columns) =>
        columns is null ? null : ExpressionTranslator.MemberNames(columns);

    private sealed record ConflictSpec(bool Ignore, List<string>? ConflictColumns, List<string>? UpdateColumns);
}
