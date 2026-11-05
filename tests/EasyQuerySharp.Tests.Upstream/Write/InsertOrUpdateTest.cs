using EasyQuerySharp.Commands;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>对应 Java：InsertOrUpdateTest.java 与 QueryTest14.testxxxxx（SQL Server / Oracle 的冲突处理写成 MERGE）。</summary>
public sealed class InsertOrUpdateTest
{
    /// <summary>对应 Java：entity/InsertOrUpdateMultiKeyEntity（复合主键）。</summary>
    [Table("t_user")]
    public sealed class InsertOrUpdateMultiKeyEntity
    {
        [Column(PrimaryKey = true)]
        public string? Column1 { get; set; }

        [Column(PrimaryKey = true)]
        public string? Column2 { get; set; }

        public string? Column3 { get; set; }

        public string? Column4 { get; set; }

        public string? Column5 { get; set; }
    }

    // 按 column2 冲突时更新其余非主键列
    [Fact, Upstream("InsertOrUpdateTest.java#test1")]
    public void Test1() =>
        SqlOnly.AssertStatement(SqlOnly.Db("mssql").Insert(new InsertOrUpdateMultiKeyEntity { Column1 = "1", Column2 = "2", Column3 = "3", Column4 = "4", Column5 = "5" })
                .Columns(ColumnStrategy.All).OnConflictUpdate(conflictColumns: o => o.Column2).ToStatement(),
            "MERGE INTO [t_user] t1 USING (SELECT ? AS [column1],? AS [column2],? AS [column3],? AS [column4],? AS [column5] ) t2 ON (t1.[column2] = t2.[column2]) WHEN MATCHED THEN UPDATE SET t1.[column3] = t2.[column3],t1.[column4] = t2.[column4],t1.[column5] = t2.[column5] WHEN NOT MATCHED THEN INSERT ([column1],[column2],[column3],[column4],[column5]) VALUES (t2.[column1],t2.[column2],t2.[column3],t2.[column4],t2.[column5]);",
            "1(String),2(String),3(String),4(String),5(String)");

    // Oracle：从 DUAL 取值；约束列是全部列（按列名排序），要更新的列都在约束里，只剩不存在时插入
    [Fact, Upstream("QueryTest14.java#testxxxxx")]
    public void Testxxxxx() =>
        SqlOnly.AssertStatement(SqlOnly.Db("oracle").Insert(new Topic()).Columns(ColumnStrategy.All)
                .OnConflictUpdate(o => o.Stars, o => new { o.Id, o.Stars, o.Title, o.CreateTime }).ToStatement(),
            "MERGE INTO \"t_topic\" t1 USING (SELECT ? AS \"id\",? AS \"stars\",? AS \"title\",? AS \"create_time\" FROM DUAL ) t2 ON (t1.\"create_time\" = t2.\"create_time\" AND t1.\"id\" = t2.\"id\" AND t1.\"stars\" = t2.\"stars\" AND t1.\"title\" = t2.\"title\") WHEN NOT MATCHED THEN INSERT (\"id\",\"stars\",\"title\",\"create_time\") VALUES (t2.\"id\",t2.\"stars\",t2.\"title\",t2.\"create_time\")",
            "null(null),null(null),null(null),null(null)");
}
