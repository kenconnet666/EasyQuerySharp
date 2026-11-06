namespace EasyQuerySharp.Tests.Upstream.Queries;

/// <summary>对应 Java：QueryTest26.java 里 code-first 建表后读写的用例；其余用例见 <see cref="QueryTest26"/>。</summary>
public sealed class QueryTest26CodeFirst
{
    // Java：MySQLGenerateKey 的 name 列 @Column(dbDefault = "''")、now 列 @Column(dbDefault = "CURRENT_TIMESTAMP(3)", nullable = false)，
    // 删表后 syncTable 建表，插入只写 name，读回的 id 是 1、now 是库里填的当前时间。要等列默认值（dbDefault）的写法定下来
    [Fact(Skip = "等列默认值（Java 的 @Column(dbDefault)）定下来，见 ROADMAP 4.1"), Upstream("QueryTest26.java#testGenerateKeys", Pending = true)]
    public Task TestGenerateKeys() => Task.CompletedTask;
}
