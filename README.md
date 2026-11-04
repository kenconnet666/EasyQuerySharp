# EasyQuerySharp

[easy-query](https://github.com/dromara/easy-query) 的 C# 实现，目前处于规划和地基阶段（见 [ROADMAP](ROADMAP.md)）。

目标：完整对标 easy-query 的能力，包括隐式 join、隐式子查询、DTO 和 VO 对象树、聚合根保存、分库分表和多方言；API 用 C# 的方式写，也就是 POCO 实体加表达式树：

```csharp
var page = await db.Query<User>()
    .Where(u => u.Name.Contains(keyword) && u.Company!.Name.StartsWith("Acme"))   // 隐式 JOIN
    .WhereIf(minAge != null, u => u.Age >= minAge)
    .Where(u => u.Posts.Any(p => p.Published))                                   // 隐式子查询
    .Select(u => new UserVo(u.Id, u.Name, u.Posts.Select(p => p.ToVo()).ToList())) // 对象树，每层一次批量查询
    .ToPageAsync(1, 20, ct);
```

上面是规划中的写法，还没有实现。

## 开发

- 需要 .NET 10 SDK，以及 WSL 里的 Docker（实库测试用）。
- 本地数据库：`bash infra/dev/db.sh up -d`，启动 MySQL 8.4 和 PostgreSQL 17。
- 构建和测试：`dotnet build`、`dotnet test`。
- 工作约定见 [CLAUDE.md](CLAUDE.md)，设计文档见 [docs/README.md](docs/README.md)。
