# lib/ — Ez 版 osu.Game（Phase 2 数据层）

本目录**不是**给 UI 用的。`osu.EzRealmSync` 只引用 **osu.Framework**；这里放的是 **Ez2Lazer 构建的 `osu.Game.dll`**，供 **`osu.Game.EzRealmSync`** 在 Phase 2 读写 Realm。

## 运行时布局（手动放置）

发布或本地运行 **EzRealmSync.exe** 时，把 DLL 放在 **exe 同目录下的 `lib/` 文件夹**（不是仓库根目录）：

```
EzRealmSync.exe
lib/
  osu.Game.dll
  osu.Framework.dll
  Realm.dll
  …（其它 publish 依赖，见下方）
  runtimes/
    win-x64/native/…   （如有 native 依赖）
```

程序启动时会自动从 `{exe}/lib/` 加载上述程序集（也兼容旧版平铺在 exe 根目录）。

## 用途

| 从 `osu.Game` 获取 | 示例 |
|--------------------|------|
| Ez 库访问 | `RealmAccess`（`client.realm`、Ez 字段、`EZ_REALM_SCHEMA_VERSION`） |
| 官方库访问 | `OfficialRealmAccess`（仅 schema 51，禁止用 Ez `RealmAccess` 写官方库） |
| 模型与工具 | `BeatmapInfo` / `ScoreInfo` 等 Realm 模型、迁移、路径解析、去 Ez 字段的映射辅助 |

UI（按钮、列表、对话框）**不要**引用本目录；界面继续用 Framework 基元（`BasicButton`、`Screen` 等）。

## 准备 lib

1. 在 EzRealmSync 仓库根目录执行（会 `dotnet publish ../osu/osu.Game` 并复制完整运行时依赖）：
   ```bash
   dotnet build -t:SyncEzRealmLibs EzRealmSync.sln -c Debug
   ```
2. 复制结果至少包含 `osu.Game.dll`、`osu.Framework.dll`（版本须与 Ez2Lazer 构建一致，如 `2026.528.1.0`）、`Realm.dll` 等；**不要**手动只拷 `osu.Game.dll`。
3. **不要**复制：
   - `osu.Game.Resources.dll`
   - `ez2lazer.Game.Resources.dll`
   - 其它 `*Resources*.dll`

存在 `lib/osu.Game.dll` 时，MSBuild 自动：

- 为 `osu.Game.EzRealmSync` 添加 `HAS_EZ_OSU_GAME` 编译常量
- 引用 `lib/*.dll`（已排除 Resources）
- 运行 exe 时复制到 **`bin/.../lib/`**（与 exe 同级的 lib 子目录）

无 `lib/osu.Game.dll` 时：仍可编译；非 `--ui-test` 模式使用 `StubRealmEzRealmSyncService`（提示未接 Realm）。

## 与 NuGet 的关系

- **不**使用 nuget.org 的 `ppy.osu.Game`（无 Ez Realm 扩展）。
- **不**要求克隆 `osu-resources` 仓库（数据层不加载 `osu.Game.Resources`）。
