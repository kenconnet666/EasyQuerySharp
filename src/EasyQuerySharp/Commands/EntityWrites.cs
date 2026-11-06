using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Commands;

/// <summary>
/// 按运行时类型执行单个实体的写入（聚合保存里对象的类型只在运行时知道）：转给泛型的 Insert / Update / Delete 命令，
/// 拦截器、主键生成、逻辑删除、按追踪差异更新照常生效。
/// </summary>
internal static class EntityWrites
{
    private static readonly ConcurrentDictionary<Type, Operations> Cache = new();

    /// <summary>插入；实体有生成的主键时回填。</summary>
    public static Task<long> InsertAsync(EasyDb db, object entity, CancellationToken ct) => For(entity.GetType()).Insert(db, entity, ct);

    /// <summary>按实体更新（已追踪时只写变化的列，没有变化不发语句）。</summary>
    public static Task<long> UpdateAsync(EasyDb db, object entity, CancellationToken ct) => For(entity.GetType()).Update(db, entity, ct);

    /// <summary>按主键删除；<paramref name="physical"/> 为 false 时有逻辑删除列就逻辑删除。</summary>
    public static Task<long> DeleteAsync(EasyDb db, object entity, bool physical, CancellationToken ct) => For(entity.GetType()).Delete(db, entity, physical, ct);

    /// <summary>
    /// 多对多的中间表行：<c>DELETE FROM 中间表 WHERE 自身映射列 = ? AND 目标映射列 = ?</c>，<paramref name="setNull"/> 时改成把两列置 NULL（与 Java 一致）。
    /// </summary>
    public static Task<long> DeleteMappingAsync(EasyDb db, NavigationMetadata nav, object parent, object target, bool setNull, CancellationToken ct)
    {
        var mapping = nav.Mapping!;
        var row = Expression.Parameter(mapping.Type, "m");
        var where = Expression.Lambda(MappingCondition(nav, row, parent, target), row);
        if (!setNull) return For(mapping.Type).DeleteWhere(db, where, ct);
        var nulls = nav.SelfMappingColumns.Concat(nav.TargetMappingColumns)
            .Select(c => (MemberBinding)Expression.Bind(c.Property, Expression.Constant(null, c.Property.PropertyType)));
        var set = Expression.Lambda(Expression.MemberInit(Expression.New(mapping.Type), nulls), row);
        return For(mapping.Type).UpdateWhere(db, set, where, ct);
    }

    /// <summary>插入一行中间表：自身映射列取父对象的键，目标映射列取目标的键（中间表自己的主键由拦截器或生成器给出）。</summary>
    public static Task<long> InsertMappingAsync(EasyDb db, NavigationMetadata nav, object parent, object target, CancellationToken ct)
    {
        var row = Activator.CreateInstance(nav.Mapping!.Type) ?? throw new InvalidOperationException($"无法创建中间表实体 {nav.Mapping.Type.Name}");
        for (var i = 0; i < nav.SelfMappingColumns.Count; i++) nav.SelfMappingColumns[i].Property.SetValue(row, nav.SelfColumns[i].Property.GetValue(parent));
        for (var i = 0; i < nav.TargetMappingColumns.Count; i++) nav.TargetMappingColumns[i].Property.SetValue(row, nav.TargetColumns[i].Property.GetValue(target));
        return InsertAsync(db, row, ct);
    }

    // 自身映射列 = 父对象的键 AND 目标映射列 = 目标的键
    private static Expression MappingCondition(NavigationMetadata nav, ParameterExpression row, object parent, object target)
    {
        var pairs = nav.SelfMappingColumns.Select((c, i) => (c, nav.SelfColumns[i].Property.GetValue(parent)))
            .Concat(nav.TargetMappingColumns.Select((c, i) => (c, nav.TargetColumns[i].Property.GetValue(target))));
        return pairs.Select(p => (Expression)Expression.Equal(Expression.Property(row, p.c.Property), Expression.Constant(p.Item2, p.c.Property.PropertyType)))
            .Aggregate(Expression.AndAlso);
    }

    private static Operations For(Type type) => Cache.GetOrAdd(type, t =>
        (Operations)typeof(EntityWrites).GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(t).Invoke(null, null)!);

    private static Operations Create<T>() where T : class => new(
        (db, entity, ct) =>
        {
            var insert = db.Insert((T)entity);
            return (db.Metadata.Get<T>().KeyColumns.Any(k => k.IsGenerated) ? insert.FillGeneratedKeys() : insert).ExecuteAsync(ct);
        },
        (db, entity, ct) => db.Update((T)entity).ExecuteAsync(ct),
        (db, entity, physical, ct) => (physical ? db.Delete((T)entity).Hard() : db.Delete((T)entity)).ExecuteAsync(ct),
        (db, where, ct) => db.Delete<T>().Where((Expression<Func<T, bool>>)where).ExecuteAsync(ct),
        (db, set, where, ct) => db.Update<T>().Set((Expression<Func<T, T>>)set).Where((Expression<Func<T, bool>>)where).ExecuteAsync(ct));

    private sealed record Operations(
        Func<EasyDb, object, CancellationToken, Task<long>> Insert,
        Func<EasyDb, object, CancellationToken, Task<long>> Update,
        Func<EasyDb, object, bool, CancellationToken, Task<long>> Delete,
        Func<EasyDb, LambdaExpression, CancellationToken, Task<long>> DeleteWhere,
        Func<EasyDb, LambdaExpression, LambdaExpression, CancellationToken, Task<long>> UpdateWhere);
}
