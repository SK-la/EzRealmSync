# official legacy 共享 **managed** 传递依赖

由 `scripts/Sync-ReaderLibs.ps1` 从 `sync-libs.config.json` 的 `sharedBaselines.official` 生成。

含 `Realm.dll`、`Sentry.dll` 等托管 DLL；**不含** `osu.Game.dll`，**也不含** `runtimes/`。

Native（`realm-wrappers.dll` 等）由主程序 `{exe}/lib/runtimes/{当前平台}/native/` 提供；Sidecar probe `../lib`，不在 reader 包里重复带全平台 native。

Run: `pwsh scripts/Sync-ReaderLibs.ps1`
