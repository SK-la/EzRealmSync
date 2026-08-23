# EzRealmSync Reader 包

当内置 NuGet 无法进程内打开 **Ez legacy** Realm 时，只读路径经 Gateway 选用 reader 包，通过 **ReadSidecar** 子进程打开。

**官方库（schema &lt; 1000）不走本目录**——一律 `official-write/` + OfficialSchema。

**写回** Ez 仍只走主进程；Ez legacy 需先在修复页升级。

## 目录布局

```
readers/
  sync-libs.config.json
  _shared/
    official/lib/           # 历史遗留；官方读已不再依赖
  51007/
    manifest.json
    lib/osu.Game.dll        # 薄切片：该 schema 的 ez2lazer.Game
```

- **Ez legacy**：共享层 = 主进程 exe 根；薄切片覆盖 `osu.Game.dll`。
- **ReadSidecar**（`read-sidecar/`）自带托管闭包；仅服务 Ez legacy。
- **官方**：见 `official-write/` 与 `OfficialSchema/README.md`。

Release 包预置 Ez legacy manifest；`lib/` 由 `Sync-ReaderLibs.ps1` 生成（不进 Git）。

日常使用说明见仓库根目录 [README.md](../README.md)（「旧版本 Realm 读不了？」一节）。

## 开发本地测试

```powershell
pwsh scripts/Sync-ReaderLibs.ps1          # 填充 Ez legacy 薄切片
dotnet build EzRealmSync.sln -c Debug
dotnet run --project osu.EzRealmSync.Desktop
```

只同步某一 Ez schema：`pwsh scripts/Sync-ReaderLibs.ps1 -ReaderDir 51007`

## manifest.json

| 字段 | 说明 |
|------|------|
| `id` | 唯一标识 |
| `profile` | 应为 `ez`（`official` 包可保留但不用于只读路由） |
| `diskSchemaVersions` | 磁盘文件头整数 |
| `libPath` | 相对包目录，默认 `lib` |

`HasValidLib` 仅检查 `readers/{id}/lib/osu.Game.dll` 存在。
