# EzRealmSync

面向 **osu!lazer / Ez2Lazer** 用户的 **Realm 维护工具**（独立 Windows 程序，不是游戏内规则集）。

用它你可以：

- 在同一数据目录下管理多份 `client.realm`
- **对比两份库（A / B）**并复制谱面集、难度、成绩、收藏夹（不改 schema）
- 浏览、删除、导出数据；备份与还原
- 升级过旧的 Ez Realm 文件，或把 Ez 库转成官方可用的 `client.realm`
- 导出 / 导入稳定客户端常用的 `collection.db`，导出 `scores.db`

> **使用前请先完全退出** osu! / Ez2Lazer。打开中的 Realm 无法安全写入。

---

## 快速开始（只想用）

### 1. 环境

- Windows 10/11
- 已安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（装过 Ez2Lazer 的一般已有）

### 2. 下载

从本仓库 **GitHub Releases** 下载 `EzRealmSync-*-win-x64.zip`，解压后运行 `EzRealmSync.exe`。

包内**不含** .NET 运行时；体积上会裁掉游戏渲染/音频等无关 DLL。

### 3. 第一次打开

1. 打开 **导入** 页  
2. 「搜索目录」选到你的 osu! / Ez2Lazer **数据根目录**（下面应有 `client.realm` 或若干 `.realm`，以及共享的 `files/`）  
3. 点刷新，确认列表里出现 Realm 文件  
4. 需要改库前，建议先用本页做一次 **备份**

设置、备份、导出目录都在 **exe 同目录**：`settings.json`、`backups/`、`exports/`、`readers/`。界面可在状态栏切换中文 / English。

---

## 五个页签怎么用

| 页签 | 做什么 | 备注 |
|------|--------|------|
| **导入** | 指定数据目录、登记 Realm、备份 / 还原 | 一切从这里开始 |
| **数据** | 浏览单库内容；可删谱面集 / 成绩 / 收藏夹；导出文件 | **仅 Ez 库可写删**；官方库请用同步或「转官方」相关流程 |
| **同步** | 选 A / B 两份库，计算差异后复制到操作目标 | **不升级** schema；跨版本可读，写入规则见下文 |
| **修复** | 升级过旧 Ez 库；转成官方 `client.realm`；修非法字符 / 缺文件等 | 升级会改版本，务必先备份并关游戏 |
| **导出** | 批量导出谱面 / 成绩 `.osr` / `collection.db` / `scores.db` | 谱面与回放依赖导入页目录下的 `files/`；名单类 db 不依赖 files |

更细的「能读 / 能写 / 会不会改版本」说明见：[docs/DATA-OPERATIONS.zh.md](docs/DATA-OPERATIONS.zh.md)  
`collection.db` / `scores.db` 能力与后续计划：[docs/LEGACY-DB-EXPORT.zh.md](docs/LEGACY-DB-EXPORT.zh.md)

### 常见操作举例

**把 A 库里缺的谱面集拷到 B**

1. 导入页选好数据目录并刷新  
2. 同步页选 A、B → 计算集合 → 在「仅 A」等标签里勾选 → 选择操作与写入目标 → 执行  

**Realm 提示版本过旧、无法修改**

1. 关游戏 → 备份  
2. 修复页选中该文件 → **升级 Realm 文件**  

**导出收藏夹名单给稳定客户端**

1. 导出页类型选 **合集 (collection.db)** → 加载列表 → 勾选 → 导出  
2. 或在数据页对收藏夹右键「导出合集」  

**导出成绩名单（不含回放文件）**

1. 导出页类型选 **成绩 (scores.db)** → 加载 → 勾选 → 导出（仅官方四模式）  

---

## 重要概念（少踩坑）

| 说法 | 含义 |
|------|------|
| **Realm 文件** | `client.realm` 等数据库文件 |
| **dll / `osu.Game.dll`** | 用来打开某版本 Realm 的程序集；缺版本时提示「缺少对应版本的 osu.Game.dll」 |
| **官方库** | 官方 lazer 写出的库（版本号较小，无 Ez 扩展列） |
| **Ez 库** | Ez2Lazer 扩展后的库（官方客户端通常打不开，属预期） |
| **files/** | 谱面、回放等实体文件目录；修复 / 导出谱面与 `.osr` 需要它 |

- 工具**不会**把 Ez 专用字段写进官方 `.osr` 或成绩提交 JSON。  
- 同步**不会**替你把库升到新版本；要升级请用修复页或官方客户端。  
- 若源库比工具内置版本**更旧**，可能需要准备 reader 包（见下节）。

---

## 旧版本 Realm 读不了？

主程序自带**当前** Ez / 官方能力。若要从更旧的 Ez schema 只读同步：

1. Release 包里已有 `readers/` 的 manifest 与 `scripts/Sync-ReaderLibs.ps1`  
2. 在 **exe 目录**执行（需要本机有 .NET 8 **SDK**）：

```powershell
pwsh scripts/Sync-ReaderLibs.ps1
```

3. 生成各 `readers/*/lib/osu.Game.dll` 后再开工具  

说明：[readers/README.md](readers/README.md)

---

## 给开发者

### 仓库结构（简）

| 项目 | 作用 |
|------|------|
| `osu.EzRealmSync.Desktop` | WPF + WPF-UI 界面 |
| `osu.EzRealmSync.AppModel` | 界面状态（Presenter、本地化） |
| `osu.Game.EzRealmSync` | Realm 读写、同步、修复、导出逻辑 |
| `osu.Game.EzRealmSync.OfficialSchema` / `OfficialWrite` | 官方库读写 Worker |
| `osu.Game.EzRealmSync.ReadSidecar` | Ez legacy 只读 Sidecar |

默认依赖 **`ez2lazer.Game` NuGet**（不是 nuget.org 的 `ppy.osu.Game`）。UI 层不直接引用 `osu.Game`。

### 构建与运行

需要 **.NET 8 SDK**，解决方案：`EzRealmSync.sln`。

```bash
cd EzRealmSync
dotnet build EzRealmSync.sln
dotnet run --project osu.EzRealmSync.Desktop
```

| 参数 / 开关 | 用途 |
|-------------|------|
| （默认） | 真实 Realm 后端 |
| `--ui-test` | Mock 数据，只调 UI |
| `--mock-delay=0` | Mock 去掉模拟延迟 |
| `-p:UseLocalOsuLibs=true` | 用仓库 `lib/` 覆盖 NuGet（需先 `dotnet build -t:SyncEzRealmLibs`） |

本地 lib、与主仓库并行开发：[lib/README.md](lib/README.md)

### 测试

```bash
dotnet test EzRealmSync.sln
```

VS Code：以本仓库为根打开，使用 `.vscode/launch.json` 里的 **EzRealmSync (UI Test)**。

### 其它文档

| 文档 | 内容 |
|------|------|
| [docs/DATA-OPERATIONS.zh.md](docs/DATA-OPERATIONS.zh.md) | 读写路由、官方 / Ez、会否改 schema |
| [docs/LEGACY-DB-EXPORT.zh.md](docs/LEGACY-DB-EXPORT.zh.md) | collection.db / scores.db |
| [docs/ROADMAP.md](docs/ROADMAP.md) | 里程碑与进度 |
| [docs/FUTURE-RULESET-HOST.md](docs/FUTURE-RULESET-HOST.md) | 将来挂入 osu 作规则集（未实现） |
| [osu.Game.EzRealmSync.OfficialSchema/README.md](osu.Game.EzRealmSync.OfficialSchema/README.md) | Official Worker |

---

## 许可证与归属

本工具为 Ez2Lazer 生态周边；Realm / 游戏模型基于 osu!(lazer) 与 Ez 扩展。使用时请自行备份数据，作者不对数据丢失负责。
