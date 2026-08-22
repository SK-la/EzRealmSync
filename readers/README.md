# EzRealmSync Reader 包

当内置 NuGet（当前 Ez2Lazer 版本）无法用 pinned schema **进程内**打开旧版 Realm 时，**所有只读**路径（同步 Tab A/B 对比、Apply 读源库、数据 Tab 浏览等）经 Gateway 选用 reader 包，通过 **ReadSidecar 子进程**只读打开。

**写回**仍只走 bundled 主 exe 根闭包；legacy schema 需先在修复页升级。

## 目录布局

```
readers/
  sync-libs.config.json
  _shared/
    official/lib/           # official legacy 共享托管 DLL（无 osu.Game、无 runtimes）
  51/
    manifest.json
    lib/osu.Game.dll        # 薄切片：仅该 schema 的 ppy.osu.Game
  51007/
    manifest.json
    lib/osu.Game.dll        # 薄切片：仅该 schema 的 ez2lazer.Game
```

- **Ez legacy**：共享层 = 主进程 exe 根（probe 链 fallback）。
- **Official legacy**：共享层 = `readers/_shared/official/lib/`（托管传递依赖一份；**无 runtimes**，native 走 exe 根 / Sidecar `runtimes/`）。
- **ReadSidecar**（`read-sidecar/`）自带托管闭包；job 内 prepend reader 薄切片（仅覆盖 `osu.Game.dll`）。

Release 包预置 manifest；`lib/` 与 `_shared/` 由 `Sync-ReaderLibs.ps1` 生成（不进 Git）。

## 开发本地测试

```powershell
pwsh scripts/Sync-ReaderLibs.ps1          # 填充 _shared/official + readers/*/lib/ 薄切片
dotnet build EzRealmSync.sln -c Debug
dotnet run --project osu.EzRealmSync.Desktop
```

只同步某一 schema：`pwsh scripts/Sync-ReaderLibs.ps1 -ReaderDir 51007`

## manifest.json

| 字段 | 说明 |
|------|------|
| `id` | 唯一标识 |
| `profile` | `official` 或 `ez` |
| `diskSchemaVersions` | 磁盘文件头整数 |
| `libPath` | 相对包目录，默认 `lib` |

`HasValidLib` 仅检查 `readers/{id}/lib/osu.Game.dll` 存在。

## 冲突

同一 `diskSchemaVersion` 被多个 manifest 声明时，取 `id` 字典序最小；Scan 日志会提示。
