# 数据操作原则（EzRealmSync）

## 三类能力

| 能力 | 作用 | 是否改磁盘 schema |
|------|------|-------------------|
| **读取（数据 Tab）** | 完整浏览 `client.realm` 中各类对象（谱面集、难度、成绩、收藏夹、文件等） | **否** — 动态只读探测版本 + `OpenWithoutMigration` + `performSchemaMigration: false` |
| **同步（同步 Tab）** | 在 A/B 库之间按 GUID 复制 **谱面集、难度、成绩、收藏夹**；跨官方/Ez 版本时剥离 Ez 独有字段 | **否** — 目标库保持原磁盘版本，仅增删改行数据 |
| **导出 / 删除（数据 Tab 右键）** | 对 **谱面集、成绩、收藏夹** 写回 Realm（软删）或复制 `files/` 实体（谱面、`.osr`）；合集名单另用 osu!stable **`collection.db`** 导入导出 | **否** |
| **修复 Tab「升级到最新版」** | 同大版本内：在备份工作副本上执行可控游戏 migration，校验后原子替换；禁止降级删库重建 | **是** — 升到工具当前官方 / Ez schema |
| **修复 Tab「转回官方版」** | 需要时先自动升级到当前 Ez schema，再剥 Ez 字段写入官方空库并原地覆盖 | **是** — 最终变为官方上游 schema |

游戏内仍用默认 `RealmAccess`（可迁移）；**除修复页显式升级 / 转官方内嵌升级外**，本工具禁止被动升/降 schema。

## 同大版本策略（本阶段）

- **只保证同大版本**：官方磁盘版本 = 当前 `UPSTREAM_SCHEMA_VERSION`；Ez = `upstream * 1000 + ez`（如 `52007`）。
- **单份 `osu.Game`**：不另存、不热加载其他 schema 的 DLL。数据同步双方须落在 `[MinSupported, MaxSupported]`（必要时先「升级到最新版」）。
- **低于最低支持**：拒绝打开，提示用对应客户端升到下限以上（不提供旧 reader）。
- **高于工具内置**：拒绝，提示更新 EzRealmSync。
- **跨大版本**（如 `51xxx` ↔ `52xxx`）与多版本 reader：**未实现**，另开专题。

## 跨版本同步为何安全（同大版本内）

- 各版本 Realm 都包含谱面集、难度、成绩、收藏夹等核心类型；Diff 按 **GUID** 对齐，不跑 migration。
- Ez 独有列（分析、扩展 SR 等）写入官方库前由 `OfficialRealmMapper` 剥离；进 Ez2Lazer 后由客户端 **重新补算**。
- 工具 **拒绝** 打开高于 / 低于同大版本支持区间的库。

## 版本识别（防「被动换版」）

1. **探测**：`RealmDiskSchemaReader` — Realm 动态 API 只读读文件头版本，不经 `RealmAccess` 构造。
2. **打开**：按磁盘版本选择 `OfficialRealmAccess` / `RealmAccess`，并传入 `pinnedDiskSchemaVersion`；`MigrationCallback = null`。
3. **禁止**：对用户库做被动 `performSchemaMigration: true`（会触发游戏启动维护、pending 清理、失败时删库重建）。
4. **例外**：修复页「升级到最新版」及「转回官方」内嵌升级 —— 仅在**已备份的工作副本**上 migration，且 `allowDestructiveRecoveryOnSchemaMismatch: false`。
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
