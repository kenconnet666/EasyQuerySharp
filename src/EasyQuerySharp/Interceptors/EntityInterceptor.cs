namespace EasyQuerySharp.Interceptors;

/// <summary>
/// 写入前修改实体（对应 Java 的 EntityInterceptor）：插入、按实体更新之前调用，常用于填写创建人、修改时间等；
/// 按条件更新没有实体，用 <see cref="BeforeConditionUpdate"/> 追加 SET（对应 Java 的 UpdateSetInterceptor）。
/// </summary>
public abstract class EntityInterceptor<TEntity> : IEntityWriteInterceptor
{
    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public Type TargetType => typeof(TEntity);

    /// <inheritdoc />
    public virtual bool EnabledByDefault => true;

    /// <summary>插入前调用。</summary>
    public virtual void BeforeInsert(TEntity entity)
    {
    }

    /// <summary>按实体更新前调用。</summary>
    public virtual void BeforeUpdate(TEntity entity)
    {
    }

    /// <summary>
    /// 按条件更新（<c>db.Update&lt;T&gt;().Set(..)</c>）时调用，追加要写的列，如
    /// <c>sets.Set(e =&gt; e.UpdateBy, user).Set(e =&gt; e.UpdateTime, DateTime.Now)</c>；用户已经 Set 过的列自动跳过。
    /// </summary>
    public virtual void BeforeConditionUpdate(UpdateSetBuilder<TEntity> sets)
    {
    }

    /// <summary>
    /// 按实体更新且用 <c>SetColumns</c> 限定了列时调用，可以补上必须写的列：<c>columns.Include(e =&gt; e.UpdateTime)</c>。
    /// 配合 <see cref="BeforeUpdate"/> 改实体上的值（对应 Java 的 UpdateEntityColumnInterceptor）。
    /// </summary>
    public virtual void BeforeEntityUpdateColumns(UpdateColumnsBuilder<TEntity> columns)
    {
    }

    void IEntityWriteInterceptor.BeforeInsert(object entity) => BeforeInsert((TEntity)entity);

    void IEntityWriteInterceptor.BeforeUpdate(object entity) => BeforeUpdate((TEntity)entity);

    void IEntityWriteInterceptor.BeforeConditionUpdate(UpdateSetCollector collector) => BeforeConditionUpdate(new UpdateSetBuilder<TEntity>(collector));

    void IEntityWriteInterceptor.BeforeEntityUpdateColumns(UpdateSetCollector collector) => BeforeEntityUpdateColumns(new UpdateColumnsBuilder<TEntity>(collector));
}
