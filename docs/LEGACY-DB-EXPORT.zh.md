# Legacy DB 导出 / 合并（collection.db · scores.db）

## 已支持

| 能力 | 说明 |
|------|------|
| 导出 **合集 (collection.db)** | 导出 Tab / 数据页右键；写出 osu!stable `collection.db` |
| 导入 **collection.db → Realm** | 按收藏夹**名称**合并 MD5（同名合并 hash） |
| 导出 **成绩 (scores.db)** | 导出 Tab 下拉「成绩 (scores.db)」；按谱面 MD5 分组写出稳定格式；**不**复制 `.osr`；仅官方四模式 |

## 未支持（计划）

下列能力在代码中以 `TODO(legacy-db-merge)` 标记：

1. **合并进现有 `collection.db`**  
   选择目标磁盘上的 `collection.db`，把勾选收藏夹追加/按名称合并进去（不经过 Realm）。

2. **合并进现有 `scores.db`**  
   选择目标 `scores.db`，按谱面 MD5 追加成绩；可选按 `ReplayMd5` / `OnlineScoreId` 去重。

3. **从 `scores.db` 导入回 Realm**  
   与 collection.db → Realm 对称；需处理规则集、mod、谱面关联缺失等。

## 相关代码

- [`LegacyCollectionDb.cs`](../osu.Game.EzRealmSync/IO/LegacyCollectionDb.cs)
- [`LegacyScoresDb.cs`](../osu.Game.EzRealmSync/IO/LegacyScoresDb.cs)
- [`RealmCollectionDbSync.cs`](../osu.Game.EzRealmSync/Realm/RealmCollectionDbSync.cs)
- [`RealmScoresDbSync.cs`](../osu.Game.EzRealmSync/Realm/RealmScoresDbSync.cs)

格式参考：[ppy wiki · Legacy database file structure](https://github.com/ppy/osu/wiki/Legacy-database-file-structure)
