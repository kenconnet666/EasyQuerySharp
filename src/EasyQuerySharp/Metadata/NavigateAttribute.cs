namespace EasyQuerySharp.Metadata;

/// <summary>导航关系的类型。</summary>
public enum RelationType
{
    /// <summary>一对一：本表的键对应目标表的键。</summary>
    OneToOne,

    /// <summary>多对一：本表的外键对应目标表的主键。</summary>
    ManyToOne,

    /// <summary>一对多：本表的主键对应目标表的外键，属性是集合。</summary>
    OneToMany,

    /// <summary>多对多：经中间表关联，属性是集合。</summary>
    ManyToMany,
}

/// <summary>集合导航取第几个元素、取一段行时，链上没写排序的话组内按什么排（对应 Java 的 PartitionOrderEnum）。</summary>
public enum PartitionOrder
{
    /// <summary>按目标表主键升序（默认，对应 Java 的 KEY）。</summary>
    Key,

    /// <summary>不排序（对应 Java 的 IGNORE）：<c>ROW_NUMBER() OVER (PARTITION BY 外键)</c>。</summary>
    None,

    /// <summary>按导航上声明的 <see cref="NavigateAttribute.OrderBy"/>（对应 Java 的 NAVIGATE）。</summary>
    Navigation,

    /// <summary>按目标表主键降序（对应 Java 的 KEY_DESC）。</summary>
    KeyDescending,
}

/// <summary>
/// 把属性声明为导航（对应 Java 的 @Navigate）。导航可以直接写在查询里：
/// 对一导航 <c>card.User!.Name</c> 生成隐式 JOIN，集合导航 <c>user.BankCards.Any(c => …)</c> 生成隐式子查询。
/// 未写的键默认取主键：对一导航的 <see cref="TargetProperties"/>、一对多的 <see cref="SelfProperties"/>。
/// </summary>
/// <example><code>
/// [Navigate(RelationType.ManyToOne, SelfProperties = [nameof(Uid)])]
/// public SysUser? User { get; set; }
///
/// [Navigate(RelationType.OneToMany, TargetProperties = [nameof(SysBankCard.Uid)])]
/// public List&lt;SysBankCard&gt;? BankCards { get; set; }
///
/// [Navigate(RelationType.ManyToMany, Mapping = typeof(UserRole),
///     SelfMappingProperties = [nameof(UserRole.UserId)], TargetMappingProperties = [nameof(UserRole.RoleId)])]
/// public List&lt;Role&gt;? Roles { get; set; }
/// </code></example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NavigateAttribute(RelationType relation) : Attribute
{
    /// <summary>关系类型。</summary>
    public RelationType Relation { get; } = relation;

    /// <summary>本表参与关联的属性（多个为复合键）；不写时取主键。</summary>
    public string[] SelfProperties { get; init; } = [];

    /// <summary>目标表参与关联的属性；不写时取主键。</summary>
    public string[] TargetProperties { get; init; } = [];

    /// <summary>多对多的中间表实体。</summary>
    public Type? Mapping { get; init; }

    /// <summary>中间表里对应本表的属性。</summary>
    public string[] SelfMappingProperties { get; init; } = [];

    /// <summary>中间表里对应目标表的属性。</summary>
    public string[] TargetMappingProperties { get; init; } = [];

    /// <summary>集合导航上的聚合总是合并成一个分组 LEFT JOIN（对应 Java 的 subQueryToGroupJoin = true），见 <c>Query.GroupJoin</c>。</summary>
    public bool GroupJoin { get; init; }

    /// <summary>
    /// 直达路径（对应 Java 的 directMapping）：这个对一导航等于沿着本实体上的一串对一导航走到底，
    /// 如 <c>DirectMapping = [nameof(ComUser), nameof(MyComUser.MyCompany)]</c>；隐式 JOIN 与直接写 <c>x.ComUser.MyCompany</c> 共用。
    /// </summary>
    public string[] DirectMapping { get; init; } = [];

    /// <summary>对一导航是否必然存在：为 true 时隐式 JOIN 用 INNER JOIN，否则 LEFT JOIN（对应 Java 的 required）。</summary>
    public bool Required { get; init; }

    /// <summary>
    /// 导航上的固定条件：本实体上一个返回 <c>Expression&lt;Func&lt;目标, bool&gt;&gt;</c> 的静态属性或字段的名字
    /// （对应 Java 的 extraFilter）。用 <c>nameof</c> 引用。
    /// </summary>
    public string? ExtraFilter { get; init; }

    /// <summary>
    /// 多对多中间表上的固定条件：本实体上一个返回 <c>Expression&lt;Func&lt;中间表, bool&gt;&gt;</c> 的静态属性或字段的名字
    /// （对应 Java 的 extraFilter 里的 getPredicateMappingClassFilterExpression）。两种条件的属性都可以按需返回 null，表示这次不加。
    /// </summary>
    public string? ExtraMappingFilter { get; init; }

    /// <summary>
    /// 集合导航的默认排序（对应 Java 的 orderByProps）：逗号分隔的"属性 [asc|desc] [nulls first|nulls last]"，
    /// 如 <c>"OpenTime, Code desc nulls first"</c>。<see cref="PartitionOrder"/> 为 <see cref="Metadata.PartitionOrder.Navigation"/> 时取元素按它排。
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>取第几个元素、取一段行而链上没写排序时组内按什么排，默认按主键。</summary>
    public PartitionOrder PartitionOrder { get; init; }

    /// <summary>
    /// 带出时每个父对象最多取几条（对应 Java 的 limit；0 为不限），按 <see cref="OrderBy"/> 排序后取。
    /// 常用于一对一取"第一张卡"：<c>[Navigate(RelationType.OneToOne, TargetProperties = [nameof(Card.Uid)], OrderBy = nameof(Card.OpenTime), Limit = 1)]</c>。
    /// </summary>
    public int Limit { get; init; }

    /// <summary>聚合保存（<c>db.Save(root)</c>）时这条导航怎样级联，见 <see cref="Metadata.Cascade"/> 与 0004；默认 <see cref="Cascade.Auto"/>。</summary>
    public Cascade Cascade { get; init; }
}
