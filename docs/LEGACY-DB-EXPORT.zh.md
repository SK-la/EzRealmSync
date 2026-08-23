# Legacy DB：collection.db · scores.db

给**使用者**看的能力说明。实现细节见文末代码链接。

## 你现在能做什么

| 操作 | 在哪里 | 结果 |
|------|--------|------|
| 导出合集名单 | 导出页 → **合集 (collection.db)**，或数据页收藏夹右键 | 写出稳定客户端可读的 `collection.db`（名称 + 谱面 MD5，**不含**谱面文件） |
| 导入合集名单 | 导出页「导入 collection.db」，或数据页收藏夹右键 | 按**名称**合并进当前 Realm；同名合并 MD5 |
| 导出成绩名单 | 导出页 → **成绩 (scores.db)** | 写出 `scores.db`（按谱面 MD5 分组；**不含** `.osr`；仅 osu/taiko/catch/mania） |

导出谱面文件或 `.osr` 回放请用导出页的「谱面集 / 收藏夹谱面 / 成绩」等类型（需要 `files/`）。

## 还不能做什么（计划中）

代码里用 `TODO(legacy-db-merge)` 标记：

1. 把勾选内容**追加进磁盘上已有的** `collection.db` / `scores.db`（不经过 Realm）  
2. 从 `scores.db` **导入回** Realm（对称于合集导入）

## 注意

- 导入 / 导出前请关闭游戏。  
- `collection.db` 也接受文件名 `collections.db`。  
- 无谱面 MD5 或非官方四模式的成绩，写入 `scores.db` 时会被跳过。

## 开发者

- [`LegacyCollectionDb.cs`](../osu.Game.EzRealmSync/IO/LegacyCollectionDb.cs)
- [`LegacyScoresDb.cs`](../osu.Game.EzRealmSync/IO/LegacyScoresDb.cs)
- [`RealmCollectionDbSync.cs`](../osu.Game.EzRealmSync/Realm/RealmCollectionDbSync.cs)
- [`RealmScoresDbSync.cs`](../osu.Game.EzRealmSync/Realm/RealmScoresDbSync.cs)

格式：[ppy wiki · Legacy database file structure](https://github.com/ppy/osu/wiki/Legacy-database-file-structure)
