using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：doc/entity/DocBank。</summary>
[Table("doc_bank")]
public class DocBank
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(DocBankCard.BankId)])]
    public List<DocBankCard>? BankCards { get; set; }
}

/// <summary>对应 Java：doc/entity/DocBankCard。</summary>
[Table("doc_bank_card")]
public class DocBankCard
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Code { get; set; }

    public string? Type { get; set; }

    public string? BankId { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(Uid)])]
    public DocUser? User { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(BankId)], Required = true)]
    public DocBank? Bank { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(BankId)])]
    public DocBank? Bank2 { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(DocPart.CardId)])]
    public List<DocPart>? Parts { get; set; }
}

/// <summary>对应 Java：doc/entity/DocPart。</summary>
[Table("doc_part")]
public class DocPart
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? CardId { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：doc/entity/DocUser。</summary>
[Table("doc_user")]
public class DocUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Phone { get; set; }

    public int? Age { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(DocBankCard.Uid)])]
    public List<DocBankCard>? BankCards { get; set; }

    [Navigate(RelationType.OneToMany, TargetProperties = [nameof(DocUserBook.Uid)])]
    public List<DocUserBook>? UserBooks { get; set; }
}

/// <summary>对应 Java：doc/entity/DocUserBook。</summary>
[Table("doc_user_book")]
public class DocUserBook
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Name { get; set; }

    public decimal? Price { get; set; }
}

/// <summary>对应 Java：entity/m2m/UserAccount（Java 写成 ManyToMany 但没有中间表，实际是按 uid 的一对多）。</summary>
[Table("t_user_account")]
public class UserAccount
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Uid)], TargetProperties = [nameof(UserBook.Uid)])]
    public List<UserBook>? Books { get; set; }
}

/// <summary>对应 Java：entity/m2m/UserBook。</summary>
[Table("t_user_book")]
public class UserBook
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Name { get; set; }

    public decimal? Price { get; set; }
}

/// <summary>对应 Java：doc/DocBlog（表 t_blog 的另一套列，与库里的表结构不一致，只为拿到出错的 SQL）。</summary>
[Table("t_blog")]
public class DocBlog
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Title { get; set; }

    public string? Topic { get; set; }

    public string? Url { get; set; }

    public int? Star { get; set; }

    public DateTime? PublishTime { get; set; }

    public decimal? Score { get; set; }

    public int? Status { get; set; }
}

/// <summary>对应 Java：doc/TestSelf（自关联的上级，库里没有这张表）。</summary>
[Table("test_self")]
public class TestSelf
{
    public string? Id { get; set; }

    public string? Pid { get; set; }

    public string? Uid { get; set; }

    public string? Name { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(Pid)], TargetProperties = [nameof(Id)])]
    public TestSelf? Parent { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(Uid)])]
    public MyUser? MyUser { get; set; }
}

/// <summary>对应 Java：doc/TopicSelfVO（两列沿导航平铺，Java 的 @NavigateJoin 写成 JOIN）。</summary>
public class TopicSelfVO
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    [MapPath("MyUser.Name")]
    public string? Uname1 { get; set; }

    [MapPath("Parent.MyUser.Name")]
    public string? Uname2 { get; set; }
}

/// <summary>对应 Java：doc/entity/SysUser（文档示例用的用户，表名常被 asTable 换掉）。</summary>
[Table("t_sys_user")]
public class DocSysUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Account { get; set; }

    public string? DepartName { get; set; }

    public string? Phone { get; set; }

    public DateTime? CreateTime { get; set; }
}
