namespace EasyQuerySharp.Samples;

/// <summary>示例数据，与 TS 版 apps/docs/src/site/seed-data.ts 一致（主键按顺序从 1 起）。</summary>
public static class SampleData
{
    /// <summary>按依赖顺序写入全部种子数据。</summary>
    public static async Task InsertAsync(EasyDb db, CancellationToken ct = default)
    {
        await db.Insert(Companies).ExecuteAsync(ct);
        await db.Insert(Departments).ExecuteAsync(ct);
        await db.Insert(Users).ExecuteAsync(ct);
        await db.Insert(Profiles).ExecuteAsync(ct);
        await db.Insert(Categories).ExecuteAsync(ct);
        await db.Insert(Posts).ExecuteAsync(ct);
        await db.Insert(Comments).ExecuteAsync(ct);
        await db.Insert(Tags).ExecuteAsync(ct);
        await db.Insert(PostTags).ExecuteAsync(ct);
        await db.Insert(Follows).ExecuteAsync(ct);
        await db.Insert(Products).ExecuteAsync(ct);
        await db.Insert(Orders).ExecuteAsync(ct);
        await db.Insert(OrderItems).ExecuteAsync(ct);
        await db.Insert(Docs).ExecuteAsync(ct);
        await db.Insert(Tickets).ExecuteAsync(ct);
    }

    // 每次调用都新建对象：插入会回填主键等，不能在多个库之间共用实例
    private static Company[] Companies =>
    [
        new() { Name = "星河科技", City = "杭州" },
        new() { Name = "云帆软件", City = "上海" },
        new() { Name = "北辰数据", City = "北京" },
        new() { Name = "南风智能", City = "深圳" },
    ];

    private static Department[] Departments =>
    [
        new() { CompanyId = 1, ParentId = null, Name = "研发部" },
        new() { CompanyId = 1, ParentId = 1, Name = "前端组" },
        new() { CompanyId = 1, ParentId = 1, Name = "后端组" },
        new() { CompanyId = 1, ParentId = 3, Name = "基础架构小组" },
        new() { CompanyId = 1, ParentId = null, Name = "市场部" },
        new() { CompanyId = 2, ParentId = null, Name = "产品部" },
        new() { CompanyId = 2, ParentId = 6, Name = "设计组" },
        new() { CompanyId = 3, ParentId = null, Name = "数据部" },
    ];

    private static User[] Users =>
    [
        new() { Name = "林晓", Age = 29, CompanyId = 1, DepartmentId = 2, ManagerId = 2, Level = UserLevel.Senior, JoinedAt = new(2023, 3, 14) },
        new() { Name = "周航", Age = 34, CompanyId = 1, DepartmentId = 1, ManagerId = null, Level = UserLevel.Lead, JoinedAt = new(2022, 7, 1) },
        new() { Name = "陈默", Age = 41, CompanyId = 2, DepartmentId = 6, ManagerId = null, Level = UserLevel.Lead, JoinedAt = new(2021, 11, 20) },
        new() { Name = "苏宁", Age = 26, CompanyId = 2, DepartmentId = 7, ManagerId = 3, Level = UserLevel.Junior, JoinedAt = new(2024, 2, 8) },
        new() { Name = "方圆", Age = null, CompanyId = 3, DepartmentId = 8, ManagerId = null, Level = UserLevel.Senior, JoinedAt = new(2024, 9, 30) },
        new() { Name = "顾言", Age = 17, CompanyId = null, DepartmentId = null, ManagerId = null, Level = UserLevel.Junior, JoinedAt = new(2025, 1, 12) },
        new() { Name = "韩梅", Age = 31, CompanyId = 1, DepartmentId = 3, ManagerId = 2, Level = UserLevel.Senior, JoinedAt = new(2022, 12, 5) },
        new() { Name = "李雷", Age = 25, CompanyId = 1, DepartmentId = 4, ManagerId = 7, Level = UserLevel.Junior, JoinedAt = new(2025, 3, 18) },
        new() { Name = "沈舟", Age = 38, CompanyId = 2, DepartmentId = 6, ManagerId = 3, Level = UserLevel.Senior, JoinedAt = new(2020, 6, 15) },
        new() { Name = "白露", Age = 23, CompanyId = 3, DepartmentId = 8, ManagerId = 5, Level = UserLevel.Junior, JoinedAt = new(2025, 6, 1) },
    ];

    private static Profile[] Profiles =>
    [
        new() { UserId = 1, Bio = "喜欢把慢查询改快", Skills = ["SQL", "TypeScript"], Website = "https://linxiao.dev" },
        new() { UserId = 2, Bio = "做了十年后端", Skills = ["Java", "PostgreSQL", "架构"], Website = null },
        new() { UserId = 3, Bio = "数据库迁移专业户", Skills = ["PostgreSQL", "MySQL"], Website = "https://chenmo.io" },
        new() { UserId = 4, Bio = "设计转前端", Skills = ["Vue", "CSS"], Website = null },
        new() { UserId = 7, Bio = "组件库爱好者", Skills = ["Vue", "TypeScript", "SSR"], Website = "https://hanmei.me" },
        new() { UserId = 9, Bio = "带团队第三年", Skills = ["管理"], Website = null },
    ];

    private static Category[] Categories =>
    [
        new() { ParentId = null, Name = "技术" },
        new() { ParentId = 1, Name = "数据库" },
        new() { ParentId = 1, Name = "前端" },
        new() { ParentId = 2, Name = "PostgreSQL" },
        new() { ParentId = 1, Name = "架构" },
        new() { ParentId = null, Name = "职场" },
    ];

    private static Post[] Posts =>
    [
        Post(1, 2, "隐式 join 是怎么生成的", 1280, new(2025, 2, 10)),
        Post(1, 2, "把 N+1 查询改成一条", 860, new(2025, 3, 22)),
        Post(2, 5, "分库分表的路由设计", 2140, new(2025, 1, 8)),
        Post(3, 4, "从 MySQL 迁到 PostgreSQL", 3320, new(2024, 11, 30)),
        Post(3, 4, "窗口函数入门", 0, null),
        Post(4, 5, "二级缓存的一致性", 640, new(2025, 4, 15)),
        Post(7, 3, "TSX 组件的类型推导", 1510, new(2025, 5, 2)),
        Post(7, 5, "服务端渲染踩坑记", 920, new(2025, 6, 18)),
        Post(9, 6, "带团队的第三年", 2760, new(2024, 9, 12)),
        Post(5, 2, "数据仓库分层实践", 430, new(2025, 7, 1)),
        Post(8, 3, "我的第一个开源组件", 180, new(2025, 8, 9)),
        Post(10, 2, "SQL 新手常见错误", 0, null),
    ];

    // 没有发布日期的是草稿
    private static Post Post(long author, long category, string title, int views, DateOnly? publishedAt) => new()
    {
        AuthorId = author,
        CategoryId = category,
        Title = title,
        Status = publishedAt is null ? PostStatus.Draft : PostStatus.Published,
        Views = views,
        PublishedAt = publishedAt,
    };

    private static Comment[] Comments =>
    [
        new() { PostId = 1, AuthorId = 2, ParentId = null, Body = "终于看懂了别名的规则", Likes = 12 },
        new() { PostId = 1, AuthorId = 3, ParentId = 1, Body = "多层导航也是这样吗", Likes = 3 },
        new() { PostId = 1, AuthorId = 1, ParentId = 2, Body = "是的，每层一个别名", Likes = 5 },
        new() { PostId = 3, AuthorId = 1, ParentId = null, Body = "按月分表时怎么裁剪", Likes = 8 },
        new() { PostId = 4, AuthorId = 2, ParentId = null, Body = "踩过同样的坑", Likes = 20 },
        new() { PostId = 4, AuthorId = 4, ParentId = null, Body = "类型映射那段很有用", Likes = 15 },
        new() { PostId = 4, AuthorId = 5, ParentId = 5, Body = "求一份迁移清单", Likes = 9 },
        new() { PostId = 4, AuthorId = 3, ParentId = 7, Body = "发你了", Likes = 2 },
        new() { PostId = 6, AuthorId = 1, ParentId = null, Body = "延迟双删的时间怎么定", Likes = 6 },
        new() { PostId = 7, AuthorId = 8, ParentId = null, Body = "学到了", Likes = 1 },
        new() { PostId = 7, AuthorId = 4, ParentId = 10, Body = "泛型那段再讲讲", Likes = 4 },
        new() { PostId = 9, AuthorId = 7, ParentId = null, Body = "说得太对了", Likes = 18 },
        new() { PostId = 9, AuthorId = 2, ParentId = null, Body = "第三年最难", Likes = 11 },
        new() { PostId = 11, AuthorId = 7, ParentId = null, Body = "欢迎来提 PR", Likes = 7 },
    ];

    private static Tag[] Tags => [new() { Name = "ORM" }, new() { Name = "PostgreSQL" }, new() { Name = "架构" }, new() { Name = "缓存" }, new() { Name = "前端" }, new() { Name = "入门" }];

    private static PostTag[] PostTags =>
    [
        .. new (long Post, long Tag)[] { (1, 1), (2, 1), (3, 3), (4, 2), (4, 3), (5, 2), (5, 6), (6, 3), (6, 4), (7, 5), (8, 5), (8, 3), (11, 5), (11, 6), (12, 6) }
            .Select(p => new PostTag { PostId = p.Post, TagId = p.Tag }),
    ];

    private static Follow[] Follows =>
    [
        .. new (long Follower, long Followee)[] { (1, 2), (1, 3), (2, 3), (4, 3), (4, 1), (8, 7), (8, 1), (7, 2), (10, 5), (6, 1), (5, 3), (3, 9) }
            .Select(f => new Follow { FollowerId = f.Follower, FolloweeId = f.Followee }),
    ];

    private static Product[] Products =>
    [
        new() { Name = "年度会员", Price = 299.00m, Stock = 999 },
        new() { Name = "季度会员", Price = 99.00m, Stock = 999 },
        new() { Name = "数据库实战课", Price = 199.00m, Stock = 50 },
        new() { Name = "架构师训练营", Price = 999.00m, Stock = 20 },
        new() { Name = "技术大会门票", Price = 1299.00m, Stock = 8 },
        new() { Name = "贴纸礼包", Price = 29.90m, Stock = 200 },
    ];

    private static Order[] Orders =>
    [
        new() { UserId = 1, Status = OrderStatus.Paid, CreatedAt = new(2025, 3, 1, 10, 20, 0) },
        new() { UserId = 1, Status = OrderStatus.Shipped, CreatedAt = new(2025, 5, 16, 9, 5, 0) },
        new() { UserId = 2, Status = OrderStatus.Paid, CreatedAt = new(2025, 4, 2, 14, 30, 0) },
        new() { UserId = 3, Status = OrderStatus.Paid, CreatedAt = new(2025, 4, 20, 20, 10, 0) },
        new() { UserId = 4, Status = OrderStatus.Cancelled, CreatedAt = new(2025, 6, 1, 8, 0, 0) },
        new() { UserId = 7, Status = OrderStatus.Pending, CreatedAt = new(2025, 7, 11, 16, 45, 0) },
        new() { UserId = 7, Status = OrderStatus.Paid, CreatedAt = new(2025, 8, 3, 11, 15, 0) },
        new() { UserId = 8, Status = OrderStatus.Paid, CreatedAt = new(2025, 8, 20, 19, 0, 0) },
    ];

    private static OrderItem[] OrderItems =>
    [
        Item(1, 1, 1, 299.00m), Item(1, 6, 2, 29.90m), Item(2, 3, 1, 199.00m), Item(3, 4, 1, 999.00m), Item(3, 5, 1, 1299.00m), Item(4, 3, 2, 199.00m),
        Item(5, 2, 1, 99.00m), Item(6, 1, 1, 299.00m), Item(7, 3, 1, 179.00m), Item(7, 6, 5, 29.90m), Item(8, 2, 1, 99.00m), Item(8, 3, 1, 199.00m),
    ];

    private static OrderItem Item(long order, long product, int quantity, decimal price) =>
        new() { OrderId = order, ProductId = product, Quantity = quantity, Price = price };

    private static Doc[] Docs => [new() { Title = "接入指南", CreatedBy = "林晓" }, new() { Title = "发布清单", CreatedBy = "周航" }];

    // 两个租户；这里是明文，库里存的是密文
    private static Ticket[] Tickets =>
    [
        new() { TenantId = 1, Title = "发票抬头写错了", Contact = "13800000001" },
        new() { TenantId = 1, Title = "想开通团队版", Contact = "13800000002" },
        new() { TenantId = 1, Title = "导出报表很慢", Contact = "13800000001" },
        new() { TenantId = 2, Title = "登录不上", Contact = "13900000001" },
        new() { TenantId = 2, Title = "要改绑手机号", Contact = "13900000002" },
    ];
}
