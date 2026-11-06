# EasyQuerySharp 工作约定

easy-query（Java，`C:\Users\Administrator\IdeaProjects\easy-query`，软 fork）的 C# 实现，参考 TS 版（`C:\Users\Administrator\RiderProjects\easy-query-ts`）。
规划在 [ROADMAP.md](ROADMAP.md)，文档索引在 [docs/README.md](docs/README.md)。每次开工先读 ROADMAP 和相关的 decisions。

## 不变的原则

- **能力完整对标**：Java 的 2404 个用例（上游 `sql-test` 2315 个，fork `sql-fork-test` 89 个）每个都要有对应的 C# 测试，用 `[Upstream("文件#方法")]` 标注，见 [0003](docs/decisions/0003-testing-and-parity.md)。
- **SQL 文本与 Java 逐字对齐**：期望 SQL 直接从 Java 用例搬过来。偏离必须写 `Deviation` 说明理由。
- **API 用 C# 的方式写**：POCO 实体加表达式树，不复刻 Java 的 Proxy，见 [0001](docs/decisions/0001-query-api-route.md)。函数做成扩展方法，不用扩展属性（扩展属性写不进表达式树）。
- **对象树只走一条管线**：Include、DTO、VO、树最后都归一成投影表达式，交给同一个规划器，见 [0002](docs/decisions/0002-object-tree-and-dto.md)。
- **核心零第三方依赖**：驱动只在方言包里引用。新增第三方依赖要先说明理由。
- **新能力必须在三种场景下都能用**：普通 where、隐式子查询（any / count）、投影到 DTO（对齐 Java fork 的原则）。

## 每个工作项的流程

1. 在 ROADMAP 里认领一项，开分支，名字用 `feat/…`、`fix/…`、`chore/…`、`docs/…`。
2. 先看 Java 用例和 TS 实现：用 `tools/ParityTests` 列出这一项对应的 Java 用例，读 TS 的同名能力和它的 ROADMAP 记录。
3. 先写测试：用 `--scaffold` 生成骨架，期望 SQL 用 Java 原文，再补上 C# 查询。
4. 实现。
5. 写文档：能力页加示例卡片，然后重新生成 SQL 快照。
6. 重新生成 `docs/parity-tests.md`；推送并开 PR；CI 全部通过后 squash 合并，更新 ROADMAP 的状态。

本地只跑和改动相关的测试，全量交给 CI。推送后不用等，下次提交前先用 `gh run list --limit 1` 看上一次的结果，有失败先修。

遇到以下情况，在 ROADMAP 对应项下标 `[!]` 并写明原因，然后换下一个独立的项，不要猜：
- 规划之外的新公共概念；
- 新增第三方依赖；
- Java 用例的语义不清楚，或者怀疑 Java 本身有 bug；
- 实库环境起不来；
- 需要账号或凭据的事。

## 环境

- .NET 10 SDK 装在 `~\.dotnet`。PATH 上的 `C:\Program Files\dotnet` 只有 11 preview，按 `global.json` 解析不到 10。所以命令行要显式调用 `& "$HOME\.dotnet\dotnet.exe"`，或者先把 `~\.dotnet` 放到 PATH 前面。
- 数据库在 WSL Debian 的 Docker 里，compose 项目名 `easy-query-sharp-dev`，端口 4xxxx。密码和连接串在 `.local/db.env`，不提交，换机器时要重新写（`db.sh` 首次运行会生成）。

## 常用命令

```powershell
# 构建与全部测试（本地必测 SQLite / MySQL 8.4 / PG 17，其余数据库没配置就跳过）
& "$HOME\.dotnet\dotnet.exe" build
& "$HOME\.dotnet\dotnet.exe" test
# 像 CI 一样要求某些库必须连上
$env:EQS_REQUIRE_DBS='sqlite,mysql,pg'; & "$HOME\.dotnet\dotnet.exe" test
```

```bash
# 数据库（在 WSL 里执行；Windows 下前面加 wsl -d Debian --）
bash infra/dev/db.sh up -d                    # MySQL 8.4 + PG 17
bash infra/dev/db.sh --profile mssql up -d    # 按需：polardb / mariadb / mssql / oracle / dameng / opengauss / clickhouse / db2 / tdengine / hana
bash infra/dev/db.sh stop
```

按 profile 启动的库，还要在 `.local/db.env` 里取消对应 `EQS_*_CS` 那一行的注释。

## 防膨胀

- 边角情况记一行进 [docs/BACKLOG.md](docs/BACKLOG.md)；正确性 bug 不在此列，要直接修。
- 同一个变化点出现第三次才抽象；只有一个实现的接口不要加。
- 文档写用法、支持矩阵和限制，不写过程流水账。
