# 数据操作原则（EzRealmSync）

## 两套模型（产品轴）

| 磁盘 | 模型 | 进程 |
|------|------|------|
| **官方**（schema &lt; 1000，如 51 / 52） | OfficialSchema 镜像（无 Ez 列） | `official-write/` Worker（读 + 转官方写 + 同步写入官方） |
| **Ez current**（如 52007） | Ez `osu.Game` | 主进程 |
| **Ez legacy**（如 51007） | Ez `osu.Game`（Sidecar 自包含 + readers 薄切片） | `read-sidecar/` |

主进程 **永不**加载 OfficialSchema（`[MapTo]` 冲突）。  
官方库 **禁止**再用 `OfficialRealmAccess` + Ez 对象模型假装官方（会 `MigrationNeeded` / 污染 Ez 列）。

## RealmAccessGateway（统一访问策略）

所有 Tab / Service **不得**自行选择 Provider / Sidecar / Official Worker；经 `RealmAccessGateway` 按**操作意图**分流：

| 入口 | 用途 | 打开方式 |
|------|------|----------|
| `ProbeSchema` | 读文件头 schema | 磁盘 API，不打开库 |
| `ReadDiffSnapshot` / `ReadBrowseSnapshot` | 同步对比、数据 Tab 浏览 | 官方 → Official Worker；Ez current → 进程内；Ez legacy → ReadSidecar |
| `ExportApplyBundleViaSidecar` | Apply 读源导出 DTO | 官方 → Official Worker；Ez legacy → Sidecar |
| `ApplyImportToOfficial` | 同步写入官方目标 | Official Worker `apply-import` |
| `OpenForWrite` / `OpenForMutation` | 删改、导入、Apply 写 **Ez** 目标 | 仅 Ez；官方直接拒绝 |
| `OpenForMigration` | 修复页升级 | **仅 Ez**；官方请用官方客户端升级或「转回官方版」 |

**错误语义：**

- 只读官方：Official Worker；缺 Worker 构建产物 → 明确错误（重新 build Desktop）。
- 只读 Ez legacy + 有 reader 包 → Sidecar；无包 → `ReaderPackageMissing`。
- **写回官方（数据 Tab）** → `SchemaModelMismatch`（请用同步 / 转官方）。
- **写回 Ez legacy** → `MigrationRequired`（请先修复页升级）。

**运行时布局：**

- Host 闭包：exe 根（主进程 + Ez current）。
- `official-write/`：OfficialSchema + Contracts + Realm native（官方读写）。
- `read-sidecar/`：Ez 托管闭包（STJ、osu.Game 等）；仅服务 **Ez legacy**。
- `readers/{id}/lib`：Ez legacy 薄切片；**不再**用于官方读。

## 三类能力

| 能力 | 作用 | 是否改磁盘 schema |
|------|------|-------------------|
| **读取（数据 Tab）** | 浏览各类对象 | **否** |
| **同步（同步 Tab）** | A/B 按 GUID 复制；写入官方经 Official Worker | **否** |
| **导出 / 删除（数据 Tab）** | Ez 库软删 / 导出文件 | **否**；官方库不支持数据 Tab 写回 |
| **修复「升级到 lib 最新」** | Ez 工作副本 migration | **是**（仅 Ez） |
| **修复「转回官方版」** | Official Worker 写官方库 | **是**（目标官方 schema） |

## 版本号

| 用途 | 来源 |
|------|------|
| 文件当前版本 | 磁盘文件头 |
| 官方 upstream | `Decode(文件头).official`（&lt;1000 即官方） |
| lib 最新 | bundled `osu.Game` |
| 最低支持 | 官方 ≥50，Ez 修订 ≥3 |

## 版本识别

1. **探测**：`RealmDiskSchemaReader` 动态只读文件头。
2. **打开**：官方 → OfficialSchema Worker；Ez → `RealmAccess`（pinned，无 migration）。
3. **禁止**：对用户库被动 `performSchemaMigration: true`；禁止主进程 Ez 模型打开官方库。
4. **例外**：修复页 Ez 升级；转官方写库（Official Worker）。
