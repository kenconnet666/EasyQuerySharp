using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Dialects;

// 插入冲突（upsert）的写法：ON DUPLICATE KEY / ON CONFLICT，或整条改写成 MERGE
public abstract partial class SqlDialect
{
    /// <summary>冲突处理改写成整条 MERGE（SQL Server、Oracle）；为 false 时在 INSERT 后面写 <see cref="WriteInsertConflict"/>。</summary>
    public virtual bool InsertConflictUsesMerge => false;

    /// <summary>MERGE 的来源查询在列之后的部分（Oracle 要 <c>FROM DUAL</c>）。</summary>
    protected virtual string MergeSourceSuffix => " ";

    /// <summary>MERGE 语句的结尾（SQL Server 要求分号）。</summary>
    protected virtual string MergeTerminator => "";

    /// <summary>
    /// 带冲突处理的单行插入写成 MERGE（与 Java 的 MsSQL / Oracle 插入一致）：
    /// <c>MERGE INTO 表 t1 USING (SELECT ? AS c1,… ) t2 ON (t1.k = t2.k AND …) [WHEN MATCHED THEN UPDATE SET t1.c = t2.c,…]
    /// WHEN NOT MATCHED THEN INSERT (c1,…) VALUES (t2.c1,…)</c>。约束列按列名排序，没有指定时取非自增的主键（自增主键不写入，不能作约束，与 Java 一致）；
    /// 没有要更新的列时不写 WHEN MATCHED。来源查询写不成 SELECT 的库（Db2 要 <c>VALUES</c>）覆盖。
    /// </summary>
    public virtual void WriteMerge(SqlRenderer renderer, InsertStatement insert)
    {
        if (insert.Rows.Count != 1) throw new NotSupportedException($"{Name} 的冲突处理（MERGE）一次只能插入一行，请逐条执行");
        var conflict = insert.Conflict!;
        var keys = MergeKeys(insert);
        renderer.Append("MERGE INTO ").WriteTableName(insert.Table).Append(" t1 USING (SELECT ");
        for (var i = 0; i < insert.Columns.Count; i++)
        {
            if (i > 0) renderer.Append(",");
            renderer.Write(insert.Rows[0][i]).Append(" AS ").Append(QuoteIdentifier(insert.Columns[i].Name));
        }
        renderer.Append(MergeSourceSuffix).Append(") t2 ON (")
            .Append(string.Join(" AND ", keys.Select(k => $"t1.{QuoteIdentifier(k)} = t2.{QuoteIdentifier(k)}"))).Append(")");
        if (conflict.UpdateColumns is { Count: > 0 } updates)
            renderer.Append(" WHEN MATCHED THEN UPDATE SET ")
                .Append(string.Join(",", updates.Select(c => $"t1.{QuoteIdentifier(c.Name)} = t2.{QuoteIdentifier(c.Name)}")));
        renderer.Append(" WHEN NOT MATCHED THEN INSERT (").Append(string.Join(",", insert.Columns.Select(c => QuoteIdentifier(c.Name))))
            .Append(") VALUES (").Append(string.Join(",", insert.Columns.Select(c => "t2." + QuoteIdentifier(c.Name)))).Append(")").Append(MergeTerminator);
    }

    /// <summary>MERGE 的约束列名（按列名排序）：指定了 conflictColumns 时用它们，否则取主键。</summary>
    protected IReadOnlyList<string> MergeKeys(InsertStatement insert)
    {
        var conflict = insert.Conflict!;
        // 自增主键不写入，不能作约束（与 Java 一致）
        var keys = (conflict.ConflictColumns.Count > 0
                ? conflict.ConflictColumns.Select(c => c.Name)
                : insert.Table.Entity.KeyColumns.Where(c => !c.IsGenerated).Select(c => c.Name))
            .Order(StringComparer.Ordinal).ToList();
        return keys.Count > 0 ? keys : throw new InvalidOperationException($"{Name} 的冲突处理需要约束列：{insert.Table.Entity.Type.Name} 没有非自增的主键时请指定 conflictColumns");
    }

    /// <summary>INSERT 的开头关键字；MySQL 忽略冲突时是 <c>INSERT IGNORE INTO </c>。</summary>
    public virtual string InsertKeyword(InsertStatement insert) => "INSERT INTO ";

    /// <summary>
    /// 写 VALUES 之后的冲突处理。默认是 PostgreSQL / SQLite 的写法（与 Java 的 PostgresSQLInsertSQLExpression 一致）：
    /// <c>ON CONFLICT (k) DO NOTHING</c>、<c>ON CONFLICT (k) DO UPDATE SET `c` = EXCLUDED.`c`</c>；没有要更新的列时不写。
    /// </summary>
    public virtual void WriteInsertConflict(SqlRenderer renderer, InsertStatement insert)
    {
        var conflict = insert.Conflict!;
        if (conflict.Ignore)
        {
            renderer.Append(" ON CONFLICT");
            if (conflict.ConflictColumns.Count > 0) WriteConflictTarget(renderer, conflict);
            renderer.Append(" DO NOTHING");
            return;
        }
        if (conflict.UpdateColumns!.Count == 0) return;
        if (conflict.ConflictColumns.Count == 0) throw new InvalidOperationException("冲突更新需要约束列：实体没有非自增主键时请指定 conflictColumns");
        renderer.Append(" ON CONFLICT");
        WriteConflictTarget(renderer, conflict);
        renderer.Append(" DO UPDATE SET ");
        for (var i = 0; i < conflict.UpdateColumns.Count; i++)
        {
            var name = QuoteIdentifier(conflict.UpdateColumns[i].Name);
            renderer.Append(i == 0 ? "" : ", ").Append(name).Append(" = EXCLUDED.").Append(name);
        }
    }

    private void WriteConflictTarget(SqlRenderer renderer, InsertConflict conflict) =>
        renderer.Append(" (").Append(string.Join(",", conflict.ConflictColumns.Select(c => QuoteIdentifier(c.Name)))).Append(")");
}
