namespace EasyQuerySharp.Metadata;

/// <summary>表上的一个索引（由 <see cref="TableIndexAttribute"/> 解析）。</summary>
internal sealed record IndexMetadata(string Name, bool Unique, IReadOnlyList<(ColumnMetadata Column, bool Descending)> Columns, string? Comment);

/// <summary>对一导航上的外键（由 <see cref="ForeignKeyAttribute"/> 解析）：本表的列引用目标表的列。</summary>
internal sealed record ForeignKeyMetadata(
    string Name, EntityMetadata Entity, IReadOnlyList<ColumnMetadata> Columns, EntityMetadata Target, IReadOnlyList<ColumnMetadata> TargetColumns, string? Action);
