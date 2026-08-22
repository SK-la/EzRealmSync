# EzRealmSync Reader 包

当内置 NuGet（当前 Ez2Lazer 版本）无法用 pinned schema 打开旧版 Realm 时，工具会按磁盘 schema **自动**选用匹配的 reader 包，通过 **ReadSidecar 子进程**只读打开（同步 Scan / Apply 源库）。

## 目录布局

默认扫描目录（exe 同目录）：

`readers\`

文件夹名 **随意**；匹配逻辑只看 `manifest.json` 中的 `diskSchemaVersions`：

```
readers/
  51/
    manifest.json          # diskSchemaVersions: [51]
    lib/
      osu.Game.dll         # 用户复制或替换
      osu.Framework.dll
      Realm.dll
      runtimes/.../realm-wrappers.dll
  51007/
    manifest.json          # diskSchemaVersions: [51007]
    lib/
      ...
  52007/
    manifest.json
    lib/
```

仓库内预置了 `51/`、`52/`、`51007/`、`52007/` 的 **manifest 示例**；`lib/` 需自行填充（见下方）。

## manifest.json

```json
{
  "id": "ez-51007",
  "displayName": "Ez2Lazer 51007",
  "profile": "ez",
  "diskSchemaVersions": [51007],
  "libPath": "lib"
}
```

| 字段 | 说明 |
|------|------|
| `id` | 唯一标识（日志 / 冲突提示） |
| `profile` | `official`（裸 upstream &lt; 1000）或 `ez` |
| `diskSchemaVersions` | **磁盘文件头整数**；可声明多个（同一 DLL 能 pinned 打开时） |
| `libPath` | 相对包目录，默认 `lib` |

**注意**：官方库文件头是 `51`、`52`；Ez 库是 `51007`（= upstream×1000 + Ez 修订），二者不能混用。

## 使用方式

1. 将 manifest + lib 放入 `readers/`（或在设置中指定自定义扫描目录）。
2. 正常打开同步 Tab Scan — **无需重启**；每次 Scan 会重新扫描 reader 包。
3. 主 lib 能 pinned 打开时 **不启** sidecar；否则按 schema 找 reader 包并 spawn ReadSidecar。

设置里「启动时 reader」仍为开发用途（全局替换主 lib），与 sidecar 自动路由独立。

## 获取 lib 文件

**推荐（NuGet 一键同步）：**

```powershell
pwsh scripts/Sync-ReaderLibs.ps1
# 或
dotnet build -t:SyncReaderLibs EzRealmSync.sln
# 只同步一个 reader：
pwsh scripts/Sync-ReaderLibs.ps1 -ReaderDir 51007
```

映射见 [`sync-libs.config.json`](sync-libs.config.json)：`readerDir` ↔ NuGet 包版本（`ez2lazer.Game` / `ppy.osu.Game`）。脚本通过临时 `dotnet publish` 还原**完整传递依赖**到 `lib/`，无需下载客户端 zip。

config 示例：

```json
{
  "source": {
    "type": "nuget",
    "gameId": "ez2lazer.Game",
    "gameVersion": "2026.704.0-ez2lazer"
  }
}
```

可选：`frameworkId` / `frameworkVersion`（默认由 game 包传递依赖解析）、`realmVersion`（默认 20.1.0）。还支持 `local-dir`、`dotnet-publish`（本地 ../osu 构建）。

**手动：**

- 从对应版本 Ez2Lazer / osu!lazer 安装目录复制 `osu.Game.dll` 及依赖到 `lib/`。
- 开发：`dotnet build -t:SyncEzRealmLibs EzRealmSync.sln -c Debug`，再将 `lib/` 复制到 `readers/{id}/lib/`。

## 冲突

两个 manifest 声明同一 `diskSchemaVersion` 时，取 `id` 字典序最小的包；Scan 日志会提示重复声明。
