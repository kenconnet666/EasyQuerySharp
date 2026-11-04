using System.Data;
using System.Data.Common;
using EasyQuerySharp.Commands;
using EasyQuerySharp.Execution;
using EasyQuerySharp.Interceptors;
using EasyQuerySharp.Metadata;
using EasyQuerySharp.Querying;

namespace EasyQuerySharp;

/// <summary>
/// 入口：查询、写入、事务、原生 SQL。线程安全，通常整个应用一个实例（按数据库）。
/// </summary>
/// <example><code>
/// var db = new EasyDb(o => o.UseMySql(new MySqlDataSource(connectionString)));
/// var users = await db.Query&lt;User&gt;().Where(u => u.Name.Contains("ali")).ToListAsync();
/// </code></example>
public sealed class EasyDb
{
    /// <summary>用配置函数创建。</summary>
    public EasyDb(Action<EasyDbOptions> configure) : this(Configure(configure))
    {
    }

    /// <summary>用配置对象创建。</summary>
    public EasyDb(EasyDbOptions options)
    {
        if (options.Dialect is null) throw new ArgumentException("必须指定方言，如 options.UseMySql(...)", nameof(options));
        Options = options;
        Metadata = new MetadataRegistry(options.NameConversion);
        Executor = new SqlExecutor(this);
    }

    /// <summary>配置（创建后不要再修改）。</summary>
    public EasyDbOptions Options { get; }

    /// <summary>实体元数据。</summary>
    public MetadataRegistry Metadata { get; }

    /// <summary>拦截器。</summary>
    public InterceptorRegistry Interceptors => Options.Interceptors;

    internal SqlExecutor Executor { get; }

    /// <summary>从实体 <typeparamref name="T"/> 开始一条查询。</summary>
    public Query<T> Query<T>() => new(new QueryState(this, [new EntitySource(typeof(T))]));

    /// <summary>插入一个实体。</summary>
    public InsertCommand<T> Insert<T>(T entity) where T : class => new(this, [entity]);

    /// <summary>插入多个实体（一条多行 INSERT）。</summary>
    public InsertCommand<T> Insert<T>(IEnumerable<T> entities) where T : class => new(this, entities.ToList());

    /// <summary>按条件更新：<c>db.Update&lt;T&gt;().Set(…).Where(…)</c>。</summary>
    public UpdateCommand<T> Update<T>() where T : class => new(this, null);

    /// <summary>按主键整行更新一个实体。</summary>
    public UpdateCommand<T> Update<T>(T entity) where T : class => new(this, [entity]);

    /// <summary>按主键整行更新多个实体（每个一条 UPDATE）。</summary>
    public UpdateCommand<T> Update<T>(IEnumerable<T> entities) where T : class => new(this, entities.ToList());

    /// <summary>按条件删除：<c>db.Delete&lt;T&gt;().Where(…)</c>。有逻辑删除列时改写成更新。</summary>
    public DeleteCommand<T> Delete<T>() where T : class => new(this, null);

    /// <summary>按主键删除实体。</summary>
    public DeleteCommand<T> Delete<T>(T entity) where T : class => new(this, [entity]);

    /// <summary>按主键删除多个实体。</summary>
    public DeleteCommand<T> Delete<T>(IEnumerable<T> entities) where T : class => new(this, entities.ToList());

    /// <summary>开启环境事务（见 <see cref="EasyTransaction"/>）。</summary>
    public EasyTransaction BeginTransaction(IsolationLevel isolation = IsolationLevel.Unspecified) => new(this, isolation);

    /// <summary>在事务里执行 <paramref name="work"/>：正常结束提交，抛出异常回滚。</summary>
    public async Task TransactionAsync(Func<Task> work, IsolationLevel isolation = IsolationLevel.Unspecified, CancellationToken cancellationToken = default)
    {
        await using var tx = BeginTransaction(isolation);
        await work().ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>打开一个新连接（调用方负责释放）。</summary>
    internal async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var source = Options.DataSource
                     ?? throw new InvalidOperationException("没有配置数据源，只能生成 SQL（ToSql）。请在 EasyDbOptions 里传入 DbDataSource");
        return await source.OpenConnectionAsync(ct).ConfigureAwait(false);
    }

    private static EasyDbOptions Configure(Action<EasyDbOptions> configure)
    {
        var options = new EasyDbOptions();
        configure(options);
        return options;
    }
}
