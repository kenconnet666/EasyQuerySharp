using System.Data.Common;
using System.Globalization;
using EasyQuerySharp.Dialects;

namespace EasyQuerySharp.Schema;

/// <summary>执行方言的 <see cref="CatalogQueries"/>，把四组平铺的行拼成 <see cref="DbTableInfo"/>（同 TS 版的 assembleTables）。</summary>
internal static class SchemaReader
{
    public static async Task<IReadOnlyList<DbTableInfo>> ReadAsync(EasyDb db, CancellationToken ct)
    {
        var dialect = db.Options.Dialect;
        var queries = dialect.CatalogQueries ?? throw new NotSupportedException($"{dialect.Name} 方言不支持读取库结构");
        var tables = await RowsAsync(db, queries.Tables, ct).ConfigureAwait(false);
        var columns = await RowsAsync(db, queries.Columns, ct).ConfigureAwait(false);
        var indexes = await RowsAsync(db, queries.Indexes, ct).ConfigureAwait(false);
        var foreignKeys = queries.ForeignKeys is null ? [] : await RowsAsync(db, queries.ForeignKeys, ct).ConfigureAwait(false);

        var columnsOf = columns.ToLookup(r => Text(r, "table_name"));
        var indexesOf = indexes.ToLookup(r => Text(r, "table_name"));
        var foreignKeysOf = foreignKeys.ToLookup(r => Text(r, "table_name"));
        return tables.Select(t =>
        {
            var name = Text(t, "table_name")!;
            var tableColumns = columnsOf[name].ToList();
            return new DbTableInfo(
                name,
                Blank(Text(t, "comment")),
                tableColumns.Select(c => new DbColumnInfo(Text(c, "column_name")!, Text(c, "column_type") ?? "", Flag(c, "is_nullable"),
                    Text(c, "column_default"), Blank(Text(c, "comment")), Flag(c, "is_generated"))).ToList(),
                tableColumns.Where(c => Flag(c, "primary_key")).Select(c => Text(c, "column_name")!).ToList(),
                Grouped(indexesOf[name], "index_name").Select(g => new DbIndexInfo(g.Key, g.Select(r => Text(r, "column_name")!).ToList(), Flag(g.First(), "is_unique"))).ToList(),
                Grouped(foreignKeysOf[name], "constraint_name").Select(g => new DbForeignKeyInfo(g.Key, g.Select(r => Text(r, "column_name")!).ToList(),
                    Text(g.First(), "referenced_table")!, g.Select(r => Text(r, "referenced_column")!).ToList())).ToList());
        }).ToList();
    }

    private static Task<List<Dictionary<string, object?>>> RowsAsync(EasyDb db, string sql, CancellationToken ct) =>
        db.Executor.QueryAsync(new SqlStatement(sql, sql, []), ReadRow, ct);

    // 列名不分大小写：Oracle、Db2 不加引号的别名读出来是大写
    private static Dictionary<string, object?> ReadRow(DbDataReader reader)
    {
        var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        return row;
    }

    // 按名字分组，保持第一次出现的顺序（查询已按位置排好）
    private static IEnumerable<IGrouping<string, Dictionary<string, object?>>> Grouped(IEnumerable<Dictionary<string, object?>> rows, string key) =>
        rows.GroupBy(r => Text(r, key)!);

    private static string? Text(Dictionary<string, object?> row, string column) =>
        row.GetValueOrDefault(column) switch
        {
            null => null,
            string s => s,
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            var value => Convert.ToString(value, CultureInfo.InvariantCulture),
        };

    private static string? Blank(string? text) => string.IsNullOrEmpty(text) ? null : text;

    // 各库的真假：布尔、0 / 1、'YES' / 'NO'、'Y' / 'N'、't' / 'f'
    private static bool Flag(Dictionary<string, object?> row, string column) =>
        row.GetValueOrDefault(column) switch
        {
            null => false,
            bool b => b,
            string s => s.Trim().ToUpperInvariant() is "YES" or "Y" or "1" or "T" or "TRUE",
            var value => Convert.ToDecimal(value, CultureInfo.InvariantCulture) != 0,
        };
}
