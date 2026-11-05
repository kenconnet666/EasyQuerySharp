using System.Linq.Expressions;
using EasyQuerySharp.Metadata;

namespace EasyQuerySharp.Tests.Upstream.Mysql8;

// 自动映射的平铺带出（Java 的 @NavigateFlat，C# 的 [MapPath]）、带出的分组大小与配置参数
public sealed partial class M8BankTest
{
    /// <summary>对应 Java：mysql8/vo/SysBankCardDTO（卡上平铺所属用户的手机号和姓名）。</summary>
    public sealed class SysBankCardDTO
    {
        public string? Id { get; set; }

        public string? Uid { get; set; }

        public string? Code { get; set; }

        public string? Type { get; set; }

        public string? BankId { get; set; }

        public DateTime? OpenTime { get; set; }

        [MapPath("User.Phone")]
        public string? UserPhone { get; set; }

        [MapPath("User.Name")]
        public string? UserName { get; set; }
    }

    // 同一导航上的两个平铺属性共用一条带出查询，单表不写别名；分组大小为 1 时每个用户一条
    [Fact, Upstream("mysql8/M8BankTest.java#partitionBy")]
    public async Task PartitionBy()
    {
        await Db.Query<SysUser>().Select(u => new Part<SysUser, string?>(u, u.BankCards!.OrderBy(c => c.OpenTime).First().Code)).ToListAsync(Ct);

        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysBankCard>().IncludeGroupSize(1).SelectAutoInclude<SysBankCardDTO>().ToListAsync(Ct);
        listen.AssertSql(0, "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t");
        listen.AssertSql(1, "SELECT `phone`,`name`,`id` AS `__relation__id` FROM `t_sys_user` WHERE `id` IN (?)");
        listen.AssertParams(1, "u1(String)");
        listen.AssertSql(2, "SELECT `phone`,`name`,`id` AS `__relation__id` FROM `t_sys_user` WHERE `id` IN (?)");
        listen.AssertParams(2, "u2(String)");
        Assert.All(list.Where(c => c.Uid is not null), c => Assert.NotNull(c.UserName));
    }

    /// <summary>对应 Java：mysql8/vo/SysBankDTO（银行 → 卡 → 卡所属用户，用户上多取书的统计）。</summary>
    public sealed class SysBankDTO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public DateTime? CreateTime { get; set; }

        public List<InternalBankCards>? BankCards { get; set; }

        /// <summary>SysBankCard 的投影。</summary>
        public sealed class InternalBankCards
        {
            public string? Id { get; set; }

            public string? Uid { get; set; }

            public string? Code { get; set; }

            public string? Type { get; set; }

            public string? BankId { get; set; }

            public DateTime? OpenTime { get; set; }

            public InternalUser? User { get; set; }
        }

        /// <summary>SysUser 的投影：书合并成分组 JOIN，按配置参数排除同名用户，多取书数和最贵的一本。</summary>
        public sealed class InternalUser : IAutoIncludeConfigure<SysUser>
        {
            public static Query<SysUser> Configure(Query<SysUser> query)
            {
                var arg = (string?)query.ConfigureArgument;
                return query.GroupJoin(u => u.UserBooks).Where(u => u.Name != arg);
            }

            private static Expression<Func<SysUser, long>> BookCountSql => u => u.UserBooks!.LongCount();

            private static Expression<Func<SysUser, string?>> BookNameSql => u => u.UserBooks!.OrderByDescending(b => b.Price).First().Name;

            private static Expression<Func<SysUser, decimal?>> BookPriceSql => u => u.UserBooks!.OrderByDescending(b => b.Price).First().Price;

            public string? Id { get; set; }

            public string? Name { get; set; }

            public string? Phone { get; set; }

            public int? Age { get; set; }

            public DateTime? CreateTime { get; set; }

            [MapExpression(nameof(BookCountSql))]
            public long? BookCount { get; set; }

            [MapExpression(nameof(BookNameSql))]
            public string? BookName { get; set; }

            [MapExpression(nameof(BookPriceSql))]
            public decimal? BookPrice { get; set; }
        }
    }

    // 配置参数传进嵌套 DTO 的带出配置：用户的带出查询按它加条件
    [Fact, Upstream("mysql8/M8BankTest.java#testExtraDTO")]
    public async Task TestExtraDTO()
    {
        using var listen = SqlCapture.Listen();
        await Db.Query<SysBank>().WithConfigureArgument("myArg").SelectAutoInclude<SysBankDTO>().ToListAsync(Ct);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t");
        listen.AssertSql(1, "SELECT t.`id`,t.`uid`,t.`code`,t.`type`,t.`bank_id`,t.`open_time` FROM `t_bank_card` t WHERE t.`bank_id` IN (?,?,?)");
        listen.AssertParams(1, "1(String),2(String),3(String)");
        listen.AssertSql(2, "SELECT t.`id`,t.`name`,t.`phone`,t.`age`,t.`create_time`,IFNULL(t2.`__count2__`,0) AS `book_count`,t5.`name` AS `book_name`,t5.`price` AS `book_price` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_sys_user_book` t1 WHERE t1.`uid` IN (?,?) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` LEFT JOIN (SELECT t3.`id`,t3.`name`,t3.`uid`,t3.`price`,(ROW_NUMBER() OVER (PARTITION BY t3.`uid` ORDER BY t3.`price` DESC)) AS `__row__` FROM `t_sys_user_book` t3 WHERE t3.`uid` IN (?,?)) t5 ON (t5.`uid` = t.`id` AND t5.`__row__` = ?) WHERE t.`name` <> ? AND t.`id` IN (?,?)");
        listen.AssertParams(2, "u1(String),u2(String),u1(String),u2(String),1(Integer),myArg(String),u1(String),u2(String)");
    }

    /// <summary>对应 Java：mysql8/vo/SysBank2DTO（银行上平铺各卡的所属用户）。</summary>
    public sealed class SysBank2DTO
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public DateTime? CreateTime { get; set; }

        [MapPath("BankCards.User")]
        public List<InternalUser>? BankCardUsers { get; set; }

        /// <summary>SysUser 的投影：与 SysBankDTO.InternalUser 相同的配置与额外列。</summary>
        public sealed class InternalUser : IAutoIncludeConfigure<SysUser>
        {
            public static Query<SysUser> Configure(Query<SysUser> query) => SysBankDTO.InternalUser.Configure(query);

            private static Expression<Func<SysUser, long>> BookCountSql => u => u.UserBooks!.LongCount();

            private static Expression<Func<SysUser, string?>> BookNameSql => u => u.UserBooks!.OrderByDescending(b => b.Price).First().Name;

            private static Expression<Func<SysUser, decimal?>> BookPriceSql => u => u.UserBooks!.OrderByDescending(b => b.Price).First().Price;

            public string? Id { get; set; }

            public string? Name { get; set; }

            public string? Phone { get; set; }

            public DateTime? CreateTime { get; set; }

            [MapExpression(nameof(BookCountSql))]
            public long? BookCount { get; set; }

            [MapExpression(nameof(BookNameSql))]
            public string? BookName { get; set; }

            [MapExpression(nameof(BookPriceSql))]
            public decimal? BookPrice { get; set; }
        }
    }

    // 平铺路径经过集合再到对一导航：先查卡表把银行键换成用户键，再按用户键查用户
    [Fact, Upstream("mysql8/M8BankTest.java#testExtraDTO123")]
    public async Task TestExtraDTO123()
    {
        using var listen = SqlCapture.Listen();
        var list = await Db.Query<SysBank>().WithConfigureArgument("myArg").SelectAutoInclude<SysBank2DTO>().ToListAsync(Ct);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`create_time` FROM `t_bank` t");
        listen.AssertSql(1, "SELECT `uid` AS `__relation__uid`,`bank_id` AS `__relation__bankId` FROM `t_bank_card` WHERE `bank_id` IN (?,?,?)");
        listen.AssertParams(1, "1(String),2(String),3(String)");
        listen.AssertSql(2, "SELECT t.`id`,t.`name`,t.`phone`,t.`create_time`,IFNULL(t2.`__count2__`,0) AS `book_count`,t5.`name` AS `book_name`,t5.`price` AS `book_price` FROM `t_sys_user` t LEFT JOIN (SELECT t1.`uid` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_sys_user_book` t1 WHERE t1.`uid` IN (?,?) GROUP BY t1.`uid`) t2 ON t2.`__group_key1__` = t.`id` LEFT JOIN (SELECT t3.`id`,t3.`name`,t3.`uid`,t3.`price`,(ROW_NUMBER() OVER (PARTITION BY t3.`uid` ORDER BY t3.`price` DESC)) AS `__row__` FROM `t_sys_user_book` t3 WHERE t3.`uid` IN (?,?)) t5 ON (t5.`uid` = t.`id` AND t5.`__row__` = ?) WHERE t.`name` <> ? AND t.`id` IN (?,?)");
        listen.AssertParams(2, "u1(String),u2(String),u1(String),u2(String),1(Integer),myArg(String),u1(String),u2(String)");
        Assert.Contains(list, b => b.BankCardUsers is { Count: > 0 });
    }
}
