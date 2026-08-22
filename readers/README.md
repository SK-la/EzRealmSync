# EzRealmSync Reader 包

当内置 NuGet（当前 Ez2Lazer 版本）无法用 pinned schema **进程内**打开旧版 Realm 时，**所有只读 Diff 路径**（同步 Tab A/B 对比、Apply 读源库等）经 `RealmAccessGateway.ReadDiffSnapshot` 自动选用匹配的 reader 包，通过 **ReadSidecar 子进程**只读打开。

**写回**（删改、Apply 写目标、数据 Tab live 浏览）仍只走 bundled 主 lib；legacy schema 需先在修复页升级，**不会**误用 Sidecar。

## 目录布局

默认扫描目录（exe 同目录）：

`readers\`

```
readers/
  sync-libs.config.json   # NuGet 版本映射（可编辑）
  51/
    manifest.json
    lib/                  # 脚本生成或手动复制
  51007/
    manifest.json
    lib/
  ...
```

Release 包预置 `51/`、`52/`、`51007/`、`52007/` 的 **manifest**；`lib/` 默认空。

## 开发本地测试

```powershell
pwsh scripts/Sync-ReaderLibs.ps1          # 1. 填充仓库 readers/*/lib/（或 -ReaderDir 51007）
dotnet build EzRealmSync.sln -c Debug       # 2. lib 复制到 bin/.../readers/*/lib/
dotnet run --project osu.EzRealmSync.Desktop
```

改完脚本或 config 后需重新 build，才能把新 lib 同步到输出目录。

## 首次使用（Release 解压后）

主程序自带**最新** `ez2lazer.Game`，可处理当前 schema 的目标库。要从**更旧 schema** 源库同步，在 **exe 同目录**运行：

```powershell
pwsh scripts/Sync-ReaderLibs.ps1
```

或：`scripts\Sync-ReaderLibs.cmd`

只同步某一 schema：`pwsh scripts/Sync-ReaderLibs.ps1 -ReaderDir 51007`

需要 **.NET 8 SDK**（跑脚本还原 NuGet）；仅运行 `EzRealmSync.exe` 只需 Desktop Runtime。

版本映射见 [`sync-libs.config.json`](sync-libs.config.json)，可按需改 `gameVersion` 后重跑。

## manifest.json

| 字段 | 说明 |
|------|------|
| `id` | 唯一标识（日志 / 冲突提示） |
| `profile` | `official`（裸 upstream &lt; 1000）或 `ez` |
| `diskSchemaVersions` | **磁盘文件头整数** |
| `libPath` | 相对包目录，默认 `lib` |

官方库文件头是 `51`、`52`；Ez 库是 `51007`（= upstream×1000 + Ez 修订），二者不能混用。

## 使用方式

1. manifest 已随 Release 提供；`lib/` 由脚本生成或从安装目录手动复制。
2. 打开同步 Tab 做 A/B 对比或 Apply — **无需重启**；每次读 Diff 前 Gateway 会重新扫描 reader 包。
3. 主 lib 能 pinned 打开时 **不启** sidecar；否则按 schema 匹配 reader 并 spawn ReadSidecar。

## 冲突

两个 manifest 声明同一 `diskSchemaVersion` 时，取 `id` 字典序最小的包；Scan 日志会提示重复声明。
