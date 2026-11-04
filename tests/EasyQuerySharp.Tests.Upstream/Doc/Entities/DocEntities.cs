using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Doc.Entities;

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
