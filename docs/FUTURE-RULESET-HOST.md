# 未来：规则集内嵌宿主（未实现）

当前仅实现 `StandaloneEzRealmSyncHost`（独立 `EzRealmSync.exe`）。

若将来要在 osu 客户端内打开同步 UI，建议：

1. 新建项目 `osu.EzRealmSync.Ruleset`（或 `EzRealmSync.Ruleset.Host`），实现 `Ruleset` 子类，**不**放入本仓库默认构建。
2. 实现 `IEzRealmSyncHost` 的 `RulesetEmbeddedEzRealmSyncHost`：在已有 `OsuGame` 的 `ScreenStack` 上 `Push(EzRealmSyncScreen)`，共享 `IEzRealmSyncService` DI。
3. `osu.Desktop` 通过可选引用或插件清单加载该规则集；**默认发行版仍只提供独立 exe**。

核心逻辑应始终在 `osu.Game.EzRealmSync`，UI 在 `osu.EzRealmSync`，避免与 `osu.Game.Rulesets.*` Gameplay 代码耦合。
