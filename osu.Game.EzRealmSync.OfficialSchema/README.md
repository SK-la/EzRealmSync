# OfficialSchema 镜像

与 ppy 官方 `client.realm` 对齐的 Realm 模型，**不含 Ez 扩展列**。

| 对齐 ppy upstream | 目录 |
|-------------------|------|
| 51 | `V51/`（含 `Score.Pauses`） |
| 52 | `V52/`（在 51 基础上增加 `RealmOnlineAsset`） |

合并 ppy 上游时：diff 官方 `BeatmapInfo` / `ScoreInfo` 等，同步镜像类；Ez 独有字段**不得**加入镜像。

## 不得持久化（官方 `[Ignored]`）

- `Score.Passed`：运行时字段，**不得**写入镜像或转官方产物。

## 转官方过滤（Exporter 阶段）

| 类型 | 策略 |
|------|------|
| 皮肤 | 排除 Ez2 / EzStylePro / SbI 代码皮肤与全部 ScriptedSkin |
| 成绩 | 排除 Ez 规则集、含 UnknownMod / Ez-only mod、关联谱面未导出的成绩 |
| 谱面集 | 排除外部托管（`HostingKind=External`）与 Ez 规则集谱面 |
| 规则集 | 排除 `diva`、`bms` 等非官方程序集规则集 |
| 收藏夹 | prune 指向已过滤谱面的 MD5 |

对应 Ez fork 移除项（镜像列）：`XxyStarRating`、`PerformancePoints`、`HasVideo`、`HasStoryboard`、`HostingKind`、`ExternalContentRoot`、`LastAppliedXxySrVersion`、`ManiaHitMode`、`ManiaHealthMode`。
