# OfficialSchema 镜像

与 ppy 官方 `client.realm` 对齐的 Realm 模型，**不含 Ez 扩展列**。  
仅由 `official-write/` Worker 加载；**主进程不得引用本程序集**（与 Ez `osu.Game` 的 `[MapTo]` 冲突）。

| 对齐 ppy upstream | 对象类型 |
|-------------------|----------|
| 51 | `V51/*`（含 `Score.Pauses`） |
| ≥52 | `V51/*` + `V52.RealmOnlineAsset` |

合并 ppy 上游时：diff 官方模型，同步镜像类；Ez 独有字段**不得**加入镜像。

## Worker 模式

| CLI | 用途 |
|-----|------|
| `<job.json>` | 转官方写库 |
| `--verify` | 校验无 Ez 列 |
| `browse` / `read` | 数据 Tab / Diff 只读 |
| `apply-export` | 同步从官方源导出 DTO |
| `apply-import` | 同步写入官方目标 |

## 不得持久化（官方 `[Ignored]`）

- `Score.Passed`：运行时字段，**不得**写入镜像或转官方产物。

## 转官方过滤（Exporter 阶段）

| 类型 | 策略 |
|------|------|
| 皮肤 | 排除 Ez2 / EzStylePro / SbI 代码皮肤与全部 ScriptedSkin |
| 成绩 | 排除 Ez 规则集、含 UnknownMod / Ez-only mod、关联谱面未导出的成绩 |
| 谱面集 | 排除外部托管与 Ez 规则集谱面 |
| 规则集 | 排除 `diva`、`bms` 等非官方程序集规则集 |
| 收藏夹 | prune 指向已过滤谱面的 MD5 |

对应 Ez fork 移除项：`XxyStarRating`、`PerformancePoints`、`HasVideo`、`HasStoryboard`、`HostingKind`、`ExternalContentRoot`、`LastAppliedXxySrVersion`、`ManiaHitMode`、`ManiaHealthMode`。
