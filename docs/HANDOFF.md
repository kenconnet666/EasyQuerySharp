# 交接记录（2026-11-06）

换机器继续开发时先读这一页，再读 [ROADMAP](../ROADMAP.md) 和相关的 decisions。读完、接上之后，把本页更新成新的状态，或者删掉。

## 新机器的环境

- 装 .NET 10 SDK 到 `~\.dotnet`，命令行用 `& "$HOME\.dotnet\dotnet.exe"`（PATH 上可能只有 11 preview），见 CLAUDE.md。
- 数据库：WSL Debian + Docker，执行 `bash infra/dev/db.sh up -d`，起 MySQL 8.4 和 PG 17。
- 新建 `.local/db.env` 写连接串。这个文件不提交，要在新机器上重新写；格式参考 `infra/dev` 下的说明和 CI 的 env。
  - 本机还取消了 `EQS_MSSQL_CS` 的注释，SQL Server 容器用 `--profile mssql` 启动。
- 这几个仓库要放在相同的相对位置：Java 源 `IdeaProjects/easy-query`，TS 参考 `IdeaProjects/easy-query-ts`。

## 当前进度

- 阶段 D 正在做 **5.11 聚合根保存**。Java 的 M8SaveTest、M8Save2Test、M8Save3Test 已经全部移植。
- mysql8 版的 M8Save4Test 也已移植。本地 M8Save* 的 42 个用例全部通过，这次推送的 CI 结果到新机器上用 `gh run list --limit 1` 查看。
- parity（已重新生成）：Todo 370 / Pending 6 / Ported 1726 / Deviation 224 / NotApplicable 78。

### 最近的修复

修改在 `src/EasyQuerySharp/Commands/SavePlan.cs`：

- 新建的对象沿用已追踪实体的主键时，`SaveOwned` 先调用 `Canonical(item)`，把它换成那个已追踪的实例。
- `Merge` 除了合并列值，也合并导航。
- 效果：这类对象按快照的差异更新；挂到别的父对象下时，只改外键。

duckdb 版的 `duckdb/M8Save4Test` 归入 6.6 DuckDB，不在这里做。

## 接下来的顺序

1. 查 CI → 把 ROADMAP 里的 5.11 标 [x]，在 0004 / 能力页补上"同主键新对象按已追踪实例处理" → 提交，查 CI。
2. 阶段 D 剩下的：5.4 值对象、5.10 Changes、5.7 DbBatch、write.strategy。
3. 阶段 E，方言。
   - 先做 MSSQL 的 Upstream fixture：38 个 MsSQLQueryTest / UpdateJoin 用例，需要 UpperCamelCase 命名；CI 的 mssql job 加上 Upstream Mssql 命名空间的过滤。
   - 然后依次是 mssql2008、dameng、h2→sqlite、duckdb（含 M8Save4Test 的 duckdb 版）、hana、clickhouse、kingbase、returning、lock 冒烟。
4. 阶段 F：表映射、分库分表（先写决策 0005）、search、code-first、cache 7.7、DI。之后做 Source Generator 加 AOT（1.1）。
5. 零散的 Todo：
   - QueryTest25 testToListFlat1 / test11a；
   - RelationTest testOneToOne、QueryTestRelationTest testRelation；
   - M8Test1 testRelationColumns（find_in_set）。

## 工作约定（摘要）

- 本地只跑和改动相关的测试，全量交给 CI。推送后不用等，下次提交前先看 `gh run list --limit 1`，有失败先修。
- commit 用英文 `feat(core): …` 风格，结尾加 `Co-Authored-By` 行。重构和功能分开提交。
- 遇到规划之外的新公共概念、新的第三方依赖、Java 语义不清楚的情况，在 ROADMAP 里标 `[!]` 并跳过。
