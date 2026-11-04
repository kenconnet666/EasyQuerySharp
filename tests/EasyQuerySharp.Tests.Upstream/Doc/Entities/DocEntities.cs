using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Doc.Entities;

/// <summary>对应 Java：doc/entity/DocBank。</summary>
[Table("doc_bank")]
public class DocBank
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

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

    public DocUser? User { get; set; }

    public DocBank? Bank { get; set; }

    public DocBank? Bank2 { get; set; }

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

    public List<DocBankCard>? BankCards { get; set; }

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
