using EasyQuerySharp.Metadata;
using EasyQuerySharp.Rendering;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

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
    public string? ToSql() => ToStatement()?.Text;

    /// <summary>同 <see cref="ToSql"/>，连同参数。</summary>
    public SqlStatement? ToStatement() => Statements().FirstOrDefault();

    /// <inheritdoc />
    public override async Task<long> ExecuteAsync(CancellationToken cancellationToken = default) =>
        await Db.Executor.SessionAsync(async session =>
        {
            long rows = 0;
            foreach (var batch in WriteBatches.Of(Statements(), s => s, UseBatch))
                rows += (await session.ExecuteBatchAsync(batch, cancellationToken).ConfigureAwait(false)).Total;
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
        var scope = NewScope();
        var table = scope.Table;
        var logical = !LogicDeleteDisabled && Entity.LogicDeleteColumn is not null;
        if (!logical)
        {
            if (!(_allowDeleteStatement ?? Db.Options.AllowDeleteStatement))
                throw new InvalidOperationException($"{typeof(T).Name} 不允许物理删除：请用逻辑删除，或调用 AllowDeleteStatement(true)");
            var delete = new DeleteStatement(table);
            delete.Where.AddRange(BuildWhere(scope, entity, applyLogicDelete: false));
            delete.Joins.AddRange(scope.Joins);
            return SqlRenderer.Render(Db.Options.Dialect, delete);
        }

        var update = new UpdateStatement(table);
        var strategy = LogicDeleteStrategies.Resolve(Db.Options, Entity);
        foreach (var (column, value) in strategy.DeletedValues(Entity, Entity.LogicDeleteColumn!))
            update.Sets.Add((table.Column(column), ExpressionTranslator.ParameterFromModel(column, value)));
        AddVersionSet(update, table, entity);
        update.Where.AddRange(BuildWhere(scope, entity, applyLogicDelete: true));
        update.Joins.AddRange(scope.Joins);
        return SqlRenderer.Render(Db.Options.Dialect, update);
    }
}
