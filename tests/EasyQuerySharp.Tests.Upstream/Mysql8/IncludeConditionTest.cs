namespace EasyQuerySharp.Tests.Upstream.Mysql8;

/// <summary>
/// 对应 Java：mysql8/IncludeConditionTest.java（<c>[IncludeOn]</c>，Java 的 @IncludeOnProperty：按父对象的 version 决定带出哪张子表 / 扩展表）。
/// 不满足条件的父对象不进 IN 列表，没有父对象满足时整条子查询都不发；DTO 没选条件列时追加 <c>__relation__version</c>。
/// </summary>
public sealed class IncludeConditionTest(Mysql8Fixture fixture) : Mysql8Test(fixture)
{
    private const string SubColumns = "t.`id`,t.`task_id`,t.`name`";

    private async Task SeedAsync()
    {
        Type[] tables = [typeof(MyTask), typeof(MySubTask1), typeof(MySubTask2), typeof(MyTask1Ext), typeof(MyTask2Ext)];
        await Db.CodeFirst.DropTablesIfExistsAsync(tables, Ct);
        await Db.CodeFirst.CreateTablesAsync(tables, Ct);
        await Db.Insert(new MyTask { Id = "v1", Name = "任务1", Version = 1 }, new MyTask { Id = "v2", Name = "任务2", Version = 2 }, new MyTask { Id = "v3", Name = "任务3", Version = 3 }).ExecuteAsync(Ct);
        await Db.Insert(new MySubTask1 { Id = "v1s1", TaskId = "v1", Name = "任务1的子任务1" }, new MySubTask1 { Id = "v1s2", TaskId = "v1", Name = "任务1的子任务2" }).ExecuteAsync(Ct);
        await Db.Insert(new MySubTask2 { Id = "v2s1", TaskId = "v2", Name = "任务2的子任务1" }, new MySubTask2 { Id = "v2s2", TaskId = "v2", Name = "任务2的子任务1" }).ExecuteAsync(Ct);
        await Db.Insert(new MyTask1Ext { Id = "v1", TaskId = "v1", Name = "任务1的扩展1" }).ExecuteAsync(Ct);
        await Db.Insert(new MyTask2Ext { Id = "v2", TaskId = "v2", Name = "任务2的扩展1" }).ExecuteAsync(Ct);
    }

    private Query<MyTask> AllIncludes() =>
        Db.Query<MyTask>().Include(m => m.MyTask1Ext).Include(m => m.MyTask2Ext).Include(m => m.MySubTask1List).Include(m => m.MySubTask2List);

    [Fact, Upstream("mysql8/IncludeConditionTest.java#testInclude1")]
    public async Task TestInclude1()
    {
        await SeedAsync();
        using var listen = SqlCapture.Listen();
        var list = (await AllIncludes().ToListAsync(Ct)).OrderBy(t => t.Id).ToList();
        Assert.Equal(["v1s1", "v1s2"], list[0].MySubTask1List!.Select(s => s.Id));
        Assert.Null(list[0].MySubTask2List);
        Assert.Equal("任务1的扩展1", list[0].MyTask1Ext!.Name);
        Assert.Null(list[0].MyTask2Ext);
        Assert.Null(list[1].MySubTask1List);
        Assert.Equal(["v2s1", "v2s2"], list[1].MySubTask2List!.Select(s => s.Id));
        Assert.Equal("任务2的扩展1", list[1].MyTask2Ext!.Name);
        Assert.True(list[2] is { MySubTask1List: null, MySubTask2List: null, MyTask1Ext: null, MyTask2Ext: null });

        Assert.Equal(5, listen.Events.Count);
        listen.AssertSql(0, "SELECT `id`,`name`,`version` FROM `my_task`");
        listen.AssertSql(1, $"SELECT {SubColumns} FROM `my_task1_ext` t WHERE t.`task_id` IN (?)");
        listen.AssertParams(1, "v1(String)");
        listen.AssertSql(2, $"SELECT {SubColumns} FROM `my_task2_ext` t WHERE t.`task_id` IN (?)");
        listen.AssertParams(2, "v2(String)");
        listen.AssertSql(3, $"SELECT {SubColumns} FROM `my_task_sub1` t WHERE t.`task_id` IN (?)");
        listen.AssertParams(3, "v1(String)");
        listen.AssertSql(4, $"SELECT {SubColumns} FROM `my_task_sub2` t WHERE t.`task_id` IN (?)");
        listen.AssertParams(4, "v2(String)");
    }

    [Fact, Upstream("mysql8/IncludeConditionTest.java#testInclude2")]
    public async Task TestInclude2()
    {
        await SeedAsync();
        using var listen = SqlCapture.Listen();
        var task = Assert.Single(await AllIncludes().Where(m => m.Id == "v1").ToListAsync(Ct));
        Assert.Equal(2, task.MySubTask1List!.Count);
        Assert.Equal("任务1的扩展1", task.MyTask1Ext!.Name);
        Assert.True(task is { MySubTask2List: null, MyTask2Ext: null });

        Assert.Equal(3, listen.Events.Count);
        listen.AssertSql(0, "SELECT `id`,`name`,`version` FROM `my_task` WHERE `id` = ?");
        listen.AssertParams(0, "v1(String)");
        listen.AssertSql(1, $"SELECT {SubColumns} FROM `my_task1_ext` t WHERE t.`task_id` IN (?)");
        listen.AssertSql(2, $"SELECT {SubColumns} FROM `my_task_sub1` t WHERE t.`task_id` IN (?)");
    }

    /// <summary>对应 Java：mysql8/dto/MyTaskDTO1（不含 version）。</summary>
    public sealed class MyTaskDTO1
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public List<InternalMySubTask>? MySubTask1List { get; set; }

        public InternalMyTaskExt? MyTask2Ext { get; set; }
    }

    /// <summary>对应 Java：mysql8/dto/MyTaskDTO2（含 version）。</summary>
    public sealed class MyTaskDTO2
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public long? Version { get; set; }

        public List<InternalMySubTask>? MySubTask1List { get; set; }

        public InternalMyTaskExt? MyTask2Ext { get; set; }
    }

    public sealed class InternalMySubTask
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    public sealed class InternalMyTaskExt
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    // DTO 没有 version：主查询追加 version AS __relation__version 来判断条件
    [Fact, Upstream("mysql8/IncludeConditionTest.java#testInclude3")]
    public async Task TestInclude3()
    {
        await SeedAsync();
        using var listen = SqlCapture.Listen();
        var dto = Assert.Single(await Db.Query<MyTask>().Where(m => m.Id == "v1").SelectAutoInclude<MyTaskDTO1>().ToListAsync(Ct));
        Assert.Equal(["v1s1", "v1s2"], dto.MySubTask1List!.Select(s => s.Id));
        Assert.Null(dto.MyTask2Ext);

        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`version` AS `__relation__version` FROM `my_task` t WHERE t.`id` = ?");
        listen.AssertParams(0, "v1(String)");
        listen.AssertSql(1, "SELECT t.`id`,t.`name`,t.`task_id` AS `__relation__taskId` FROM `my_task_sub1` t WHERE t.`task_id` IN (?)");
        listen.AssertParams(1, "v1(String)");
    }

    [Fact, Upstream("mysql8/IncludeConditionTest.java#testInclude4")]
    public async Task TestInclude4()
    {
        await SeedAsync();
        using var listen = SqlCapture.Listen();
        var dto = Assert.Single(await Db.Query<MyTask>().Where(m => m.Id == "v2").SelectAutoInclude<MyTaskDTO2>().ToListAsync(Ct));
        Assert.Null(dto.MySubTask1List);
        Assert.Equal("任务2的扩展1", dto.MyTask2Ext!.Name);

        Assert.Equal(2, listen.Events.Count);
        listen.AssertSql(0, "SELECT t.`id`,t.`name`,t.`version` FROM `my_task` t WHERE t.`id` = ?");
        listen.AssertParams(0, "v2(String)");
        listen.AssertSql(1, "SELECT t.`id`,t.`name`,t.`task_id` AS `__relation__taskId` FROM `my_task2_ext` t WHERE t.`task_id` IN (?)");
        listen.AssertParams(1, "v2(String)");
    }
}
