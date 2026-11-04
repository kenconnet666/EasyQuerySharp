using System.Collections;
using System.Linq.Expressions;
using EasyQuerySharp.Materialization;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.SqlAst;
using EasyQuerySharp.Translation;

namespace EasyQuerySharp.Commands;

/// <summary>按主键构造条件：查询与写入共用。</summary>
internal static class KeyPredicates
{
    /// <summary><c>t => t.Key == id</c>（主键值先转成主键类型）。</summary>
    public static Expression<Func<T, bool>> ById<T>(EntityMetadata entity, object id)
    {
        var key = entity.SingleKey;
        var p = Expression.Parameter(typeof(T), "t");
        var body = Expression.Equal(Expression.Property(p, key.Property), Expression.Constant(ValueReader.ConvertTo(id, key.ClrType), key.ClrType));
        return Expression.Lambda<Func<T, bool>>(body, p);
    }

    /// <summary><c>t => ids.Contains(t.Key)</c>。</summary>
    public static Expression<Func<T, bool>> ByIds<T>(EntityMetadata entity, IEnumerable ids)
    {
        var key = entity.SingleKey;
        var listType = typeof(List<>).MakeGenericType(key.ClrType);
        var values = (IList)Activator.CreateInstance(listType)!;
        foreach (var v in ids) values.Add(ValueReader.ConvertTo(v, key.ClrType));
        var p = Expression.Parameter(typeof(T), "t");
        var body = Expression.Call(Expression.Constant(values), listType.GetMethod("Contains")!, Expression.Property(p, key.Property));
        return Expression.Lambda<Func<T, bool>>(body, p);
    }

    /// <summary>某个实体实例的主键条件（复合主键依次 AND）。</summary>
    public static List<SqlExpr> ForEntity(EntityTableSource table, object entity)
    {
        var keys = table.Entity.KeyColumns;
        if (keys.Count == 0) throw new InvalidOperationException($"{table.Entity.Type.Name} 没有主键，不能按实体更新或删除");
        return keys.Select(k => (SqlExpr)new BinaryExpr(table.Column(k), SqlBinaryOperator.Equal,
            ExpressionTranslator.Parameter(k.GetProviderValue(entity), k.Converter?.ProviderType ?? k.ClrType))).ToList();
    }
}
