namespace EasyQuerySharp.Metadata;

/// <summary>
/// 聚合保存（<c>db.Save(root)</c>）时导航的级联方式（对应 Java 的 CascadeTypeEnum，见 0004）。只对"值对象"导航起作用：
/// 自身列是主键的一对一、一对多，以及有中间表的多对多；多对一（别的聚合根）从不级联。
/// </summary>
public enum Cascade
{
    /// <summary>默认：子对象是引用，只改外键挂上或断开（孤儿的外键置 NULL），不插入、不递归。</summary>
    Auto,

    /// <summary>不处理。</summary>
    NoAction,

    /// <summary>同 <see cref="Auto"/>：孤儿的外键置 NULL。</summary>
    SetNull,

    /// <summary>子对象是聚合的一部分：新增插入、改动更新、孤儿删除，并递归处理它自己的导航；多对多维护中间表行。</summary>
    Delete,
}
