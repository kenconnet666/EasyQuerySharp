using System.Collections.Immutable;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>
/// INSERT。每个实体一条语句（与 Java 版一致），在同一连接上依次执行。默认只写值不为 null 的列；
/// 数据库生成的列（自增）不写，调用 <see cref="FillGeneratedKeys"/> 后插入完回填到实体。
/// </summary>
public sealed class InsertCommand<T> where T : class
{
    private readonly EasyDb _db;
    private readonly IReadOnlyList<T> _entities;
    private readonly EntityMetadata _entity;
    private bool _fillKeys;
    private ColumnStrategy _strategy = ColumnStrategy.OnlyNotNull;
    private ImmutableHashSet<string> _disabled = [];
    private string? _tableName;
    private readonly Dictionary<string, string> _columnSql = new(StringComparer.Ordinal);

    internal InsertCommand(EasyDb db, IReadOnlyList<T> entities)
    {
        _db = db;
        _entities = entities;
        _entity = db.Metadata.Get<T>();
    }

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

    /// <summary>不应用名为 <paramref name="name"/> 的拦截器。</summary>
    public InsertCommand<T> DisableInterceptor(string name)
    {
        _disabled = _disabled.Add(name);
        return this;
    }

    /// <summary>不应用任何拦截器。</summary>
    public InsertCommand<T> DisableInterceptors() => DisableInterceptor("*");

    /// <summary>插入到另一张同结构的表。</summary>
    public InsertCommand<T> AsTable(string tableName)
    {
        _tableName = tableName;
        return this;
    }

    /// <summary>
    /// 这一列的值用 SQL 模板包一层（对应 Java 的 columnConfigure）：<c>{0}</c> 是列，<c>{1}</c> 是实体上的值，
    /// 如 <c>.ColumnSql(t => t.Id, "sde.next_rowid('sde',{1})")</c>。
    /// </summary>
    public InsertCommand<T> ColumnSql<TValue>(System.Linq.Expressions.Expression<Func<T, TValue>> column, string template)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);
        var member = ExpressionTranslator.StripConvert(column.Body) as System.Linq.Expressions.MemberExpression
                     ?? throw new ArgumentException($"需要直接的属性访问，如 t => t.Title：{column}", nameof(column));
        _columnSql[member.Member.Name] = template;
        return this;
    }

    /// <summary>渲染第一个实体的语句，不执行（不调用拦截器）。</summary>
    public string ToSql() => Render(_entities[0]).Statement.Text;

    /// <summary>执行，返回插入的行数。</summary>
    public async Task<long> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        foreach (var e in _entities) _db.Interceptors.BeforeWrite(e, insert: true, _disabled);
        return await _db.Executor.SessionAsync(async session =>
        {
            long rows = 0;
            foreach (var entity in _entities)
            {
                var (statement, keyColumn) = Render(entity);
                if (keyColumn is null)
                {
                    rows += await session.ExecuteAsync(statement, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                object? key;
                if (_db.Options.Dialect.LastInsertIdQuery is { } query)
                {
                    rows += await session.ExecuteAsync(statement, cancellationToken).ConfigureAwait(false);
                    key = await session.ScalarAsync(new SqlStatement(query, query, []), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    key = await session.ScalarAsync(statement, cancellationToken).ConfigureAwait(false);
                    rows++;
                }
                keyColumn.Setter?.Invoke(entity, ValueReader.ConvertTo(key, keyColumn.ClrType));
            }
            return rows;
        }, cancellationToken).ConfigureAwait(false);
    }

    // 返回语句，以及需要回填的生成键列（不需要时为 null）
    private (SqlStatement Statement, ColumnMetadata? KeyColumn) Render(T entity)
    {
        var table = new EntityTableSource(_entity) { TableNameOverride = _tableName };
        var columns = new List<ColumnExpr>();
        var values = new List<SqlExpr>();
        foreach (var column in _entity.Columns)
        {
            if (column.InsertIgnore || column.IsGenerated) continue;
            var value = column.GetProviderValue(entity);
            if (_strategy == ColumnStrategy.OnlyNotNull && value is null) continue;
            if (_strategy == ColumnStrategy.OnlyNull && value is not null) continue;
            var target = table.Column(column);
            SqlExpr parameter = ExpressionTranslator.Parameter(value, column.Converter?.ProviderType ?? column.ClrType);
            if (_columnSql.TryGetValue(column.PropertyName, out var template)) parameter = new FragmentExpr(template, [target, parameter]);
            columns.Add(target);
            values.Add(parameter);
        }

        var keyColumn = _fillKeys ? _entity.KeyColumns.FirstOrDefault(k => k.IsGenerated) : null;
        var insert = new InsertStatement(table, columns, [values])
        {
            Returning = keyColumn is not null && _db.Options.Dialect.LastInsertIdQuery is null ? [table.Column(keyColumn)] : [],
        };
        return (SqlRenderer.Render(_db.Options.Dialect, insert), keyColumn);
    }
}
