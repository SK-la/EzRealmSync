# 数据操作原则（EzRealmSync）

## RealmAccessGateway（统一访问策略）

所有 Tab / Service **不得**自行选择 `RealmSchemaProbe`、`RealmDiffSnapshotProvider` 或 Sidecar；经 `RealmAccessGateway` 按**操作意图**分流：

| 入口 | 用途 | 打开方式 | Sidecar |
|------|------|----------|---------|
| `ProbeSchema` | 读文件头 schema | 磁盘 API，不打开库 | — |
| `ReadDiffSnapshot` | 同步 A/B 对比、Apply 读源库 | 主 lib 进程内 → 失败则 ReadSidecar + `readers/` | **是**（legacy 时） |
| `OpenForMutation` | 数据 Tab 浏览/删改、Apply 写目标 | 仅 bundled 主 lib | **否** — legacy 抛 `MigrationRequired` |
| `OpenForMigration` | 修复页升级 / 转官方 | 主 lib，允许在工作副本 migration | N/A |

**错误语义（框架层）：**

- 只读 Diff + 有 reader 包 → Sidecar 透明成功，UI 无「先升级」提示。
- 只读 Diff + 无 reader 包 → `ReaderPackageMissing`（指向 `readers/` + `Sync-ReaderLibs.ps1`）。
- 写回 + legacy schema → `MigrationRequired` / `LegacyReaderUnavailable`，文案明确「写操作需 lib 最新或先升级」，**不**与 Sidecar 读路径混用。

同步 Tab 的 Compare + Apply 均经 `IEzRealmSyncService`，内部统一走 Gateway。

## 三类能力

| 能力 | 作用 | 是否改磁盘 schema |
|------|------|-------------------|
| **读取（数据 Tab）** | 完整浏览 `client.realm` 中各类对象（谱面集、难度、成绩、收藏夹、文件等） | **否** — 动态只读探测版本 + `OpenWithoutMigration` + `performSchemaMigration: false` |
| **同步（同步 Tab）** | 在 A/B 库之间按 GUID 复制 **谱面集、难度、成绩、收藏夹**；跨官方/Ez 版本时剥离 Ez 独有字段 | **否** — 目标库保持原磁盘版本，仅增删改行数据 |
| **导出 / 删除（数据 Tab 右键）** | 对 **谱面集、成绩、收藏夹** 写回 Realm（软删）或复制 `files/` 实体（谱面、`.osr`）；合集名单另用 osu!stable **`collection.db`** 导入导出 | **否** |
| **修复 Tab「升级到 lib 最新」** | 在备份工作副本上 migration 到 bundled lib 的官方 / Ez schema，校验后原子替换 | **是** — 升到 lib 号（`UPSTREAM_SCHEMA_VERSION` / `EzFileSchemaVersion`） |
| **修复 Tab「转回官方版」** | 剥 Ez 字段写入官方空库并原地覆盖；**保持读取号**或**升到 lib 官方号**二选一 | **是** — 目标为解码 upstream 或 lib 官方 upstream |

游戏内仍用默认 `RealmAccess`（可迁移）；**除修复页显式升级 / 转官方外**，本工具禁止被动升/降 schema。

## 版本号从哪来

| 用途 | 来源 |
|------|------|
| 文件当前版本 | 读磁盘文件头 |
| 读取号（官方 upstream） | `Decode(文件头).official` |
| lib 官方 / Ez 最新 | bundled `osu.Game.dll` |
| 最低支持 | 工具常量（官方 ≥50，Ez 修订 ≥3） |
| 同步风险提示 | 工具内置修订分类表 |

## 支持区间

- **上限**：lib 内置 schema（换工具 / NuGet 即变）。
- **下限**：官方 upstream ≥50，Ez 修订 ≥3（如 `51006` 可打开；`51002` 拒绝）。
- **同步**：upstream 不一致时确认框软警告，不阻断；不跑 migration。
- **跨 upstream**（如 51 ↔ 52 数据复制）：同步 Tab **不改 schema**；跨 upstream 请用修复页升级或转官方。

## 跨版本同步为何安全（同大版本内）

- 各版本 Realm 都包含谱面集、难度、成绩、收藏夹等核心类型；Diff 按 **GUID** 对齐，不跑 migration。
- Ez 独有列（分析、扩展 SR 等）写入官方库前由 `OfficialRealmMapper` 剥离；进 Ez2Lazer 后由客户端 **重新补算**。
- 工具 **拒绝** 打开高于 / 低于同大版本支持区间的库。

## 版本识别（防「被动换版」）

1. **探测**：`RealmDiskSchemaReader` — Realm 动态 API 只读读文件头版本，不经 `RealmAccess` 构造。
2. **打开**：按磁盘版本选择 `OfficialRealmAccess` / `RealmAccess`，并传入 `pinnedDiskSchemaVersion`；`MigrationCallback = null`。
3. **禁止**：对用户库做被动 `performSchemaMigration: true`（会触发游戏启动维护、pending 清理、失败时删库重建）。
4. **例外**：修复页「升级到 lib 最新」及「转回官方版」—— 仅在**已备份的工作副本**上 migration（升级）或显式写目标 schema（转官方），且 `allowDestructiveRecoveryOnSchemaMismatch: false`。
5. **禁止**：用错误访问器打开库触发「schema 降级 → 备份并删库」。

若库已被错误迁移，请从 `client_newer_version.realm` 或导入页备份恢复。

## 数据 Tab 右键范围

| 类型 | 删除 | 导出 |
|------|------|------|
| 谱面集 | `DeletePending = true`（与游戏一致） | 复制所属难度在 `files/` 中的实体 |
| 难度 | 无单独删除（随谱面集） | 复制该难度在 `files/` 中的实体 |
| 成绩 | `DeletePending = true` | 右键单个/批量导出 `.osr`；可选 `replays/玩家名/` 子目录 |
| 收藏夹 | 从 Realm 移除记录 | **谱面**：按 MD5 复制 `files/` 实体；**合集**：`collection.db` 导入/导出（名称 + MD5） |
| 其它类 | 仅浏览，无写操作 | — |

操作前请 **关闭** osu!/Ez2Lazer；写库前可选时间戳备份（同步 Tab）。

## 收藏夹谱面 vs 合集 `collection.db`

这是两条独立能力：

- **导谱面**（导出 Tab「收藏夹谱面」、数据 Tab「导出文件…」）：按收藏夹把 `files/` 里的谱面复制出来，需要共享 `files/`。
- **导合集**（导出 Tab「合集 (collection.db)」、数据 Tab「导出/导入 collection.db…」）：osu!stable / Collection Manager 同款二进制（`collection.db`，导入也接受 `collections.db`），只含名称与谱面 MD5，**不**复制谱面文件。

合集导入按 **名称** 对齐；已存在则把缺失的 MD5 并入，不存在则新建。谱面未入库时 hash 仍会记下。导入会写 Realm，请先关游戏；工具会按现有策略做时间戳备份。
