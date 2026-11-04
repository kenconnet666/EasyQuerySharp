using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Tests.Upstream.LogicDelete;

/// <summary>对应 Java：logicdel/MyLogicDelStrategy。未删除是 <c>deleted_at IS NULL</c>；删除时写删除时间与当前用户。</summary>
public sealed class MyLogicDelStrategy : ILogicDeleteStrategy
{
    /// <inheritdoc />
    public SqlExpr NotDeleted(ColumnExpr column) => new IsNullExpr(column, negated: false);

    /// <inheritdoc />
    public IEnumerable<(ColumnMetadata Column, object? Value)> DeletedValues(EntityMetadata entity, ColumnMetadata column) =>
        [(column, DateTime.Now), (entity.GetColumn("DeletedUser"), CurrentUserHelper.UserId)];
}

/// <summary>对应 Java：logicdel/CurrentUserHelper，当前用户与租户随异步流程传递（各测试互不影响）。</summary>
public static class CurrentUserHelper
{
    private static readonly AsyncLocal<string?> User = new();
    private static readonly AsyncLocal<string?> Tenant = new();

    /// <summary>当前用户 id。</summary>
    public static string? UserId
    {
        get => User.Value;
        set => User.Value = value;
    }

    /// <summary>当前租户 id。</summary>
    public static string? TenantId
    {
        get => Tenant.Value;
        set => Tenant.Value = value;
    }
}
