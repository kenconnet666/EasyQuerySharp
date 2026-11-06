namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>Java 的 mysql8/BaseTest 与 mssql/MsSQLBaseTest 写入的同一份银行数据：三家银行、五张卡、两个用户。</summary>
public static class BankSeed
{
    /// <summary>写入银行、银行卡、用户（表须已建好）。</summary>
    public static async Task InsertAsync(EasyDb db)
    {
        await db.Insert(new[]
        {
            new SysBank { Id = "1", Name = "工商银行", CreateTime = new DateTime(2000, 1, 1) },
            new SysBank { Id = "2", Name = "建设银行", CreateTime = new DateTime(2001, 1, 1) },
            new SysBank { Id = "3", Name = "招商银行", CreateTime = new DateTime(2002, 1, 1) },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new SysBankCard { Id = "bc1", Uid = "u1", Code = "123", Type = "储蓄卡", BankId = "1", OpenTime = new DateTime(2000, 1, 2) },
            new SysBankCard { Id = "bc2", Uid = "u1", Code = "1234", Type = "信用卡", BankId = "1", OpenTime = new DateTime(2000, 1, 2) },
            new SysBankCard { Id = "bc3", Uid = "u2", Code = "1235", Type = "信用卡", BankId = "1", OpenTime = new DateTime(2000, 1, 2) },
            new SysBankCard { Id = "bc4", Uid = "u1", Code = "1236", Type = "储蓄卡", BankId = "2", OpenTime = new DateTime(2001, 1, 2) },
            new SysBankCard { Id = "bc5", Code = "1237", Type = "储蓄卡", BankId = "2", OpenTime = new DateTime(2001, 1, 2) },
        }).ExecuteAsync();
        await db.Insert(new[]
        {
            new SysUser { Id = "u1", Name = "用户1", Phone = "123", Age = 22, CreateTime = new DateTime(2012, 1, 1) },
            new SysUser { Id = "u2", Name = "用户2", Phone = "1234", Age = 23, CreateTime = new DateTime(2012, 1, 1) },
        }).ExecuteAsync();
    }
}
