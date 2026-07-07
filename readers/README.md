# EzRealmSync Reader 包

当内置 NuGet（当前 Ez2Lazer 版本）无法用 pinned schema 打开旧版 Realm 时，可安装匹配版本的 reader 包。

## 目录布局

默认扫描目录：

`%AppData%\EzRealmSync\readers\`

示例：

```
readers/
  ez-51003/
    manifest.json
    lib/
      osu.Game.dll
      osu.Framework.dll
      Realm.dll
      ...（该版本运行所需的传递依赖）
```

## manifest.json

```json
{
  "id": "ez-51003",
  "displayName": "Ez2Lazer 51003",
  "profile": "ez",
  "diskSchemaVersions": [51003],
  "libPath": "lib"
}
```

- `profile`: `official` 或 `ez`
- `diskSchemaVersions`: 该包可打开的磁盘 schema 列表
- `libPath`: 相对包目录的 lib 子目录，默认 `lib`

## 使用方式

1. 将上述目录放入 reader 包目录（或在设置中指定自定义目录）。
2. 打开 EzRealmSync **设置 → Reader 包**，选择对应 reader。
3. **重启应用**（reader 在启动时加载）。

同一会话内，旧版库会先尝试用当前内置 DLL + pinned schema 打开；只有失败时才提示安装 reader 包。

## 获取 lib 文件

可从对应版本的 Ez2Lazer / osu!lazer 安装目录，或 `dotnet build` 输出中复制 `osu.Game.dll` 及其依赖到 `lib/`。

也可在开发时执行：

`dotnet build EzRealmSync.sln -p:UseLocalOsuLibs=true`

然后将 `lib/` 复制到 reader 包目录。
