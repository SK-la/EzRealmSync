# lib/ — 本地开发用 osu.Game 闭包源

Build / Publish 后，**所有** `ez2lazer.Game` 传递依赖（含 `osu.Game.dll`、`Sentry.dll`、`Realm.dll` 等）位于 **exe 根目录**（标准 dotnet 平铺布局）。

仓库根 `lib/` **仅**用于并行开发 Ez2Lazer 主仓库、尚未发布对应 NuGet 时：

```bash
dotnet build -t:SyncEzRealmLibs EzRealmSync.sln -c Debug   # 从 ../osu 填充 lib/
dotnet build EzRealmSync.sln -p:UseLocalOsuLibs=true       # 复制到 bin/.../exe 根
```

`SyncEzRealmLibs` 会 `dotnet publish ../osu/osu.Game` 并复制完整运行时依赖到仓库 `lib/`，再复制到 Desktop 输出根目录。

## 与 NuGet 的关系

| 模式 | 开关 | 运行时布局 |
|------|------|------------|
| **NuGet（默认）** | `UseLocalOsuLibs=false` | exe 根平铺全部 DLL |
| **本地 lib** | `-p:UseLocalOsuLibs=true` | 同上（源来自仓库 `lib/`） |

- **不**使用 nuget.org 的 `ppy.osu.Game`（无 Ez Realm 扩展）。
- publish 后 `prune-publish.ps1` 会裁剪渲染/音频等 dead weight DLL。

## Sidecar / Reader

- **ReadSidecar**（`read-sidecar/`）自带托管闭包（从 ReadSidecar 构建输出全量复制，排除 Resources）；job 内 prepend reader 薄切片覆盖 `osu.Game.dll`。
- **Native**（`realm-wrappers.dll` 等）：优先 `read-sidecar/runtimes/`，亦可 fallback 到 exe 根 `runtimes/{当前 RID}/native/`；reader / `_shared` **不带** runtimes。

## 清理

`lib/*.dll` 与 `lib/runtimes/` 已在 `.gitignore` 中。删除仓库 `lib/` 内容不影响 NuGet 构建。
