using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Dapper;
using EasyQuerySharp.Dialects;
using EasyQuerySharp.Execution;
using FreeSql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SqlSugar;

namespace EasyQuerySharp.Benchmarks;

/// <summary>
/// 各基准共用的库和五个 ORM 的客户端。库是临时目录里的 SQLite 文件（WAL 模式），GlobalSetup 里建表、写一次数据：
/// 100 个客户，每个客户 20 个订单，每个订单 5 条明细；外键列都有索引。
/// 客户端都按各自的常规用法创建：EasyDb、IFreeSql、SqlSugarClient 是长生命周期的单例；
/// EF Core 的 DbContext 也复用同一个（查询全部 AsNoTracking，不累积状态，相当于池化上下文）。
/// 除 FreeSql 自带连接池、连接常开外，其余每次查询都从 Microsoft.Data.Sqlite 的连接池取连接、用完归还。
/// GlobalSetup 最后由子类核对各 ORM 读到的结果一致，避免某个 ORM 因映射配置不对而读空、显得更快。
/// </summary>
[SuppressMessage("Design", "CA1001", Justification = "客户端在 GlobalCleanup 里释放，BenchmarkDotNet 管理生命周期")]
public abstract class OrmBenchmark
{
    public const int CustomerCount = 100;
    public const int OrdersPerCustomer = 20;
    public const int ItemsPerOrder = 5;

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"eqs_bench_{Environment.ProcessId}.db");

    /// <summary>不为 null 时，各 ORM 把执行的 SQL 交给它（<c>--sql</c> 模式用来核对 SQL 形状）。</summary>
    public Action<string>? SqlLog { get; init; }

    protected string ConnectionString { get; private set; } = "";
    protected EasyDb Easy { get; private set; } = null!;
    protected BenchContext Ef { get; private set; } = null!;
    protected IFreeSql Free { get; private set; } = null!;
    protected SqlSugarClient Sugar { get; private set; } = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        ConnectionString = $"Data Source={_file}";
        var dataSource = SqliteFactory.Instance.CreateDataSource(ConnectionString);
        await SeedAsync(new EasyDb(o => o.Use(SqliteDialect.Instance, dataSource)));
        Easy = new EasyDb(o =>
        {
            o.Use(SqliteDialect.Instance, dataSource);
            if (SqlLog is { } log) o.AddListener(new SqlLogListener(log));
        });
        Ef = new BenchContext(SqlLog, ConnectionString);
        Free = CreateFreeSql();
        Sugar = CreateSqlSugar();
        // Dapper 按 customer_id → CustomerId 匹配列（全局设置）
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        await VerifyAsync();
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await Ef.DisposeAsync();
        Free.Dispose();
        Sugar.Dispose();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(_file + suffix);
    }

    /// <summary>各 ORM 各跑一次，核对结果一致；不一致时抛异常，基准不再继续。</summary>
    protected abstract Task VerifyAsync();

    protected static void Check(string what, long actual, long expected)
    {
        if (actual != expected) throw new InvalidOperationException($"{what}：读到 {actual}，应为 {expected}");
    }

    /// <summary>Dapper / ADO.NET 用：从连接池取一个打开的连接，与其它 ORM 每次查询取连接的开销一致。</summary>
    protected async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedAsync(EasyDb db)
    {
        // WAL：默认的回滚日志模式下，每个读事务都要检查日志文件是否存在，在 Windows 上一条查询固定多出约 300 µs，
        // 会把各 ORM 之间的差别淹没；WAL 也是 SQLite 在服务端的常规配置
        await ExecuteAsync("PRAGMA journal_mode = WAL;");
        await db.CodeFirst.DropTablesIfExistsAsync(Schema.Entities);
        await db.CodeFirst.CreateTablesAsync(Schema.Entities);
        await ExecuteAsync("""
            CREATE INDEX ix_bench_order_customer_id ON bench_order (customer_id);
            CREATE INDEX ix_bench_order_item_order_id ON bench_order_item (order_id);
            """);

        var start = new DateTime(2026, 1, 1, 8, 0, 0);
        const int orderCount = CustomerCount * OrdersPerCustomer;
        await using var tx = db.BeginTransaction();
        await db.Insert(Enumerable.Range(1, CustomerCount).Select(i => new Customer
        {
            Id = i,
            Name = "customer " + i,
            Level = i % 5,
            CreatedAt = start.AddDays(i),
        })).Batch().ExecuteAsync();
        await db.Insert(Enumerable.Range(1, orderCount).Select(i => new Order
        {
            Id = i,
            CustomerId = (i - 1) / OrdersPerCustomer + 1,
            Title = "order " + i,
            Amount = i * 1.5,
            Status = i % 4,
            CreatedAt = start.AddMinutes(i),
            Remark = i % 3 == 0 ? null : "remark " + i,
        })).Batch().ExecuteAsync();
        await db.Insert(Enumerable.Range(1, orderCount * ItemsPerOrder).Select(i => new OrderItem
        {
            Id = i,
            OrderId = (i - 1) / ItemsPerOrder + 1,
            Product = "product " + i % 50,
            Quantity = i % 7 + 1,
            Price = i % 100 + 0.5,
        })).Batch().ExecuteAsync();
        await tx.CommitAsync();
    }

    private IFreeSql CreateFreeSql()
    {
        var builder = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, ConnectionString)
            // 条件里的变量生成命令参数（默认会把值直接拼进 SQL），与其它 ORM 的 SQL 形状一致
            .UseGenerateCommandParameterWithLambda(true)
            .UseAutoSyncStructure(false);
        if (SqlLog is { } log) builder.UseMonitorCommand(command => log(command.CommandText));
        var free = builder.Build();
        free.Aop.ConfigEntity += (_, e) => e.ModifyResult.Name = Schema.Table(e.EntityType) ?? e.ModifyResult.Name;
        free.Aop.ConfigEntityProperty += (_, e) =>
        {
            e.ModifyResult.Name = Schema.Column(e.Property.Name);
            if (Schema.IsKey(e.Property.Name)) e.ModifyResult.IsPrimary = true;
        };
        free.CodeFirst.ConfigEntity<Customer>(t => t.Navigate(c => c.Orders, nameof(Order.CustomerId)));
        free.CodeFirst.ConfigEntity<Order>(t => t.Navigate(o => o.Items, nameof(OrderItem.OrderId)));
        return free;
    }

    private SqlSugarClient CreateSqlSugar()
    {
        var sugar = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = ConnectionString,
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityNameService = (type, entity) => entity.DbTableName = Schema.Table(type) ?? entity.DbTableName,
                EntityService = (property, column) =>
                {
                    column.DbColumnName = Schema.Column(property.Name);
                    column.IsPrimarykey = Schema.IsKey(property.Name);
                    column.IfTable<Customer>().OneToMany(c => c.Orders, nameof(Order.CustomerId), nameof(Customer.Id));
                    column.IfTable<Order>().OneToMany(o => o.Items, nameof(OrderItem.OrderId), nameof(Order.Id));
                },
            },
        });
        if (SqlLog is { } log) sugar.Aop.OnLogExecuting = (sql, _) => log(sql);
        return sugar;
    }

    private sealed class SqlLogListener(Action<string> log) : IExecutionListener
    {
        public void OnExecuting(SqlExecutionEvent e) => log(e.Statement.Text);
    }
}

/// <summary>EF Core 的上下文：表名、列名按 <see cref="Schema"/> 配置。</summary>
public sealed class BenchContext(Action<string>? sqlLog, string connectionString) : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(connectionString);
        if (sqlLog is not null)
            optionsBuilder.LogTo(sqlLog, [Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuting]);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasMany(c => c.Orders).WithOne().HasForeignKey(o => o.CustomerId);
        modelBuilder.Entity<Order>().HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(Schema.Table(entity.ClrType));
            foreach (var property in entity.GetProperties()) property.SetColumnName(Schema.Column(property.Name));
        }
    }
}
