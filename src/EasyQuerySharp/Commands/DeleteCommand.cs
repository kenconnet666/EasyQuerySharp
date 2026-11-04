using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;

namespace EasyQuerySharp.Commands;

/// <summary>
/// DELETE。实体有逻辑删除列时改写成 UPDATE（把删除标记置上），<see cref="WriteCommandBase{TSelf,T}.DisableLogicDelete"/> 或
/// <see cref="Hard"/> 之后才是物理删除。按条件删除且没有条件时会被拒绝（除非调用 <c>All()</c>）。
/// </summary>
public sealed class DeleteCommand<T> : WriteCommandBase<DeleteCommand<T>, T> where T : class
{
    internal DeleteCommand(EasyDb db, IReadOnlyList<T>? entities) : base(db, entities)
    {
    }

    private bool? _allowDeleteStatement;

    /// <summary>物理删除（同 <c>DisableLogicDelete()</c>）。</summary>
    public DeleteCommand<T> Hard() => DisableLogicDelete();

    /// <summary>
    /// 这条命令是否允许生成物理 DELETE（对应 Java 的 allowDeleteStatement），覆盖 <see cref="EasyDbOptions.AllowDeleteStatement"/>；
    /// 不允许时，没有逻辑删除（或关闭了逻辑删除）的删除会被拒绝。
    /// </summary>
    public DeleteCommand<T> AllowDeleteStatement(bool allow)
    {
        _allowDeleteStatement = allow;
        return this;
    }

    /// <summary>渲染第一条语句，不执行；没有实体时返回 null。</summary>
    public string? ToSql() => Statements().FirstOrDefault()?.Text;

    /// <inheritdoc />
    public override async Task<long> ExecuteAsync(CancellationToken cancellationToken = default) =>
        await Db.Executor.SessionAsync(async session =>
        {
            long rows = 0;
            foreach (var statement in Statements()) rows += await session.ExecuteAsync(statement, cancellationToken).ConfigureAwait(false);
            return rows;
        }, cancellationToken).ConfigureAwait(false);

    private IEnumerable<SqlStatement> Statements()
    {
        if (Entities is null)
        {
            yield return Render(null);
            yield break;
        }
        foreach (var entity in Entities) yield return Render(entity);
    }

    private SqlStatement Render(T? entity)
    {
        var table = NewTable();
        var logical = !LogicDeleteDisabled && Entity.LogicDeleteColumn is not null;
        if (!logical)
        {
            if (!(_allowDeleteStatement ?? Db.Options.AllowDeleteStatement))
                throw new InvalidOperationException($"{typeof(T).Name} 不允许物理删除：请用逻辑删除，或调用 AllowDeleteStatement(true)");
            var delete = new DeleteStatement(table);
            delete.Where.AddRange(BuildWhere(table, entity, applyLogicDelete: false));
            return SqlRenderer.Render(Db.Options.Dialect, delete);
        }

        var update = new UpdateStatement(table);
        var column = Entity.LogicDeleteColumn!;
        SqlExpr deleted = Entity.LogicDelete!.Strategy switch
        {
            LogicDeleteStrategy.Boolean => new ParameterExpr(true, typeof(bool)),
            LogicDeleteStrategy.DeletedTime => new ParameterExpr(DateTime.Now, typeof(DateTime)),
            _ => throw new NotSupportedException($"逻辑删除策略 {Entity.LogicDelete.Strategy} 需要注册自定义策略"),
        };
        update.Sets.Add((table.Column(column), deleted));
        update.Where.AddRange(BuildWhere(table, entity, applyLogicDelete: true));
        return SqlRenderer.Render(Db.Options.Dialect, update);
    }
}
