namespace EasyQuerySharp.Commands;

/// <summary>批量写入的分组（对应 Java 的 createBatchExecutionUnits）。</summary>
internal static class WriteBatches
{
    // 一批最多带的语句数，与 Java 的 BATCH_GROUP_COUNT 一致
    private const int MaxSize = 1000;

    /// <summary>
    /// 不批量时每条自成一批；批量时按 SQL 分组：组按首次出现的顺序执行，组内保持原顺序，每组最多 1000 条（与 Java 一致）。
    /// </summary>
    public static IEnumerable<IReadOnlyList<TItem>> Of<TItem>(IEnumerable<TItem> items, Func<TItem, SqlStatement> statementOf, bool batch) =>
        batch
            ? items.GroupBy(i => statementOf(i).CommandText, StringComparer.Ordinal).SelectMany(g => g.Chunk(MaxSize))
            : items.Select(i => (IReadOnlyList<TItem>)[i]);
}
