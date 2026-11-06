namespace EasyQuerySharp.Tests.Upstream.Dto;

/// <summary>对应 Java：doc/dto/BankCardVO（银行卡的列，再加用户名与银行名）。</summary>
public class BankCardVO
{
    public string? Id { get; set; }

    public string? Uid { get; set; }

    /// <summary>银行卡号。</summary>
    public string? Code { get; set; }

    /// <summary>银行卡类型：借记卡、储蓄卡。</summary>
    public string? Type { get; set; }

    public string? UserName { get; set; }

    public string? BankName { get; set; }
}

/// <summary>对应 Java：doc/dto/DocUserVO（用户的列，再加银行卡数）。</summary>
public class DocUserVO
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Phone { get; set; }

    public int? Age { get; set; }

    public long? CardCount { get; set; }
}
