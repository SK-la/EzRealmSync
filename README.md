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
- 已安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)（装过 Ez2Lazer 的一般已有）

### 2. 下载

从本仓库 **GitHub Releases** 下载 `EzRealmSync-*-win-x64.zip`，解压后运行 `EzRealmSync.exe`。

包内**不含** .NET 运行时；体积上会裁掉游戏渲染/音频等无关 DLL。

### 3. 第一次打开

1. 打开 **导入** 页  
2. 「搜索目录」选到你的 osu! / Ez2Lazer **数据根目录**（下面应有 `client.realm` 或若干 `.realm`，以及共享的 `files/`）  
3. 点刷新，确认列表里出现 Realm 文件  
4. 需要改库前，建议先用本页做一次 **备份**

设置、备份、导出目录都在 **exe 同目录**：`settings.json`、`backups/`、`exports/`。界面可在状态栏切换中文 / English。

---

## 五个页签怎么用

| 页签 | 做什么 | 备注 |
|------|--------|------|
| **导入** | 指定数据目录、登记 Realm、备份 / 还原 | 一切从这里开始 |
| **数据** | 浏览单库内容；可删谱面集 / 成绩 / 收藏夹；导出文件 | **仅 Ez 库可写删**；官方库请用同步或「转官方」相关流程 |
| **同步** | 选 A / B 两份库，计算差异后把谱面 / 收藏夹 / 皮肤 / 成绩复制到操作目标 | **不升级** schema；跨版本可读，写入规则见下文 |
| **修复** | 把 Ez 库按官方 schema 收窄成官方 `client.realm`；修非法字符 / 缺文件等 | 转官方会先备份原文件，收窄后 Ez 独有数据只留在备份里 |
| **导出** | 批量导出谱面 / 成绩 `.osr` / `collection.db` / `scores.db` | 谱面与回放依赖导入页目录下的 `files/`；名单类 db 不依赖 files |

更细的「能读 / 能写 / 会不会改版本」说明见：[docs/DATA-OPERATIONS.zh.md](docs/DATA-OPERATIONS.zh.md)  
`collection.db` / `scores.db` 能力与后续计划：[docs/LEGACY-DB-EXPORT.zh.md](docs/LEGACY-DB-EXPORT.zh.md)

### 常见操作举例

**把 A 库里缺的谱面集拷到 B**

1. 导入页选好数据目录并刷新  
2. 同步页选 A、B → 计算集合 → 在「仅 A」等标签里勾选 → 选择操作与写入目标 → 执行  

**Realm 提示版本过旧、无法修改**

1. 关游戏 → 备份
2. 用 Ez2Lazer 客户端打开该数据目录一次，让游戏自己把库迁到当前版本
3. 回到本工具刷新列表后重试

本工具**不会**改动 Realm 文件的版本号，也不会代替游戏做 schema 迁移。

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
| **dll / `osu.Game.dll`** | **产品不使用它打开用户库**。只有测试用它验证产物能否被对应版本客户端打开 |
| **官方库** | 官方 lazer 写出的库（版本号较小，无 Ez 扩展列） |
| **Ez 库** | Ez2Lazer 扩展后的库（官方客户端通常打不开，属预期） |
| **files/** | 谱面、回放等实体文件目录；修复 / 导出谱面与 `.osr` 需要它 |

- 工具**不会**把 Ez 专用字段写进官方 `.osr` 或成绩提交 JSON。  
- 同步只拷官方基线（谱面 / 收藏夹 / 皮肤 / 成绩 / `File` 与对应 `files/`），**不改**两边 schema，也不写 Ez 专用列。  
- 成绩按 `BeatmapHash` 挂到目标库的难度上：目标缺该难度（或目标没有该成绩的规则集）时会**跳过**并在状态栏说明，不会写出一条目标端看不见的成绩。先同步谱面再同步成绩即可。  
- 同步**不会**用 `osu.Game.dll` 打开用户库：读写全走 DynamicRealm，产品进程不加载它。要迁移版本请用 Ez2Lazer 客户端打开一次。

---

## 数据页打不开旧版 Ez？

不需要任何额外 DLL。数据页与同步页一样按磁盘 schema 动态读取，遇到读不出的版本请附上 `log/` 里的日志反馈。

---

## 给开发者

### 仓库结构（简）

| 项目 | 作用 |
|------|------|
| `osu.EzRealmSync.Desktop` | WPF + WPF-UI 界面 |
| `osu.EzRealmSync.AppModel` | 界面状态（Presenter、本地化） |
| `osu.Game.EzRealmSync` | Realm 读写、同步、修复、导出逻辑 |
| `osu.Game.EzRealmSync.OfficialSchema` / `OfficialWrite` | 官方库读写 Worker |
| `osu.Game.EzRealmSync.Tests` | 测试；也是唯一允许加载 `osu.Game.dll` 的地方（parity 对照、`readers/` 夹具） |

产品工程只依赖 **`ez2lazer.Framework`**（osu.Framework），**不依赖** `ez2lazer.Game`：读写全走 DynamicRealm，官方产物由 `OfficialWrite` Worker 用官方 schema 镜像写出。测试工程才引 `ez2lazer.Game`，用来做 typed 对照。发布前 CI 会跑 `scripts/smoke-publish.ps1`，它断言 `osu.Game.dll` **不得**出现在发布目录。

### 构建与运行

需要 **.NET 10 SDK**，解决方案：`EzRealmSync.sln`。

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
| `-p:UseLocalOsuLibs=true` | 测试工程改用仓库 `lib/` 的 `osu.Game.dll` 做 typed 对照（需先 `dotnet build -t:SyncEzRealmLibs`）；产品工程不受影响 |

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
