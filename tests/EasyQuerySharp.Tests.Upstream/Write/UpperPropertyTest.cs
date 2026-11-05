namespace EasyQuerySharp.Tests.Upstream.Write;

/// <summary>
/// 对应 Java：UpperPropertyTest.java（属性名大写开头、全大写、is 开头时的列映射与读写往返）。
/// Java 要处理 JavaBean 属性名的推断规则；C# 的属性名就是声明的名字，按命名约定或 <c>[Column]</c> 对到列。
/// </summary>
public sealed class UpperPropertyTest(MySqlFixture fixture) : MySqlTest(fixture)
{
    [Fact, Upstream("UpperPropertyTest.java#test1", Deviation = "Java 的库是宽松模式，MyTopicTestDTO 不写 create_time 也能插入；MySQL 8.4 严格模式下改为比对插入 SQL、读取已有的行")]
    public async Task Test1()
    {
        var meta = Db.Metadata.Get<UpperTopic>();
        Assert.Equal(["ID", "Stars", "Title", "CreateTime"], meta.Columns.Select(c => c.PropertyName));
        foreach (var topic in await Db.Query<UpperTopic>().ToListAsync(Ct))
        {
            Assert.NotNull(topic.ID);
            Assert.NotNull(topic.Stars);
            // Java 还断言 9、10 号的标题为空：依赖别的用例改过的种子数据，这里只查其余的
            if (topic.ID is not ("10" or "9")) Assert.NotNull(topic.Title);
            Assert.NotNull(topic.CreateTime);
        }

        const string id = "xxxxxxaaappll";
        await Db.Delete<UpperTopic>().WhereById(id).ExecuteAsync(Ct);
        try
        {
            var upper = new UpperTopic { ID = id, Title = id, Stars = 123, CreateTime = DateTime.Now };
            await Db.Insert(upper).ExecuteAsync(Ct);
            var read = await Db.Query<UpperTopic>().Where(u => u.ID == id).SingleAsync(Ct);
            Assert.Equal((upper.ID, upper.Stars, upper.Title), (read.ID, read.Stars, read.Title));
            await Db.Delete(upper).ExecuteAsync(Ct);
        }
        finally
        {
            await Db.Delete<UpperTopic>().WhereById(id).ExecuteAsync(Ct);
        }

        // 属性名与列名完全不同的实体：插入只比对 SQL（t_topic 的 create_time 非空，MySQL 8.4 严格模式下不能缺省），读取用已有的行
        SqlOnly.AssertStatement(Db.Insert(new MyTopicTestDTO { ECgi = id, SQL = id, Stars = 123 }).ToStatement(),
            "INSERT INTO `t_topic` (`id`,`title`,`stars`) VALUES (?,?,?)", $"{id}(String),{id}(String),123(Integer)");
        var existing = await Db.Query<Topic>().WhereById("1").FirstAsync(Ct);
        var mapped = await Db.Query<MyTopicTestDTO>().Where(u => u.ECgi == "1").SingleAsync(Ct);
        Assert.Equal((existing.Id, existing.Title, existing.Stars), (mapped.ECgi, mapped.SQL, mapped.Stars));
    }

    [Fact, Upstream("UpperPropertyTest.java#test2")]
    public async Task Test2()
    {
        var meta = Db.Metadata.Get<MyALLTYPE>();
        Assert.Equal(["id", "is_number_decimal", "is_number_integer", "is_enable", "is_number_integer_basic", "is_enable_basic", "is_time_local_date_time", "is_value"],
            meta.Columns.Select(c => c.Name));

        const string id = "xxxxxxaaappll";
        await Db.Delete<MyALLTYPE>().WhereById(id).ExecuteAsync(Ct);
        try
        {
            var all = new MyALLTYPE
            {
                Id = id, IsNumberDecimal = 123.32m, IsNumberInteger = 999, IsEnable = true, IsNumberIntegerBasic = -999, IsEnableBasic = true,
                IsTimeLocalDateTime = new DateTime(2024, 1, 1, 1, 1, 0), IsValue = "123",
            };
            await Db.Insert(all).ExecuteAsync(Ct);
            var read = await Db.Query<MyALLTYPE>().Where(u => u.Id == id).SingleAsync(Ct);
            Assert.Equal((all.Id, all.IsEnable, all.IsEnableBasic, all.IsNumberDecimal, all.IsNumberIntegerBasic, all.IsValue),
                (read.Id, read.IsEnable, read.IsEnableBasic, read.IsNumberDecimal, read.IsNumberIntegerBasic, read.IsValue));
            await Db.Delete(all).ExecuteAsync(Ct);
        }
        finally
        {
            await Db.Delete<MyALLTYPE>().WhereById(id).ExecuteAsync(Ct);
        }
    }
}
