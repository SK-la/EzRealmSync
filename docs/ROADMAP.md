# EzRealmSync 路线图

**osu! Realm 维护工具**：双 `client.realm`（A/B）Diff、写入、备份、浏览。与 [EzRealmSyncTool-中文.md](../Ez2Lazer.wiki/EzRealmSyncTool-中文.md) 及 Cursor 计划 `ezrealmsynctool_规格` 对齐。

## 当前状态（2026-05-29）

| 里程碑 | 状态 | 说明 |
|--------|------|------|
| **M1** Phase 1 UI | **基本完成** | 独立仓库 WPF Desktop（五 Tab），`--ui-test` Mock |
| **M2** Phase 2 数据 | **进行中** | P2.1–P2.5a 已交付；P2.5b 手工验收待做 |
| **M3** Phase 3 | 部分完成 | 收藏夹同步/导出、`.osr` 导出已交付；僵尸文件扫描等仍待完善 |

### Phase 1 已交付（Desktop）

- `osu.EzRealmSync.Desktop`：WPF + WPF-UI，导入 / 数据 / 同步 / 修复 / 导出
- `osu.EzRealmSync.AppModel`：`RealmAppPresenter`、设置持久化（`%AppData%\EzRealmSync\settings.json`）
- 数据页：Realm Studio 式左栏 8 类 + 右栏动态列（Mock）
- 全表右键：勾选 / 取消 / 反选 / 删除；删除前确认（可关）
- `MockEzRealmSyncService` + `--ui-test`

### Phase 2 进度

- [x] **P2.1** `OfficialRealmAccess`（`osu.Game`）
- [x] **P2.2** `RealmDiffEngine` + `RealmDiffReader` + `ScanAsync`（需 `lib/osu.Game.dll`）
- [x] **P2.3** `RealmRowCopier` + `ApplyAsync`（Ez→官方；`RealmApplySupport` 单测）
- [x] **P2.4** `RealmBackupCatalog` / 还原、`RealmRealmDataService` 真实加载与集合比对
- [x] **P2.4b** 任意 A→B 库对（含同类型/跨版本）Diff+写入；`RealmWritePlan`；导入页备份还原 UI
- [x] **P2.4c** 单目录扫描（Ez 根目录 `*.realm` + 共享 `files/`）；`RealmServiceSession` 共享注册表；真实修复/导出
- [x] **P2.5a** `RealmProcessGuard`；`RealmIllegalCharacterFixer` 写回；`RealmOrphanFileScanner` 僵尸文件
- [ ] **P2.5b** 手工验收：关游戏 → 导入 → 同步写入 → 修复/导出（需 `lib/osu.Game.dll`）
- [x] **P3.1** 导出 Tab：收藏夹按 `BeatmapMD5Hashes` 构建目录；成绩 `.osr`（`ExportDataKind.Score`）
- [x] **P3.2** 数据 Tab：成绩右键导出 `.osr`

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
| `osu.Game.EzRealmSync.Tests` | Schema 编解码、`RealmFileBackup`、`RealmBackupCatalog`、`RealmSetCompareHelper`、`RealmFileRegistry`、Mock 同步/备份、设置持久化 |
| `osu.Game.Tests` | `OfficialRealmAccess` / `EzRealmSchemaProfile` / Ez schema 版本 |

```bash
dotnet test EzRealmSync.sln
```
