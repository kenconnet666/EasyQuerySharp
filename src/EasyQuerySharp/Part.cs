using EasyQuerySharp.Metadata;

namespace EasyQuerySharp;

// 实体加上若干计算列（对应 Java 的 Select.PART.of(t, …)），多用于窗口函数：先投影成 Part，再按计算列过滤。
// 计算列在 SQL 里叫 __part__column1 …（与 Java 一致）；外层用 p.Entity.X / p.Value1 引用。
// 同样的事也可以用匿名类型 new { b, Rn = … } 做，列名按成员名。

/// <summary>实体加一个计算列：<c>.Select(b =&gt; new Part&lt;Blog, long&gt;(b, Sql.Over.RowNumber(…))).Where(p =&gt; p.Value1 == 1)</c>。</summary>
public sealed record Part<TEntity, T1>(TEntity Entity, [property: Column("__part__column1")] T1 Value1);

/// <summary>实体加两个计算列。</summary>
public sealed record Part<TEntity, T1, T2>(TEntity Entity, [property: Column("__part__column1")] T1 Value1, [property: Column("__part__column2")] T2 Value2);

/// <summary>实体加三个计算列。</summary>
public sealed record Part<TEntity, T1, T2, T3>(
    TEntity Entity, [property: Column("__part__column1")] T1 Value1, [property: Column("__part__column2")] T2 Value2, [property: Column("__part__column3")] T3 Value3);

/// <summary>实体加四个计算列。</summary>
public sealed record Part<TEntity, T1, T2, T3, T4>(
    TEntity Entity, [property: Column("__part__column1")] T1 Value1, [property: Column("__part__column2")] T2 Value2, [property: Column("__part__column3")] T3 Value3,
    [property: Column("__part__column4")] T4 Value4);
