# lib/ — 测试工程的本地 osu.Game 覆盖源

产品进程**不加载 `osu.Game.dll`**（读写全走 DynamicRealm，见 [docs/DATA-OPERATIONS.zh.md](../docs/DATA-OPERATIONS.zh.md)），
所以 `lib/` 现在**只服务测试工程**：`osu.Game.EzRealmSync.Tests` 用 typed `osu.Game` 模型跑 parity 对照，
默认取 NuGet `ez2lazer.Game`，需要未发布的主仓库构建时才改用仓库 `lib/`。

```bash
dotnet build -t:SyncEzRealmLibs EzRealmSync.sln -c Debug   # 从 ../osu 填充 lib/
dotnet build EzRealmSync.sln -p:UseLocalOsuLibs=true       # 让测试工程引用 lib/
```

`SyncEzRealmLibs` 会 `dotnet publish ../osu/osu.Game` 并把完整运行时依赖复制到仓库 `lib/`；
`UseLocalOsuLibs=true` 时由 `Directory.Build.targets` 复制进测试输出目录。

## 与 NuGet 的关系

| 模式 | 开关 | 谁在用 |
|------|------|--------|
| **NuGet（默认）** | `UseLocalOsuLibs=false` | 测试工程引用 `ez2lazer.Game`（含 `osu.Game.dll`） |
| **本地 lib** | `-p:UseLocalOsuLibs=true` | 测试工程引用仓库 `lib/osu.Game.dll` |

- **不**使用 nuget.org 的 `ppy.osu.Game`（无 Ez Realm 扩展）。
- 产品发布目录里不允许出现 `osu.Game.dll`：`scripts/prune-publish.ps1` 会清掉它，`scripts/smoke-publish.ps1` 会反向断言。
- `readers/` 里按版本保存的官方 dll 是早期「按版本直连 osu.Game 打开产物」的夹具，当前验收口径已改成交给 `OfficialWrite` Worker（官方 schema 镜像）；这些文件暂时保留，但已无代码引用。

## 清理

`lib/*.dll` 与 `lib/runtimes/` 已在 `.gitignore` 中。删除仓库 `lib/` 内容不影响 NuGet 构建。
