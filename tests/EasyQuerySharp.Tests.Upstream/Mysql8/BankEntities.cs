using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>对应 Java：mysql8/entity/bank/SysBank。</summary>
[Table("t_bank")]
public class SysBank
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(SysBankCard.BankId)])]
    public List<SysBankCard>? BankCards { get; set; }

    [Navigate(RelationType.ManyToMany, SelfProperties = [nameof(Id)], SelfMappingProperties = [nameof(SysBankCard.BankId)],
        Mapping = typeof(SysBankCard), TargetProperties = [nameof(SysUser.Id)], TargetMappingProperties = [nameof(SysBankCard.Uid)])]
    public List<SysUser>? Users { get; set; }
}

/// <summary>对应 Java：mysql8/entity/bank/SysBankCard。</summary>
[Table("t_bank_card")]
public class SysBankCard
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Code { get; set; }

    public string? Type { get; set; }

    public string? BankId { get; set; }

    public DateTime? OpenTime { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(BankId)], TargetProperties = [nameof(SysBank.Id)], Required = true)]
    public SysBank? Bank { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(Uid)], TargetProperties = [nameof(SysUser.Id)])]
    public SysUser? User { get; set; }
}

/// <summary>对应 Java：mysql8/entity/bank/SysUser。</summary>
[Table("t_sys_user")]
public class SysUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Phone { get; set; }

    public int? Age { get; set; }

    public DateTime? CreateTime { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(SysBankCard.Uid)], PartitionOrder = PartitionOrder.None)]
    public List<SysBankCard>? BankCards { get; set; }

    /// <summary>默认合并成分组 JOIN 的银行卡（Java 的 subQueryToGroupJoin = true）。</summary>
    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(SysBankCard.Uid)], GroupJoin = true)]
    public List<SysBankCard>? BankCards2 { get; set; }

    /// <summary>同上且必有（Java 的 required = true）：取某一张时用 INNER JOIN。</summary>
    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(SysBankCard.Uid)], GroupJoin = true, Required = true)]
    public List<SysBankCard>? BankCards3 { get; set; }


    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(SysUserBook.Uid)], PartitionOrder = PartitionOrder.None)]
    public List<SysUserBook>? UserBooks { get; set; }

    /// <summary>按开户时间、卡号（NULL 在前）倒序排的银行卡（Java 的 orderByProps + PartitionOrderEnum.NAVIGATE）。</summary>
    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Id)], TargetProperties = [nameof(SysBankCard.Uid)],
        OrderBy = "OpenTime, Code desc nulls first", PartitionOrder = PartitionOrder.Navigation)]
    public List<SysBankCard>? BankCard4s { get; set; }
}

/// <summary>对应 Java：mysql8/entity/bank/SysUserBook。</summary>
[Table("t_sys_user_book")]
public class SysUserBook
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Uid { get; set; }

    public decimal? Price { get; set; }
}
