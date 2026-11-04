namespace EasyQuerySharp;

// 临时结果类型（对应 Java 的 Draft1..Draft10）：投影 new Draft<…>(a, b) 时列别名是 value1、value2…，与 Java 版一致。
// 一般写法用匿名类型或 record 即可，需要与 Java 的 SQL 逐字对齐或跨方法传递结果时用 Draft。

/// <summary>一列的临时结果。</summary>
public sealed record Draft<T1>(T1 Value1);

/// <summary>两列的临时结果。</summary>
public sealed record Draft<T1, T2>(T1 Value1, T2 Value2);

/// <summary>三列的临时结果。</summary>
public sealed record Draft<T1, T2, T3>(T1 Value1, T2 Value2, T3 Value3);

/// <summary>四列的临时结果。</summary>
public sealed record Draft<T1, T2, T3, T4>(T1 Value1, T2 Value2, T3 Value3, T4 Value4);

/// <summary>五列的临时结果。</summary>
public sealed record Draft<T1, T2, T3, T4, T5>(T1 Value1, T2 Value2, T3 Value3, T4 Value4, T5 Value5);

/// <summary>六列的临时结果。</summary>
public sealed record Draft<T1, T2, T3, T4, T5, T6>(T1 Value1, T2 Value2, T3 Value3, T4 Value4, T5 Value5, T6 Value6);

/// <summary>七列的临时结果。</summary>
public sealed record Draft<T1, T2, T3, T4, T5, T6, T7>(T1 Value1, T2 Value2, T3 Value3, T4 Value4, T5 Value5, T6 Value6, T7 Value7);

/// <summary>八列的临时结果。</summary>
public sealed record Draft<T1, T2, T3, T4, T5, T6, T7, T8>(T1 Value1, T2 Value2, T3 Value3, T4 Value4, T5 Value5, T6 Value6, T7 Value7, T8 Value8);

/// <summary>九列的临时结果。</summary>
public sealed record Draft<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
    T1 Value1, T2 Value2, T3 Value3, T4 Value4, T5 Value5, T6 Value6, T7 Value7, T8 Value8, T9 Value9);

/// <summary>十列的临时结果。</summary>
public sealed record Draft<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
    T1 Value1, T2 Value2, T3 Value3, T4 Value4, T5 Value5, T6 Value6, T7 Value7, T8 Value8, T9 Value9, T10 Value10);
