# EzRealmSync 路线图

**osu! Realm 维护工具**：双 `client.realm`（A/B）Diff、写入、备份、浏览、导出。使用说明见仓库 [README.md](../README.md)。

## 当前状态（2026-09）

| 里程碑 | 状态 | 说明 |
|--------|------|------|
| **M1** Phase 1 UI | **完成** | 独立仓库 WPF Desktop（五 Tab），`--ui-test` Mock |
| **M2** Phase 2 数据 | **完成** | P2.1–P2.5a 已交付（P2.2/P2.3 的 typed 实现后来被动态路径取代）；P2.5b 手工验收待做 |
| **M3** Phase 3 | **完成** | 收藏夹谱面导出、合集 `collection.db` 导入/导出、`.osr`、按玩家分目录；修复页僵尸/缺文件 |
| **M4** Phase 4 动态化 | **完成** | 产品全面下线 `osu.Game.dll` / reader 包，读写改 DynamicRealm；同步支持皮肤；「转回官方版」单入口 |

### Phase 1 已交付（Desktop）

- `osu.EzRealmSync.Desktop`：WPF + WPF-UI，导入 / 数据 / 同步 / 修复 / 导出
- `osu.EzRealmSync.AppModel`：`RealmAppPresenter`、设置持久化（优先 exe 旁 `settings.json`，其次 `%AppData%\EzRealmSync\settings.json`）
- 数据页：Realm Studio 式左栏 8 类 + 右栏动态列（Mock）
- 全表右键：勾选 / 取消 / 反选 / 删除；删除前确认（可关）
- `MockEzRealmSyncService` + `--ui-test`

### Phase 2 进度

- [x] **P2.1** ~~`OfficialRealmAccess`（`osu.Game`）~~ —— 产品侧已不再加载 `osu.Game`，改为 DynamicRealm 动态打开
- [x] **P2.2** ~~`RealmDiffEngine` + `RealmDiffReader` + `ScanAsync`（需 `lib/osu.Game.dll`）~~ —— 已被 `DynamicBaselineReader` 的动态 Diff 取代，不再需要 `lib/osu.Game.dll`
- [x] **P2.3** ~~`RealmRowCopier` + `ApplyAsync`（Ez→官方；`RealmApplySupport` 单测）~~ —— typed 复制已被 DynamicRealm 官方基线同步取代（`DynamicBaselineReader` / `DynamicBaselineWriter`），相关类已删除
- [x] **P2.4** `RealmBackupCatalog` / 还原、`RealmRealmDataService` 真实加载与集合比对
- [x] **P2.4b** 任意 A→B 库对（含同类型/跨版本）Diff+写入；`RealmWritePlan`；导入页备份还原 UI
- [x] **P2.4c** 单目录扫描（Ez 根目录 `*.realm` + 共享 `files/`）；`RealmServiceSession` 共享注册表；真实修复/导出
- [x] **P2.5a** `RealmProcessGuard`；`RealmIllegalCharacterFixer` 写回；`RealmOrphanFileScanner` 僵尸文件
- [ ] **P2.5b** 手工验收：关游戏 → 导入 → 同步写入 → 修复/导出
- [x] **P3.1** 导出 Tab：收藏夹谱面按 `BeatmapMD5Hashes` 复制 files/；合集 `collection.db`；成绩 `.osr`
- [x] **P3.2** 数据 Tab：成绩右键导出 `.osr`
- [x] **P3.3** 数据 Tab：单难度导出；导出 Tab 右键导出；导出目录缓存失效
- [x] **P3.4** 导出 Tab：`scores.db`（成绩名单）；合并进现有 db 见 `TODO(legacy-db-merge)` / [LEGACY-DB-EXPORT.zh.md](LEGACY-DB-EXPORT.zh.md)

### Phase 4 进度（动态化）

- [x] **P4.1** 读写引擎动态化：`DynamicRealmSession` / `DynamicSchemaReader` / `RealmSchemaSnapshot` / `DynamicValueCodec` / `DynamicRowAccess`，按磁盘 schema 读写，不迁移、不改版本号
- [x] **P4.2** 消费方全部切到动态：数据页浏览 / 同步对比与写入 / 修复页扫描与删改 / 导出 / 修复页「转回官方版」
- [x] **P4.3** 下线 DLL 与 reader 布线：产品工程去掉 `ez2lazer.Game`，删除 `readers/`、`scripts/Sync-ReaderLibs.ps1`、`ReadSidecar`、Stub 后端与 typed 同步死代码；发布包不再带 `osu.Game.dll` / OfficialWrite
- [x] **P4.4** 同步能力：新增皮肤实体（`EntityKind.Skin`，官方 `Skin` + `File` / `RealmNamedFileUsage`）；成绩按 `BeatmapHash` 挂难度、跳过有原因；覆盖写入把 Ez 扩展列按原值回写（`EzColumnResolver`）
- [x] **P4.5** 官方兼容收窄：`OfficialExportPolicy` 纯数据判定（规则集 / 谱面集 / 难度 / 成绩）+ `OfficialModList.json`（`Generate-OfficialModList.ps1` 生成）；`DynamicOfficialConverter` 单入口「转回官方版」，原文件先备份
- [x] **P4.6** 下线「升级 Realm 文件」；`SchemaModelMismatch` 收窄为「缺对应版本的官方 schema 快照」
- [x] **P4.7** 验证：`DllVerifier` 与官方镜像 schema 对拍 + 官方 DLL 打开验收；typed / dynamic 逐单元格 parity 测试；`RealmAccessGatewayTest` 守卫挡住 `ez2lazer.Game` 回流；`smoke-publish.ps1` 断言发布目录不含 `osu.Game.dll`

## 仓库结构

```
EzRealmSync/
  osu.Game.EzRealmSync/            # 契约、Mock、动态读写引擎、官方兼容策略
  osu.Game.EzRealmSync.Contracts/  # Worker 与宿主共享的 DTO
  osu.Game.EzRealmSync.OfficialSchema/  # 官方 schema 镜像（只服务测试）
  osu.Game.EzRealmSync.OfficialWrite/   # 官方库读写 Worker（只服务测试，发布包不带）
  osu.Game.EzRealmSync.DllVerifier/     # 用真实官方包验收产物能否被对应客户端打开（只服务测试）
  osu.EzRealmSync.AppModel/        # Presenter、设置
  osu.EzRealmSync.Desktop/         # WPF Exe
  osu.Game.EzRealmSync.Tests/      # 测试；唯一允许加载 osu.Game.dll 的地方
```

产品侧读写不依赖 `osu.Game.dll`（见 [DATA-OPERATIONS.zh.md](DATA-OPERATIONS.zh.md)）；测试要 typed 对照时用 `-p:UseLocalOsuLibs=true` 从 `lib/` 取（见 [lib/README.md](../lib/README.md)）。

## 单元测试

| 项目 | 覆盖 |
|------|------|
| `osu.Game.EzRealmSync.Tests` | 动态读写 / 值编解码、typed-vs-dynamic parity、baseline 同步与 Ez 列保留、官方兼容过滤与 mod 名录、schema 快照与漂移守卫、`DllVerifier` 对拍与 DLL 打开验收、`RealmFileBackup` / `RealmBackupCatalog` / `RealmSetCompareHelper` / Mock 同步 / 设置持久化 |

```bash
dotnet test EzRealmSync.sln
```
