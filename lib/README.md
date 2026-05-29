# lib/ — Ez 版 osu.Game（Phase 2 数据层）

本目录**不是**给 UI 用的。`osu.EzRealmSync` 只引用 **osu.Framework**；这里放的是 **Ez2Lazer 构建的 `osu.Game.dll`**，供 **`osu.Game.EzRealmSync`** 在 Phase 2 读写 Realm。

## 用途

| 从 `osu.Game` 获取 | 示例 |
|--------------------|------|
| Ez 库访问 | `RealmAccess`（`client.realm`、Ez 字段、`EZ_REALM_SCHEMA_VERSION`） |
| 官方库访问 | `OfficialRealmAccess`（仅 schema 51，禁止用 Ez `RealmAccess` 写官方库） |
| 模型与工具 | `BeatmapInfo` / `ScoreInfo` 等 Realm 模型、迁移、路径解析、去 Ez 字段的映射辅助 |

UI（按钮、列表、对话框）**不要**引用本目录；界面继续用 Framework 基元（`BasicButton`、`Screen` 等）。

## 准备 lib

1. 在 Ez2Lazer `osu` 仓库构建：
   ```bash
   dotnet build osu.Game/osu.Game.csproj -c Debug
   ```
2. 将输出目录中**除资源程序集外**的 DLL 复制到本目录，至少包含：
   - `osu.Game.dll`
   - 以及运行时所缺的传递依赖（常见还有 `osu.Framework.dll`、`Realm.dll` 等，以构建输出为准）
3. **不要**复制：
   - `osu.Game.Resources.dll`
   - `ez2lazer.Game.Resources.dll`
   - 其它 `*Resources*.dll`

存在 `lib/osu.Game.dll` 时，MSBuild 自动：

- 为 `osu.Game.EzRealmSync` 添加 `HAS_EZ_OSU_GAME` 编译常量
- 引用 `lib/*.dll`（已排除 Resources）
- 运行 exe 时随输出目录复制这些 DLL

无 `lib/osu.Game.dll` 时：仍可编译；非 `--ui-test` 模式使用 `StubRealmEzRealmSyncService`（提示未接 Realm）。

## 与 NuGet 的关系

- **不**使用 nuget.org 的 `ppy.osu.Game`（无 Ez Realm 扩展）。
- **不**要求克隆 `osu-resources` 仓库（数据层不加载 `osu.Game.Resources`）。
