# EzRealmSync

**独立** Ez Realm 双库同步工具（非 osu! 规则集）。

| 层 | 项目 | 职责 |
|----|------|------|
| **UI** | `osu.EzRealmSync.Desktop` | WPF + **WPF-UI**（Fluent 深色），`net8.0-windows` |
| **状态** | `osu.EzRealmSync.AppModel` | `SyncPresenter` + **osu.Framework.Bindables** |
| **数据** | `osu.Game.EzRealmSync` | `IEzRealmSyncService`、Mock；可选 **`lib/osu.Game.dll`** |

UI **不引用** `osu.Game` / `osu.Game.Resources`。中文/英文见 `Strings.resx`（设置中切换）。

旧版纯 Framework 自绘 UI 在 [`osu.EzRealmSync/`](osu.EzRealmSync/)（已从解决方案移除，仅作参考）。

## 构建与运行

需要 **.NET 8 SDK**。使用 **`EzRealmSync.sln`**。

```bash
cd EzRealmSync
dotnet build EzRealmSync.sln
dotnet run --project osu.EzRealmSync.Desktop -- --ui-test
```

- `--ui-test`：Mock，不读写 `.realm`
- `--mock-delay=0`：去掉 Mock 扫描延迟
- 无 `lib/osu.Game.dll` 且非 UI 测试：扫描会提示放入 `lib/`（见 `StubRealmEzRealmSyncService`）

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
