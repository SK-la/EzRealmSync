# 数据操作原则（EzRealmSync）

> 面向开发者与进阶用户：说明工具**怎么打开**不同版本的 Realm、**会不会改版本**。  
> 日常怎么点界面请看仓库根目录 [README.md](../README.md)。

## 一套模型（产品轴）

**所有**读写都由 DynamicRealm 完成，产品进程**永不加载 `osu.Game.dll`**：

| 场景 | 打开方式 |
|------|----------|
| 探测文件头 | `RealmDiskSchemaReader`（只读文件头，不开库） |
| 同步对比 / 导出包 | `DynamicBaselineReader`（官方基线列白名单） |
| 同步写入 | `DynamicBaselineWriter`（只写目标库**已有**的官方列） |
| 数据页浏览 | `DynamicBrowseSnapshotBuilder` |
| 修复页扫描 / 删改 | 动态扫描 + 动态软删（只动 Ez 目标） |
| 转回官方版 | `DynamicOfficialConverter`（按官方 schema 收窄成新文件） |

- 打开一律**钉死磁盘 schema**：不迁移、不改文件头版本号。
- 不因 schema 高于/低于 bundled 版本拒绝用户库；版本号只用于**显示**与**选官方 schema 目标**。
- `osu.Game.dll` 只存在于**测试工程**，用来验证产物能否被对应版本的客户端打开（`DllOpenCompatibilityTest`、parity 对照）。

**错误语义：**

- **同步读/写**：DynamicRealm 官方基线；缺列跳过，不因版本拒绝。
- **成绩同步的边界**：成绩按 `BeatmapHash` 链接目标难度。目标缺该难度、缺归属规则集（Ez 专用 `diva`/`bms` 且目标本来没有）、或没有 `Score` 表时**跳过**，在结果里计数并在状态栏列出原因，不写目标端看不见的悬空成绩；整次同步不中断。Ez → Ez 时目标已有该规则集行则正常写入。
- **Ez 列不被改动**：目标已有同 ID 行时按覆盖重建，但 Ez 扩展列取**删除前的原值**写回（谱面集的 `ExternalContentRoot`/`HostingKind`、难度的 `XxyStarRating`/`PerformancePoints`/`HasVideo`/`HasStoryboard`、成绩的 `ManiaHitMode`/`ManiaHealthMode`、规则集的 `LastAppliedXxySrVersion`）。覆盖同步不会顺手清掉 xxySR 或外部托管路径；单独同步难度走就地更新，天然保留。
- **官方库写回**（数据 Tab 删改）→ `SchemaModelMismatch`（请用同步 / 转官方）。
- **版本过旧**：本工具不升级 Realm 文件。请用 Ez2Lazer 客户端打开一次让游戏迁移，再回到本工具。

**运行时布局：**

- Host 闭包：exe 根（全部读写都在这里完成）。
- `official-write/`：仅测试与「转官方」的外部写入通道（OfficialSchema 镜像），不参与日常浏览。

## 三类能力

| 能力 | 作用 | 是否改磁盘 schema |
|------|------|-------------------|
| **读取（数据 Tab）** | 浏览各类对象 | **否** |
| **同步（同步 Tab）** | A/B 按 GUID 复制官方基线（谱面 / 收藏夹 / 皮肤 / 成绩 / File + files/）；两边 schema 原样保留 | **否** |
| **导出 / 删除（数据 Tab）** | Ez 库软删 / 导出文件 | **否**；官方库不支持数据 Tab 写回 |
| **修复「转回官方版」** | 按官方 schema 收窄成新文件，原文件先备份 | **是**（落成官方 schema 的新文件） |

## 版本号

| 用途 | 来源 |
|------|------|
| 文件当前版本 | 磁盘文件头 |
| 官方 upstream | `Decode(文件头).official`（&lt;1000 即官方） |

Ez 修订号仍按 `official * 1000 + ez` 编码，但工具只把它当**显示信息**，不再据此选 reader。

## 禁止

1. 对用户库被动 `performSchemaMigration: true`。
2. 在主进程中加载任何版本的 `osu.Game.dll`（测试工程除外）。
3. 用「错误版本的工具」当借口拒绝读写：动态路径本就不吃版本号。
