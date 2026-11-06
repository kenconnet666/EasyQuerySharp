using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.H2;

/// <summary>
/// 对应 Java：h2/domain/ALLTYPE 与 ALLTYPE1（同一张表，后者只是不用 Lombok、手写访问器，C# 合成一个）。
/// Java 的类型对应：Byte → sbyte（Java 的 byte 有符号），java.util.Date / java.sql.Date → DateTime，java.sql.Time → TimeSpan，UUID → Guid；
/// 基本类型（float、int、boolean …）对应非可空的值类型。
/// </summary>
[Table("t_all_type")]
public class ALLTYPE
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public decimal? NumberDecimal { get; set; }

    public float? NumberFloat { get; set; }

    public double? NumberDouble { get; set; }

    public short? NumberShort { get; set; }

    public int? NumberInteger { get; set; }

    public long? NumberLong { get; set; }

    public sbyte? NumberByte { get; set; }

    public DateTime? TimeLocalDateTime { get; set; }

    public DateOnly? TimeLocalDate { get; set; }

    public TimeOnly? TimeLocalTime { get; set; }

    public DateTime? OnlyDate { get; set; }

    public DateTime? SqlDate { get; set; }

    public TimeSpan? OnlyTime { get; set; }

    public bool? Enable { get; set; }

    public string? Value { get; set; }

    public Guid? Uid { get; set; }

    public float NumberFloatBasic { get; set; }

    public double NumberDoubleBasic { get; set; }

    public short NumberShortBasic { get; set; }

    public int NumberIntegerBasic { get; set; }

    public long NumberLongBasic { get; set; }

    public sbyte NumberByteBasic { get; set; }

    public bool EnableBasic { get; set; }

    /// <summary>与 Java 用例相同的一行：各类型的取值固定，<see cref="OnlyDate"/> 取当前时间（截到毫秒，同 Java 的 Date），<see cref="SqlDate"/> 取今天。</summary>
    public static ALLTYPE Sample(string id)
    {
        var now = DateTime.Now;
        return new ALLTYPE
        {
            Id = id,
            NumberDecimal = 12.33m,
            NumberFloat = 12.3f,
            NumberDouble = 22.1d,
            NumberShort = 12,
            NumberInteger = 33,
            NumberLong = 12345678911L,
            NumberByte = -1,
            Enable = true,
            TimeLocalDateTime = new DateTime(2021, 1, 1, 0, 0, 0),
            TimeLocalDate = new DateOnly(2121, 1, 2),
            TimeLocalTime = new TimeOnly(21, 1, 9),
            OnlyDate = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond)),
            SqlDate = now.Date,
            OnlyTime = new TimeSpan(12, 9, 10),
            Value = "3322",
            Uid = Guid.NewGuid(),
            NumberFloatBasic = 12.3f,
            NumberDoubleBasic = 22.1d,
            NumberShortBasic = 12,
            NumberIntegerBasic = 33,
            NumberLongBasic = 12345678911L,
            NumberByteBasic = -1,
            EnableBasic = true,
        };
    }
}

/// <summary>对应 Java：h2/vo/ALLTYPEVO1（与 ALLTYPE 同名属性的 VO，按属性自动映射）。</summary>
public class ALLTYPEVO1
{
    public string? Id { get; set; }

    public decimal? NumberDecimal { get; set; }

    public float? NumberFloat { get; set; }

    public double? NumberDouble { get; set; }

    public short? NumberShort { get; set; }

    public int? NumberInteger { get; set; }

    public long? NumberLong { get; set; }

    public sbyte? NumberByte { get; set; }

    public DateTime? TimeLocalDateTime { get; set; }

    public DateOnly? TimeLocalDate { get; set; }

    public TimeOnly? TimeLocalTime { get; set; }

    public DateTime? OnlyDate { get; set; }

    public DateTime? SqlDate { get; set; }

    public TimeSpan? OnlyTime { get; set; }

    public bool? Enable { get; set; }

    public string? Value { get; set; }

    public Guid? Uid { get; set; }

    public float NumberFloatBasic { get; set; }

    public double NumberDoubleBasic { get; set; }

    public short NumberShortBasic { get; set; }

    public int NumberIntegerBasic { get; set; }

    public long NumberLongBasic { get; set; }

    public sbyte NumberByteBasic { get; set; }

    public bool EnableBasic { get; set; }
}

/// <summary>对应 Java：h2/domain/DefTable。</summary>
[Table("t_def_table")]
public class DefTable
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserName { get; set; }

    public string? Nickname { get; set; }

    public bool? Enable { get; set; }

    public decimal? Score { get; set; }

    public string? Mobile { get; set; }

    public string? Avatar { get; set; }

    public int? Number { get; set; }

    public int? Status { get; set; }

    public DateTime? Created { get; set; }

    public string? Options { get; set; }
}

/// <summary>对应 Java：h2/domain/DefTableLeft1。</summary>
[Table("t_def_table_left1")]
public class DefTableLeft1
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? DefId { get; set; }

    public string? UserName { get; set; }

    public string? Nickname { get; set; }

    public bool? Enable { get; set; }

    public decimal? Score { get; set; }

    public string? Mobile { get; set; }

    public string? Avatar { get; set; }

    public int? Number { get; set; }

    public int? Status { get; set; }

    public DateTime? Created { get; set; }

    public string? Options { get; set; }
}

/// <summary>对应 Java：h2/domain/DefTableLeft2。</summary>
[Table("t_def_table_left2")]
public class DefTableLeft2
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? DefId { get; set; }

    public string? Def1Id { get; set; }

    public string? UserName { get; set; }

    public string? Nickname { get; set; }

    public bool? Enable { get; set; }

    public decimal? Score { get; set; }

    public string? Mobile { get; set; }

    public string? Avatar { get; set; }

    public int? Number { get; set; }

    public int? Status { get; set; }

    public DateTime? Created { get; set; }

    public string? Options { get; set; }
}

/// <summary>对应 Java：h2/domain/DefTableLeft3。</summary>
[Table("t_def_table_left3")]
public class DefTableLeft3
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? DefId { get; set; }

    public string? Def1Id { get; set; }

    public string? Def2Id { get; set; }

    public string? UserName { get; set; }

    public string? Nickname { get; set; }

    public bool? Enable { get; set; }

    public decimal? Score { get; set; }

    public string? Mobile { get; set; }

    public string? Avatar { get; set; }

    public int? Number { get; set; }

    public int? Status { get; set; }

    public DateTime? Created { get; set; }

    public string? Options { get; set; }
}

/// <summary>
/// 对应 Java：h2/domain/TbOrder。Java 把两个导航写成没有中间表的 ManyToMany（selfProperty = targetProperty = uid），
/// 实际是按 uid 的一对多，C# 照实写成 OneToMany；myAccounts 的固定条件对应 Java 的 NameXMTbAccountExtraFilter（uname = '小明'）。
/// </summary>
[Table("t_tb_order")]
public class TbOrder
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Uname { get; set; }

    public decimal? Price { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Uid)], TargetProperties = [nameof(TbAccount.Uid)])]
    public List<TbAccount>? Accounts { get; set; }

    [Navigate(RelationType.OneToMany, SelfProperties = [nameof(Uid)], TargetProperties = [nameof(TbAccount.Uid)], ExtraFilter = nameof(NameXMTbAccountExtraFilter))]
    public List<TbAccount>? MyAccounts { get; set; }

    private static Expression<Func<TbAccount, bool>> NameXMTbAccountExtraFilter => a => a.Uname == "小明";
}

/// <summary>对应 Java：h2/domain/TbAccount（反向导航 orders 没有用例用到，省略）。</summary>
[Table("t_tb_account")]
public class TbAccount
{
    [Column(PrimaryKey = true)]
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Uname { get; set; }

    public string? Account { get; set; }
}

/// <summary>对应 Java：h2/domain/TbOrderDTO（结构化 DTO，带出 myAccounts）。</summary>
public class TbOrderDTO
{
    public string? Id { get; set; }

    public string? Uid { get; set; }

    public string? Uname { get; set; }

    public decimal? Price { get; set; }

    public List<InternalMyAccounts>? MyAccounts { get; set; }

    /// <summary>对应 Java：TbOrderDTO.InternalMyAccounts。</summary>
    public class InternalMyAccounts
    {
        public string? Id { get; set; }

        public string? Uid { get; set; }

        public string? Uname { get; set; }

        public string? Account { get; set; }
    }
}

/// <summary>对应 Java：h2/domain/H2TableCommentEntity（带表注释与列注释的实体）。</summary>
[Table("t_h2_table_comment", Comment = "H2表注释测试")]
public class H2TableCommentEntity
{
    [Column(PrimaryKey = true, Comment = "主键ID")]
    public string? Id { get; set; }

    [Column(Comment = "名称")]
    public string? Name { get; set; }
}

/// <summary>对应 Java：entity/MultiPrimaryEntity（联合主键）。</summary>
[Table("t_multi_primary")]
public class MultiPrimaryEntity
{
    [Column(PrimaryKey = true)]
    public string? Id1 { get; set; }

    [Column(PrimaryKey = true)]
    public string? Id2 { get; set; }
}

/// <summary>
/// 对应 Java：h2/domain/ALLTYPESharding（列与 <see cref="ALLTYPE"/> 相同，按 id 取模分成 t_all_type_sharding_0、_1，
/// Java 的 AllTYPEShardingInitializer + AllTypeRoute，规则在 <see cref="H2Fixture"/> 里登记）。
/// </summary>
[Table("t_all_type_sharding")]
public class ALLTYPESharding : ALLTYPE;

/// <summary>对应 Java：h2/domain/H2Order（按 id 取模分成 t_order_0 … t_order_4，Java 的 H2OrderShardingInitializer + H2OrderRoute）。</summary>
[Table("t_order")]
public class H2Order
{
    [Column(PrimaryKey = true)]
    public int? Id { get; set; }

    public int? Status { get; set; }

    public string? Created { get; set; }
}
