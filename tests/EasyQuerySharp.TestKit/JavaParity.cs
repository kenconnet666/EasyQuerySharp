namespace EasyQuerySharp.TestKit;

/// <summary>对标 Java 用例的 EasyDb 配置。</summary>
public static class JavaParity
{
    /// <summary>按 SQL 原样比较 NULL（<see cref="EasyDbOptions.UseRelationalNulls"/>）：Java 不补 IS NULL 判断，期望 SQL 才能逐字对齐。</summary>
    public static EasyDbOptions WithJavaNulls(this EasyDbOptions options)
    {
        options.UseRelationalNulls = true;
        return options;
    }
}
