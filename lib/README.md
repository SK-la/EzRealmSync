# lib/ — 本地开发覆盖（可选）

**默认不再依赖本目录。** 发布与 CI 通过 NuGet 包 `ez2lazer.Game` / `ez2lazer.Framework` 拉取 `osu.Game.dll` 及传递依赖，输出在 exe 同目录（非 `lib/` 子文件夹）。NuGet 发布与本地 lib 模式均**排除** `osu.Game.Resources`，并在 publish 后裁剪渲染/音频等死重 DLL。

仅在以下场景使用 `lib/`：

- 并行开发 Ez2Lazer 主仓库，尚未发布对应 NuGet 版本
- 需要验证与本地 `../osu` 构建产物完全一致的 DLL

## 启用本地 lib 模式

```bash
dotnet build -t:SyncEzRealmLibs EzRealmSync.sln -c Debug   # 从 ../osu 填充 lib/
dotnet build EzRealmSync.sln -p:UseLocalOsuLibs=true
```

`SyncEzRealmLibs` 会 `dotnet publish ../osu/osu.Game` 并复制**完整**运行时依赖到 `lib/`（约百个 DLL，含 Veldrid、NAudio 等 osu.Game 传递依赖）。这是 osu.Game 发布闭包，不是 EzRealmSync 额外需要的隐性依赖。

启用后，构建会把 `lib/*.dll` 复制到 `bin/.../lib/`（排除 `*Resources*.dll`），运行时由 `EzRealmSyncRuntimeLibLoader` 从该目录加载。

## 与 NuGet 的关系

| 模式 | 开关 | 编译 | 运行时布局 |
|------|------|------|------------|
| **NuGet（默认）** | `UseLocalOsuLibs=false` | `ez2lazer.Game` 包 | exe 目录 + `runtimes/win-x64/native/` |
| **本地 lib** | `-p:UseLocalOsuLibs=true` | `lib/*.dll` 直接引用 | `exe/lib/` |

- **不**使用 nuget.org 的 `ppy.osu.Game`（无 Ez Realm 扩展）。
- **不**要求克隆 `osu-resources`（数据层不加载 Resources）。

## 清理

`lib/*.dll` 与 `lib/runtimes/` 已在 `.gitignore` 中，可随时删除整个 `lib/` 内容（保留本 README）而不影响 NuGet 构建。

## 迁移与 schema

- 主进程 **单份** 最新 `osu.Game`（NuGet 或本地 `lib/`）。
- 旧 schema 通过 `readers/{id}/lib/` + **ReadSidecar** 子进程读取（manifest 驱动，代码不写死版本列表）。
- 日常打开：锁定磁盘已有 schema（`OpenWithoutMigration`）。
- 修复页「升级到最新版」：在备份工作副本上 migration，升到工具当前 schema。
- 跨 upstream 差集同步：源库可走 sidecar 读，目标库用主 lib 写（不改目标 schema）。
