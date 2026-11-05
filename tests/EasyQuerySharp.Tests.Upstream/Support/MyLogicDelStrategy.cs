using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Tests.Upstream.Support;

/// <summary>对应 Java：logicdel/MyLogicDelStrategy。未删除是 <c>deleted_at IS NULL</c>；删除时写删除时间与当前用户。</summary>
public sealed class MyLogicDelStrategy : ILogicDeleteStrategy
{
    /// <inheritdoc />
    public SqlExpr NotDeleted(ColumnExpr column) => new IsNullExpr(column, negated: false);

    /// <inheritdoc />
    public IEnumerable<(ColumnMetadata Column, object? Value)> DeletedValues(EntityMetadata entity, ColumnMetadata column) =>
        [(column, DateTime.Now), (entity.GetColumn("DeletedUser"), CurrentUserHelper.UserId)];
}
