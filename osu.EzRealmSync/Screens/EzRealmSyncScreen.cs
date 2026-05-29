// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.EzRealmSync.Components;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Models;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;
using osu.Game.Screens;
using osuTK;
using osuTK.Graphics;

namespace osu.EzRealmSync.Screens
{
    public partial class EzRealmSyncScreen : OsuScreen
    {
        public override string Title => "Ez Realm Sync";

        public override bool ShowFooter => false;

        [Resolved]
        private IEzRealmSyncService syncService { get; set; } = null!;

        [Resolved]
        private EzRealmSyncLaunchOptions launchOptions { get; set; } = null!;

        [Resolved]
        private EzRealmSyncGame game { get; set; } = null!;

        [Resolved]
        private IDialogOverlay dialogOverlay { get; set; } = null!;

        private readonly Bindable<string> ezPath = new Bindable<string>();
        private readonly Bindable<string> officialPath = new Bindable<string>();
        private readonly Bindable<SyncDirection> direction = new Bindable<SyncDirection>(SyncDirection.EzToOfficial);
        private readonly BindableBool includeBeatmapSets = new BindableBool(true);
        private readonly BindableBool includeBeatmaps = new BindableBool(true);
        private readonly BindableBool includeScores = new BindableBool(true);
        private readonly BindableBool uiTestMode = new BindableBool();

        private ScanResult? lastScanResult;
        private DiffCategory activeCategory = DiffCategory.SourceOnly;

        private Container contentArea = null!;
        private DiffListPanel? activeListPanel;
        private OsuTabControl<DiffCategory> categoryTabs = null!;
        private ProgressBar progressBar = null!;
        private OsuSpriteText statusText = null!;
        private OsuSpriteText selectionCountText = null!;
        private RoundedButton applyButton = null!;
        private RoundedButton deleteButton = null!;
        private RoundedButton scanButton = null!;
        private EzRealmSyncSettingsOverlay? settingsOverlay;

        private CancellationTokenSource? operationCts;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            uiTestMode.Value = launchOptions.UiTestMode;

            var defaults = launchOptions.CreateDefaultPaths();
            ezPath.Value = defaults.EzDataPath;
            officialPath.Value = defaults.OfficialDataPath;

            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colours.Gray0,
            };

            AddInternal(new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                },
                Content = new[]
                {
                    createHeaderRow(),
                    createPathRow(),
                    new Drawable[]
                    {
                        createMainBody(colours),
                    },
                    createActionRow(),
                    createStatusRow(),
                },
            });
        }

        private Drawable[] createHeaderRow()
        {
            return new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(20),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = launchOptions.UiTestMode ? "Ez Realm Sync [UI Test]" : "Ez Realm Sync",
                            Font = OsuFont.GetFont(size: 28, weight: FontWeight.Bold),
                        },
                        new RoundedButton
                        {
                            Text = "设置",
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Width = 100,
                            Action = toggleSettings,
                        },
                    },
                },
            };
        }

        private Drawable[] createPathRow()
        {
            return new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 20 },
                    Spacing = new Vector2(0, 8),
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new PathInputRow("Ez 数据目录", ezPath)
                        {
                            BrowseRequested = () => browsePath(ezPath),
                        },
                        new PathInputRow("官方数据目录", officialPath)
                        {
                            BrowseRequested = () => browsePath(officialPath),
                        },
                        scanButton = new RoundedButton
                        {
                            Text = "扫描 Diff",
                            Width = 160,
                            Action = () => _ = runScanAsync(),
                        },
                    },
                },
            };
        }

        private Drawable createMainBody(OsuColour colours)
        {
            return new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Horizontal = 20, Bottom = 10 },
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 200),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        createLeftPanel(),
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Left = 15 },
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colours.Gray3,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding(10),
                                    Children = new Drawable[]
                                    {
                                        categoryTabs = new OsuTabControl<DiffCategory>
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 40,
                                            AccentColour = colours.PinkLight,
                                        },
                                        contentArea = new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding { Top = 50 },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private Drawable createLeftPanel()
        {
            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Y,
                Width = 200,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    new OsuSpriteText { Text = "同步方向", Font = OsuFont.GetFont(weight: FontWeight.SemiBold) },
                    new RoundedButton { Text = "Ez → 官方", Action = () => direction.Value = SyncDirection.EzToOfficial },
                    new RoundedButton { Text = "官方 → Ez", Action = () => direction.Value = SyncDirection.OfficialToEz },
                    new OsuSpriteText { Text = "实体类型", Font = OsuFont.GetFont(weight: FontWeight.SemiBold) },
                    new SettingsCheckbox { LabelText = "谱面集", Current = includeBeatmapSets },
                    new SettingsCheckbox { LabelText = "难度", Current = includeBeatmaps },
                    new SettingsCheckbox { LabelText = "成绩", Current = includeScores },
                    new SettingsCheckbox
                    {
                        LabelText = "收藏夹 (Phase 2)",
                        Alpha = 0.4f,
                    },
                    new OsuSpriteText
                    {
                        Text = uiTestMode.Value ? "UI 测试模式已开启" : string.Empty,
                        Font = OsuFont.GetFont(size: 12),
                        Colour = Color4.Orange,
                    },
                },
            };
        }

        private Drawable[] createActionRow()
        {
            return new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 20, Vertical = 8 },
                    Children = new Drawable[]
                    {
                        selectionCountText = new OsuSpriteText
                        {
                            Text = "已选 0 项",
                            Font = OsuFont.GetFont(size: 14),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        applyButton = new RoundedButton
                        {
                            Text = "添加到目标",
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Position = new Vector2(-220, 0),
                            Width = 160,
                            Action = () => confirmApply(deleteFromSource: false),
                        },
                        deleteButton = new RoundedButton
                        {
                            Text = "从源删除",
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Width = 140,
                            Action = () => confirmApply(deleteFromSource: true),
                        },
                        new RoundedButton
                        {
                            Text = "导出 .osr (Phase 2)",
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Position = new Vector2(-380, 0),
                            Width = 160,
                            Alpha = 0.4f,
                        },
                    },
                },
            };
        }

        private Drawable[] createStatusRow()
        {
            return new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 48,
                    Padding = new MarginPadding { Horizontal = 20, Bottom = 10 },
                    Children = new Drawable[]
                    {
                        progressBar = new ProgressBar(false)
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 8,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Width = 400,
                        },
                        statusText = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Position = new Vector2(420, 0),
                            Font = OsuFont.GetFont(size: 13),
                            Text = launchOptions.UiTestMode ? "UI 测试模式 — 未连接真实数据库" : "就绪",
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            categoryTabs.AddItem(DiffCategory.SourceOnly);
            categoryTabs.AddItem(DiffCategory.TargetOnly);
            categoryTabs.AddItem(DiffCategory.Conflicted);

            categoryTabs.Current.BindValueChanged(tab =>
            {
                activeCategory = tab.NewValue;
                refreshList();
                updateApplyButtonState();
            }, true);

            progressBar.EndTime = 1;
            progressBar.CurrentTime = 0;

            if (launchOptions.UiTestMode)
                _ = runScanAsync();
        }

        private void browsePath(Bindable<string> target)
        {
            if (launchOptions.UiTestMode)
            {
                target.Value = target == ezPath
                    ? @"C:\Fake\Ez2Lazer\data"
                    : @"C:\Fake\osu\data";
                return;
            }

            DirectoryInfo? initial = string.IsNullOrWhiteSpace(target.Value) ? null : new DirectoryInfo(target.Value);

            this.Push(new EzRealmSyncDirectoryScreen("选择数据目录", dir => target.Value = dir.FullName, initial));
        }

        private async Task runScanAsync()
        {
            operationCts?.Cancel();
            operationCts = new CancellationTokenSource();
            var token = operationCts.Token;

            setBusy(true);
            statusText.Text = "正在扫描…";

            try
            {
                if (!launchOptions.UiTestMode)
                {
                    var validation = await syncService.ValidatePathsAsync(createPaths(), token).ConfigureAwait(false);

                    if (!validation.IsValid)
                    {
                        Schedule(() => showError(string.Join("\n", validation.Errors)));
                        return;
                    }
                }

                var request = new ScanRequest
                {
                    Direction = direction.Value,
                    Paths = createPaths(),
                    EntityKinds = getSelectedEntityKinds(),
                };

                var progress = new Progress<ScanProgress>(p => Schedule(() =>
                {
                    progressBar.CurrentTime = p.Progress;
                    statusText.Text = p.Message;
                }));

                lastScanResult = await syncService.ScanAsync(request, progress, token).ConfigureAwait(false);

                Schedule(() =>
                {
                    refreshList();
                    statusText.Text = $"扫描完成 — 仅源有 {lastScanResult.SourceOnly.Count}，仅目标有 {lastScanResult.TargetOnly.Count}，不一致 {lastScanResult.Conflicted.Count}";
                    progressBar.CurrentTime = 1;
                });
            }
            catch (Exception ex)
            {
                Schedule(() => showError(ex.Message));
            }
            finally
            {
                Schedule(() => setBusy(false));
            }
        }

        private void confirmApply(bool deleteFromSource)
        {
            var selected = activeListPanel?.GetSelectedItems().ToList() ?? new List<DiffItem>();

            if (selected.Count == 0)
            {
                showError("请先勾选要处理的条目。");
                return;
            }

            if (!deleteFromSource && activeCategory == DiffCategory.Conflicted)
            {
                showError("「不一致」条目仅展示，不能添加到目标。");
                return;
            }

            PopupDialog dialog = deleteFromSource
                ? new EzRealmSyncDeleteDialog($"确定从源库删除 {selected.Count} 项？此操作不可撤销。", () => _ = runApplyAsync(selected, deleteFromSource: true))
                : new ConfirmDialog($"将 {selected.Count} 项添加到目标库（{direction.Value}）。", () => _ = runApplyAsync(selected, deleteFromSource: false));

            dialogOverlay.Push(dialog);
        }

        private async Task runApplyAsync(List<DiffItem> selected, bool deleteFromSource)
        {
            operationCts?.Cancel();
            operationCts = new CancellationTokenSource();
            var token = operationCts.Token;

            setBusy(true);

            try
            {
                var request = new ApplyRequest
                {
                    Direction = direction.Value,
                    Paths = createPaths(),
                    ItemIds = selected.Select(i => i.Id).ToList(),
                    CreateBackup = !deleteFromSource,
                    DeleteFromSource = deleteFromSource,
                };

                var progress = new Progress<ApplyProgress>(p => Schedule(() =>
                {
                    progressBar.CurrentTime = p.Progress;
                    statusText.Text = p.Message;
                }));

                var result = await syncService.ApplyAsync(request, progress, token).ConfigureAwait(false);

                Schedule(() =>
                {
                    statusText.Text = deleteFromSource
                        ? $"已删除 {result.AppliedCount} 项"
                        : $"已添加 {result.AppliedCount} 项" + (result.BackupPath != null ? $"，备份: {result.BackupPath}" : string.Empty);
                    progressBar.CurrentTime = 1;
                    _ = runScanAsync();
                });
            }
            catch (Exception ex)
            {
                Schedule(() => showError(ex.Message));
            }
            finally
            {
                Schedule(() => setBusy(false));
            }
        }

        private void refreshList()
        {
            if (lastScanResult == null)
            {
                contentArea.Clear();
                activeListPanel = null;
                updateSelectionCount();
                return;
            }

            var items = activeCategory switch
            {
                DiffCategory.SourceOnly => lastScanResult.SourceOnly,
                DiffCategory.TargetOnly => lastScanResult.TargetOnly,
                DiffCategory.Conflicted => lastScanResult.Conflicted,
                _ => Array.Empty<DiffItem>(),
            };

            contentArea.Child = activeListPanel = new DiffListPanel(items);
            updateSelectionCount();
            updateApplyButtonState();
        }

        private void updateApplyButtonState()
        {
            bool conflicted = activeCategory == DiffCategory.Conflicted;
            applyButton.Alpha = conflicted ? 0.4f : 1;
        }

        private void updateSelectionCount()
        {
            int count = activeListPanel?.GetSelectedItems().Count() ?? 0;
            selectionCountText.Text = $"已选 {count} 项";
        }

        private PathConfiguration createPaths() => new()
        {
            EzDataPath = ezPath.Value,
            OfficialDataPath = officialPath.Value,
        };

        private List<EntityKind> getSelectedEntityKinds()
        {
            var list = new List<EntityKind>();
            if (includeBeatmapSets.Value) list.Add(EntityKind.BeatmapSet);
            if (includeBeatmaps.Value) list.Add(EntityKind.Beatmap);
            if (includeScores.Value) list.Add(EntityKind.Score);
            return list;
        }

        private void setBusy(bool busy)
        {
            scanButton.Enabled.Value = !busy;
            applyButton.Enabled.Value = !busy;
            deleteButton.Enabled.Value = !busy;
        }

        private void showError(string message)
        {
            statusText.Text = message;
            dialogOverlay.Push(new ConfirmDialog(message, () => { }));
        }

        private void toggleSettings()
        {
            if (settingsOverlay != null)
            {
                RemoveInternal(settingsOverlay, true);
                settingsOverlay = null;
                return;
            }

            MockEzRealmSyncService? mock = syncService as MockEzRealmSyncService;

            AddInternal(settingsOverlay = new EzRealmSyncSettingsOverlay(uiTestMode, mock)
            {
                CloseRequested = toggleSettings,
            });

            settingsOverlay.Show();
        }
    }
}
