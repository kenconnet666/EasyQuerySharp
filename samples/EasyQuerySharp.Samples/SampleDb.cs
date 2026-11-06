using System.Data.Common;
using System.Linq.Expressions;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Interceptors;

namespace EasyQuerySharp.Samples;

/// <summary>
/// 示例库：建表、写入种子数据，以及示例模型用到的拦截器（工单的租户与更新时间、文档的更新时间）。
/// 主键由库生成，从 1 起，按种子数据的顺序；外键写的就是这些序号。
/// </summary>
public static class SampleDb
{
    /// <summary>示例里"现在"的时间：拦截器填的更新时间都用它，结果才稳定。</summary>
    public static readonly DateTime Now = new(2026, 11, 1, 8, 0, 0);

    /// <summary>全部表，按建表顺序（被引用的在前）。</summary>
    public static IReadOnlyList<Type> Tables { get; } =
    [
        typeof(Company), typeof(Department), typeof(User), typeof(Profile), typeof(Category), typeof(Post), typeof(Comment),
        typeof(Tag), typeof(PostTag), typeof(Follow), typeof(Product), typeof(Order), typeof(OrderItem), typeof(Doc), typeof(Ticket), typeof(Shop),
    ];

    /// <summary>示例的 EasyDb：给定方言与数据源，挂上示例模型的拦截器。</summary>
    public static EasyDb Create(SqlDialect dialect, DbDataSource? dataSource, Action<EasyDbOptions>? configure = null) =>
        new(o =>
        {
            o.Use(dialect, dataSource)
                .AddInterceptor(new TenantFilter())
                .AddInterceptor(new TicketFill())
                .AddInterceptor(new DocTouch());
            configure?.Invoke(o);
        });

    /// <summary>重建全部表并写入种子数据。</summary>
    public static async Task ResetAsync(EasyDb db, CancellationToken cancellationToken = default)
    {
        await db.CodeFirst.DropTablesIfExistsAsync(Tables.Reverse(), cancellationToken);
        await db.CodeFirst.CreateTablesAsync(Tables, cancellationToken);
        await SampleData.InsertAsync(db, cancellationToken);
    }

    /// <summary>工单只看 1 号租户。</summary>
    public sealed class TenantFilter : QueryFilter<Ticket>
    {
        /// <inheritdoc />
        public override Expression<Func<Ticket, bool>>? Filter() => t => t.TenantId == 1;
    }

    /// <summary>工单：插入时没写租户就填 1 号；更新时填更新时间。</summary>
    public sealed class TicketFill : EntityInterceptor<Ticket>
    {
        /// <inheritdoc />
        public override void BeforeInsert(Ticket entity)
        {
            if (entity.TenantId == 0) entity.TenantId = 1;
        }

        /// <inheritdoc />
        public override void BeforeUpdate(Ticket entity) => entity.TouchedAt = Now;

        /// <inheritdoc />
        public override void BeforeConditionUpdate(UpdateSetBuilder<Ticket> sets) => sets.Set(t => t.TouchedAt, Now);
    }

    /// <summary>文档：更新时填更新时间。</summary>
    public sealed class DocTouch : EntityInterceptor<Doc>
    {
        /// <inheritdoc />
        public override void BeforeUpdate(Doc entity) => entity.UpdatedAt = Now;

        /// <inheritdoc />
        public override void BeforeConditionUpdate(UpdateSetBuilder<Doc> sets) => sets.Set(d => d.UpdatedAt, Now);
    }
}
