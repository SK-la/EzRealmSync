# 数据操作原则（EzRealmSync）

## 三类能力

| 能力 | 作用 | 是否改磁盘 schema |
|------|------|-------------------|
| **读取（数据 Tab）** | 完整浏览 `client.realm` 中各类对象（谱面集、难度、成绩、收藏夹、文件等） | **否** — 动态只读探测版本 + `OpenWithoutMigration` + `performSchemaMigration: false` |
| **同步（同步 Tab）** | 在 A/B 库之间按 GUID 复制 **谱面集、难度、成绩、收藏夹**；跨官方/Ez 版本时剥离 Ez 独有字段 | **否** — 目标库保持原磁盘版本，仅增删改行数据 |
| **导出 / 删除（数据 Tab 右键）** | 对 **谱面集、成绩、收藏夹** 写回 Realm（软删）或复制 `files/` 实体（谱面集、收藏夹） | **否** |

游戏内仍用默认 `RealmAccess`（可迁移）；**仅本工具**禁止被动升/降 schema。

## 跨版本同步为何安全

- 各版本 Realm 都包含谱面集、难度、成绩、收藏夹等核心类型；Diff 按 **GUID** 对齐，不跑 migration。
- Ez 独有列（分析、扩展 SR 等）写入官方库前由 `OfficialRealmMapper` 剥离；进 Ez2Lazer 后由客户端 **重新补算**。
- 工具 **拒绝** 打开高于 `lib/osu.Game.dll` 所支持版本的库；**允许** 打开更旧版本（如 `51003`），只要磁盘版本识别正确且 pinned 打开。

## 版本识别（防「被动换版」）

1. **探测**：`RealmDiskSchemaReader` — Realm 动态 API 只读读文件头版本，不经 `RealmAccess` 构造。
2. **打开**：按磁盘版本选择 `OfficialRealmAccess` / `RealmAccess`，并传入 `pinnedDiskSchemaVersion`；`MigrationCallback = null`。
3. **禁止**：用错误访问器打开库触发「schema 降级 → 备份并删库」；用 Ez 访问器探测官方 `51` 库将其迁到 `51006`。

若库已被错误迁移，请从 `client_newer_version.realm` 或导入页备份恢复。

## 数据 Tab 右键范围

| 类型 | 删除 | 导出 |
|------|------|------|
| 谱面集 | `DeletePending = true`（与游戏一致） | 复制所属难度在 `files/` 中的实体 |
| 成绩 | `DeletePending = true` | 暂未实现文件导出（.osr 见路线图） |
| 收藏夹 | 从 Realm 移除记录 | 按收藏夹内 MD5 复制对应谱面文件 |
| 其它类 | 仅浏览，无写操作 | — |

操作前请 **关闭** osu!/Ez2Lazer；写库前可选时间戳备份（同步 Tab）。
