---
name: release
description: 发布 EasyQuerySharp 的新版本到 nuget.org（正式版或预览版）：核对版本号与 CHANGELOG、推 tag 触发 Release 流程、盯结果、确认三个包上线、发版后升版本号。用户说“发版”“发布 vX.Y.Z”“推到 nuget”时使用。
---

# 发布新版本

发布全自动：推一个 `v*` tag，`.github/workflows/release.yml` 会核对版本、等 CI、打包、用 nuget.org 的可信发布推送、建 GitHub Release。这里的步骤只是把 tag 推对，并确认结果。

一次发三个包：`EasyQuerySharp`（生成器与分析器在包里）、`EasyQuerySharp.DependencyInjection`、`EasyQuerySharp.Caching`，各带 `.snupkg`。版本号统一，三个包一起发。

## 1. 定版本号

- 按语义化版本：修 bug 升补丁位；加公开成员（`PublicAPI.Unshipped.txt` 不为空）至少升次版本位；改动或删除 `PublicAPI.Shipped.txt` 里的成员是破坏性变更，要先跟用户确认。
- 预览版写成 `1.1.0-rc.1`。流程只拿 `-` 之前的部分和 `VersionPrefix` 比，GitHub Release 自动标成预发布。
- 版本号由用户定；用户没说时，按上面的规则提一个，确认后再继续。

## 2. 改版本号与 CHANGELOG

- `src/Directory.Build.props` 的 `VersionPrefix` 改成要发的版本（不带预览后缀）。
- `CHANGELOG.md`：把 `## [Unreleased]` 下的内容挪到新的 `## [X.Y.Z] - 日期`（日期用当天），`[Unreleased]` 留空；文末的链接改成 `[Unreleased]: …/compare/vX.Y.Z...HEAD`，再加一行 `[X.Y.Z]: …/releases/tag/vX.Y.Z`。Release 流程用 awk 截取 `## [X.Y.Z]` 到下一个 `## [` 之间的内容作为 Release 说明，标题格式不能改。
- 发正式版时，`PublicAPI.Unshipped.txt` 的条目挪进 `PublicAPI.Shipped.txt`（三个包各一份），Unshipped 留空。分析器规则同理：`src/EasyQuerySharp.Generator/AnalyzerReleases.Unshipped.md` 的条目挪进 `AnalyzerReleases.Shipped.md`，标题写 `## Release X.Y.Z`。
- 本地验证，看到 3 个 `.nupkg`：

```powershell
& "$HOME\.dotnet\dotnet.exe" pack -c Release -o artifacts/packages -p:Version=X.Y.Z
```

## 3. 推到 master，等 CI 通过

```bash
git commit -am "release: X.Y.Z"
git push origin master
gh run list --workflow CI --branch master --limit 1
```

Release 流程要求 tag 指向的提交在 master 上有一次 push 触发的 CI 运行。没有这次运行，流程会直接失败；CI 还在跑时，流程最多等 100 分钟，所以不必先等 CI 跑完再推 tag。CI 最慢的是 HANA 任务（约 9 分钟）。

## 4. 推 tag

```bash
git tag -a vX.Y.Z -m "EasyQuerySharp X.Y.Z"
git push origin vX.Y.Z
```

推 tag 不需要另外问用户：用户说要发布，就已经包含了推 tag。

## 5. 盯结果

把下面的命令挂到后台（`run_in_background`），完成后看各步骤的结果：

```bash
id=$(gh run list --workflow Release --limit 1 --json databaseId --jq '.[0].databaseId')
until [ "$(gh run view $id --json status --jq .status)" = completed ]; do sleep 30; done
gh run view $id --json conclusion,jobs --jq '"\(.conclusion)", (.jobs[].steps[] | "\(.name): \(.conclusion)")'
```

推送日志里每个文件都有 `Created` 和 `Your package was pushed.`，说明 nuget.org 已经收下。

## 6. 确认上线

包推上去后，nuget.org 还要校验、建索引，通常十几分钟，核心包最慢。这段时间包页面和下载地址都是 404，是正常的，不要重推。在后台等三个包都出现：

```bash
for i in $(seq 1 115); do ok=0
  for id in easyquerysharp easyquerysharp.dependencyinjection easyquerysharp.caching; do
    curl -sf https://api.nuget.org/v3-flatcontainer/$id/index.json | grep -q '"X.Y.Z"' && ok=$((ok+1))
  done
  [ $ok -eq 3 ] && break; sleep 30
done; echo "indexed: $ok/3"
```

nuget.org 也会给账号邮箱发 “Package published” 邮件，一个包一封。搜索结果更新得更晚，不影响安装。

## 7. 发版后

- `VersionPrefix` 改成下一个补丁版本，`CHANGELOG.md` 恢复空的 `## [Unreleased]`，提交为 `chore: start X.Y.(Z+1) after releasing X.Y.Z`。
- 告诉用户：包页面链接、GitHub Release 链接、安装命令。

## 出错时

| 失败的步骤 | 原因与处理 |
|---|---|
| Check version | tag 与 `VersionPrefix` 不一致，或 CHANGELOG 没有 `## [X.Y.Z]`。改好、提交推送，然后删掉 tag 重推（见下）。 |
| Wait for CI on this commit | 这个提交没推到 master，或 CI 失败。先修 CI，修复提交推上去后，把 tag 移到新提交上。 |
| Check package layout | 生成器不在 `analyzers/dotnet/cs`，或包的数量不是 3。检查 `src/EasyQuerySharp/EasyQuerySharp.csproj` 对生成器的引用。 |
| NuGet login | 可信发布的策略对不上：策略锁定了仓库 `kenconnet666/EasyQuerySharp`、工作流文件名 `release.yml`、环境 `release`。改了其中任何一项，都要请用户在 nuget.org → Trusted Publishing 里同步修改。secret `NUGET_USER` 是 nuget.org 的用户名，不是邮箱。 |
| Push to nuget.org | 版本已经存在时，`--skip-duplicate` 会跳过；其余错误看日志。nuget.org 上的版本不能删除，也不能覆盖，只能下架（unlist），所以修复后要发新的版本号。 |
| GitHub Release | 包已经推上去了，只是 Release 没建成：可以手动执行 workflow 最后一步的 `gh release create`，或者重跑这个任务（推送时会跳过已有的包）。 |

在 Push to nuget.org 之前失败时，什么都还没发出去。改好后删掉 tag 重推：

```bash
git tag -d vX.Y.Z
git push origin :refs/tags/vX.Y.Z
git tag -a vX.Y.Z -m "EasyQuerySharp X.Y.Z" <新提交>
git push origin vX.Y.Z
```

## 账号与设置（已配好，只在出问题时查）

- nuget.org 账号 kenconnet666：Trusted Publishing 策略见上表；包 ID 前缀 `EasyQuerySharp.*` 的保留要用户本人写信给 account@nuget.org 申请（可选）。
- GitHub 环境 `release`：只允许 `v*` tag 部署，不设审批人（用户要求直接发）。
- 不存长期 API key：`NuGet/login@v1` 用 GitHub OIDC 换一个 1 小时有效的 key。
