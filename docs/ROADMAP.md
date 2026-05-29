# EzRealmSync 路线图

与 [EzRealmSyncTool-中文.md](../Ez2Lazer.wiki/EzRealmSyncTool-中文.md) 及 Cursor 计划 `ezrealmsynctool_规格` 对齐。

## 当前状态（2026-05-29）

| 里程碑 | 状态 | 说明 |
|--------|------|------|
| **M1** Phase 1 UI | **基本完成** | 独立仓库 WPF Desktop（五 Tab），`--ui-test` Mock |
| **M2** Phase 2 数据 | **进行中** | P2.1 `OfficialRealmAccess` 已入 `osu.Game` |
| **M3** Phase 3 | 未开始 | Collections / Zombie / `.osr` |

### Phase 1 已交付（Desktop）

- `osu.EzRealmSync.Desktop`：WPF + WPF-UI，导入 / 数据 / 同步 / 修复 / 导出
- `osu.EzRealmSync.AppModel`：`RealmAppPresenter`、设置持久化（`%AppData%\EzRealmSync\settings.json`）
- 数据页：Realm Studio 式左栏 8 类 + 右栏动态列（Mock）
- 全表右键：勾选 / 取消 / 反选 / 删除；删除前确认（可关）
- `MockEzRealmSyncService` + `--ui-test`

### Phase 2 下一步

1. **P2.2** `RealmDiffEngine`（GUID/Hash，三分类）
2. **P2.3** `RealmRowCopier` + `OfficialRealmMapper`（strip Ez 列）
3. **P2.4** `RealmEzRealmSyncService` / `IRealmDataService` 真实实现
4. **P2.5** 集成测试 + 手工 Ez→官方验收

## 仓库结构

```
EzRealmSync/
  osu.Game.EzRealmSync/      # 契约、Mock、Phase 2 引擎
  osu.EzRealmSync.AppModel/  # Presenter、设置
  osu.EzRealmSync.Desktop/   # WPF Exe
  osu.EzRealmSync/           # 旧 Framework UI（参考，不在 sln）
```

Phase 2 依赖 `lib/osu.Game.dll`（见 [lib/README.md](../lib/README.md)）。

## 单元测试

| 项目 | 覆盖 |
|------|------|
| `osu.Game.EzRealmSync.Tests` | Schema 编解码、`RealmFileBackup`、`RealmWorkspacePaths`、Mock 同步/备份、设置持久化 |
| `osu.Game.Tests` | `OfficialRealmAccess` / `EzRealmSchemaProfile` / Ez schema 版本 |

```bash
dotnet test EzRealmSync.sln
```
