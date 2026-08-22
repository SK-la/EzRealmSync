# lib/

将对应版本的 `osu.Game.dll` 及其传递依赖复制到此目录（含 `runtimes/.../realm-wrappers.dll`）。

开发时可执行：

```bash
dotnet build -t:SyncEzRealmLibs EzRealmSync.sln -c Debug
```

## 主进程 lib

exe 同目录（NuGet 默认）或 `lib/`（`-p:UseLocalOsuLibs=true`）为 **最新 Ez**，负责目标库读写、修复页升级。

## 旧版读取

旧 schema 的库由 `readers/{id}/lib/` + ReadSidecar 子进程读取，见 [readers/README.md](../readers/README.md)。

代码 **不写死** schema 列表；只要 manifest 声明的 `diskSchemaVersions` 与磁盘文件头匹配即可。
