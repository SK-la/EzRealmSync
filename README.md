# EzRealmSync

**osu!lazer Realm 维护工具**（独立 exe，非规则集）：对比两份 `client.realm`（**A 源 / B 目标**），维护差异与失效数据、备份还原；支持同类型库（新旧版本）与 Ez↔官方等组合。

| 层 | 项目 | 职责 |
|----|------|------|
| **UI** | `osu.EzRealmSync.Desktop` | WPF + **WPF-UI**（Fluent 深色），`net8.0-windows` |
| **状态** | `osu.EzRealmSync.AppModel` | `RealmAppPresenter` + **osu.Framework.Bindables** |
| **数据** | `osu.Game.EzRealmSync` | `IEzRealmSyncService`、Mock；默认 **`ez2lazer.Game` NuGet** |

UI **不引用** `osu.Game` / `osu.Game.Resources`。中文/英文见 `Strings.resx`（设置中切换）。

旧版纯 Framework 自绘 UI 在 [`osu.EzRealmSync/`](osu.EzRealmSync/)（已从解决方案移除，仅作参考）。

## 构建与运行

需要 **.NET 8 SDK**。使用 **`EzRealmSync.sln`**。

```bash
cd EzRealmSync
dotnet build EzRealmSync.sln
dotnet run --project osu.EzRealmSync.Desktop
```

- **默认**：真实 Realm 后端（`ez2lazer.Game` NuGet，版本见 `EzRealmSync.Dependencies.props`）
- `--ui-test`：Mock 假数据（仅调 UI，不读 `.realm`）
- `--mock-delay=0`：Mock 模式去掉模拟延迟
- **本地 lib 覆盖**（并行开发 osu 主仓库时）：`dotnet build -t:SyncEzRealmLibs EzRealmSync.sln` 后加 `-p:UseLocalOsuLibs=true`（见 [lib/README.md](lib/README.md)）

主界面五 Tab：**导入**（osu! 数据目录 + Realm 列表 + 备份）→ **数据**（单库完整浏览；谱面集/成绩/收藏夹可写删与导出）→ **同步**（A/B 跨版本复制谱面集、难度、成绩、收藏夹，**不**改 schema）→ **修复** / **导出**（谱面与成绩共用导入目录下 `files/`；合集名单另用 osu!stable `collection.db`）。设置持久化至 `%AppData%\EzRealmSync\settings.json`。

数据安全与三类操作说明：[docs/DATA-OPERATIONS.zh.md](docs/DATA-OPERATIONS.zh.md)

路线图：[docs/ROADMAP.md](docs/ROADMAP.md)

单元测试：

```bash
dotnet test EzRealmSync.sln
dotnet test ../osu/osu.Game.Tests/osu.Game.Tests.csproj --filter "FullyQualifiedName~OfficialRealmAccess|FullyQualifiedName~RealmSchemaProfile|FullyQualifiedName~EzRealmAccessSchema"
```

## 中文显示

WPF 使用系统字体（`Microsoft YaHei UI` / `Segoe UI`），无需 `osu.Game.Resources.dll`。

## 独立调试

打开 **EzRealmSync** 为工作区根目录，使用 `.vscode/launch.json` 中的 **EzRealmSync (UI Test)**。

## 与规则集的关系

| 方式 | 状态 |
|------|------|
| **独立 exe** | 已实现（本仓库） |
| **挂入 osu 为规则集** | 未实现，见 [docs/FUTURE-RULESET-HOST.md](docs/FUTURE-RULESET-HOST.md) |

规格：[EzRealmSyncTool-中文.md](../Ez2Lazer.wiki/EzRealmSyncTool-中文.md)
