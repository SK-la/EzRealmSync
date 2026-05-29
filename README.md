# EzRealmSync

**独立** Ez Realm 双库同步工具（非 osu! 规则集）。

与 [Ez2Lazer/osu](../osu) 为**同级目录**，通过 **ProjectReference** 编译链接 **Ez2Lazer 版 `osu.Game`**（Framework UI、Realm 模型、后续 `OfficialRealmAccess` 等）。**不**编入 `osu.sln`，也**不是**规则集挂载。

## Git（独立仓库）

本目录是 **自己的 Git 仓库**，与 `../osu` **没有**嵌套关系。若在 Cursor 里同时打开 `osu` 与 `EzRealmSync`，请在源代码管理里选择 **EzRealmSync** 仓库再提交/推送，不要选 `osu`。

首次推送到 GitHub（仓库需先在网站上建好，例如 `Ez2Lazer/EzRealmSync`）：

```bash
cd EzRealmSync
git init   # 若尚未初始化
git add .
git commit -m "Initial EzRealmSync tool (Phase 1 UI)"
git branch -M main
git remote add origin https://github.com/Ez2Lazer/EzRealmSync.git
git push -u origin main
```

## 前置条件

同级存在已可编译的 `../osu`（至少能构建 `osu.Game`）。**不要**把 `osu` 源码提交进本仓库。

## 结构

| 项目 | 说明 |
|------|------|
| `osu.Game.EzRealmSync` | 契约、DTO、Mock / Phase2 数据层（不引用 osu.Game） |
| `osu.EzRealmSync` | 独立 WinExe（`EzRealmSync.dll`） |

## 构建与运行

```bash
cd EzRealmSync
dotnet build osu.EzRealmSync/osu.EzRealmSync.csproj
dotnet run --project osu.EzRealmSync -- --ui-test
```

- `--ui-test`：UI 测试模式，不读写真实 `client.realm`
- `--mock-delay=0`：去掉 Mock 扫描延迟

## 独立调试

打开 **EzRealmSync** 文件夹为工作区根目录，使用 `.vscode/launch.json` 中的 **EzRealmSync (UI Test)**。

## 代码风格

`Directory.Build.props` 导入 `../osu` 的分析器与约定；`.editorconfig` 可链接 osu 主仓：

```powershell
.\scripts\Link-OsuSharedFiles.ps1
```

## 与规则集的关系

| 方式 | 状态 |
|------|------|
| **独立 exe** | 已实现 |
| **挂入 osu 为规则集** | 未实现，见 [docs/FUTURE-RULESET-HOST.md](docs/FUTURE-RULESET-HOST.md) |

规格：[EzRealmSyncTool-中文.md](../Ez2Lazer.wiki/EzRealmSyncTool-中文.md)
