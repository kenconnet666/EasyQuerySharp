using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Entities;

/// <summary>对应 Java：doc/MySignUp（报名；按 (comId, userId) 复合键关联到企业用户）。</summary>
[Table("my_sign_up")]
public class MySignUp
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ComId { get; set; }

    public string? UserId { get; set; }

    public DateTime? Time { get; set; }

    public string? Content { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ComId), nameof(UserId)], TargetProperties = [nameof(MyComUser.ComId), nameof(MyComUser.UserId)])]
    public MyComUser? ComUser { get; set; }

    /// <summary>直达：经 ComUser 到它的企业。</summary>
    [Navigate(RelationType.ManyToOne, DirectMapping = [nameof(ComUser), nameof(MyComUser.MyCompany)])]
    public MyCompany? ComUserCompany { get; set; }
}

/// <summary>对应 Java：doc/MyComUser（我在企业的信息）。</summary>
[Table("my_com_user")]
public class MyComUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? ComId { get; set; }

    public string? UserId { get; set; }

    public string? Gw { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(ComId), nameof(UserId)], TargetProperties = [nameof(MySignUp.ComId), nameof(MySignUp.UserId)])]
    public List<MySignUp>? MySignUps { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(ComId)])]
    public MyCompany? MyCompany { get; set; }

    [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(UserId)])]
    public MyUser? MyUser { get; set; }
}

/// <summary>对应 Java：doc/MyCompany。</summary>
[Table("my_company_info")]
public class MyCompany
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>对应 Java：doc/MyUser。</summary>
[Table("my_user")]
public class MyUser
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }
}
