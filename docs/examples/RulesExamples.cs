using EasyQuerySharp.Encryption;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Samples;

namespace EasyQuerySharp.Docs;

/// <summary>"列规则"页：逻辑删除、乐观锁、多租户等查询过滤、写入前填值、列加密。用示例模型的文档（Doc）与工单（Ticket）。</summary>
public static class RulesExamples
{
    // ---- 逻辑删除

    [Example("rules", "逻辑删除：查询自动加上未删除的条件")]
    public static Task<List<string>> LogicDeleteQuery(EasyDb db) =>
        db.Query<Doc>().OrderBy(d => d.Id).Select(d => d.Title).ToListAsync();

    [Example("rules", "查出已删除的行：DisableLogicDelete")]
    public static Task<long> LogicDeleteDisabled(EasyDb db) =>
        db.Query<Doc>().DisableLogicDelete().CountAsync();

    [Example("rules", "按条件删除也改写成 UPDATE；要真删时用 Hard()")]
    public static async Task<long> HardDelete(EasyDb db)
    {
        await db.Delete<Doc>().Where(d => d.Title == "接入指南").IgnoreVersion().ExecuteAsync();
        return await db.Delete<Doc>().Where(d => d.Title == "接入指南").IgnoreVersion().Hard().ExecuteAsync();
    }

    // <snippet:rules-logic-delete>
    [Table("t_audit_log")]
    public class AuditLog
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [LogicDelete(LogicDeleteStrategy.DeletedTime)]
        public DateTime? DeletedAt { get; set; }              // NULL 表示未删除，删除时写当前时间
    }
    // </snippet:rules-logic-delete>

    // ---- 乐观锁

    [Example("rules", "乐观锁：按实体更新时带上版本号，版本号加一；影响 0 行时抛 ConcurrencyException")]
    public static async Task<long> VersionUpdate(EasyDb db)
    {
        var doc = await db.Query<Doc>().FindOrThrowAsync(1L);
        doc.Title = "接入指南（第二版）";
        return await db.Update(doc).ExecuteAsync(1, "文档已被别人修改，请刷新后再试");
    }

    [Example("rules", "按条件更新要说明版本：WithVersion 或 IgnoreVersion")]
    public static async Task<long> VersionCondition(EasyDb db)
    {
        await db.Update<Doc>().Set(d => d.Title, "改个标题").Where(d => d.Id == 1).WithVersion(1).ExecuteAsync();
        return await db.Update<Doc>().Set(d => d.Title, "管理端修正").Where(d => d.Id == 1).IgnoreVersion().ExecuteAsync();
    }

    // ---- 拦截器

    [Example("rules", "查询过滤：工单只看 1 号租户，更新、删除同样带上")]
    public static async Task<List<string>> TenantFilter(EasyDb db)
    {
        await db.Delete<Ticket>().Where(t => t.Title == "登录不上").ExecuteAsync();
        return await db.Query<Ticket>().OrderBy(t => t.Id).Select(t => t.Title).ToListAsync();
    }

    [Example("rules", "按名字关掉某个拦截器")]
    public static Task<long> DisableInterceptor(EasyDb db) =>
        db.Query<Ticket>().DisableInterceptor(nameof(SampleDb.TenantFilter)).CountAsync();

    [Example("rules", "写入前填值：插入时补租户，按条件更新时追加 SET")]
    public static async Task<long> FillBeforeWrite(EasyDb db)
    {
        await db.Insert(new Ticket { Title = "新工单", Contact = "13800000009" }).ExecuteAsync();
        return await db.Update<Ticket>().Set(t => t.Title, "发票抬头写错了（已处理）").Where(t => t.Id == 1).ExecuteAsync();
    }

    // ---- 列加密

    [Example("rules", "加密列：写入加密、读出解密，等值条件按密文比较")]
    public static Task<List<string>> EncryptedColumn(EasyDb db) =>
        db.Query<Ticket>().Where(t => t.Contact == "13800000001").OrderBy(t => t.Id).Select(t => t.Title).ToListAsync();

    // <snippet:rules-encryption>
    /// <summary>手机号用 AES 加密、Base64 存储，与 Java 版的密文兼容；密钥从环境变量读，不写进代码。</summary>
    public sealed class PhoneEncryption : AesBase64EncryptionStrategy
    {
        protected override string Key => Environment.GetEnvironmentVariable("PHONE_AES_KEY")!;

        protected override string Iv => Environment.GetEnvironmentVariable("PHONE_AES_IV")!;
    }

    [Table("t_member")]
    public class Member
    {
        [Column(PrimaryKey = true, Generated = true)]
        public long Id { get; set; }

        [Encryption(typeof(PhoneEncryption), SupportQueryLike = true)]
        public string? Phone { get; set; }                    // 分段加密：Phone.Contains("1380") 也能查
    }
    // </snippet:rules-encryption>
}
