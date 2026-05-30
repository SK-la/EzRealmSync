using osu.EzRealmSync.Components;
using osu.EzRealmSync.Platform;
using osu.EzRealmSync.UI;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzRealmSync.Abstractions;
using osu.Game.EzRealmSync.Mock;
using osu.Game.EzRealmSync.Models;
using Screen = osu.Framework.Screens.Screen;

namespace osu.EzRealmSync.Screens
{
    public partial class EzRealmSyncScreen : Screen
    {
        [Resolved]
        private IEzRealmSyncService syncService { get; set; } = null!;

        [Resolved]
        private EzRealmSyncLaunchOptions launchOptions { get; set; } = null!;

        [Resolved]
        private IEzRealmSyncDialogs dialogs { get; set; } = null!;

        private readonly Bindable<string> endpointAPath = new Bindable<string>();
        private readonly Bindable<string> endpointBPath = new Bindable<string>();
        private readonly BindableBool uiTestMode = new BindableBool();

        private ScanResult? lastScanResult;

        private SyncSidebarPanel sidebar = null!;
        private DiffResultPanel diffPanel = null!;
        private DiffListPanel? activeListPanel;

        private EzProgressBar progressBar = null!;
        private EzText statusText = null!;
        private EzText selectionCountText = null!;
        private EzButton applyButton = null!;
        private EzButton deleteButton = null!;
        private EzButton scanButton = null!;
        private EzRealmSyncSettingsOverlay? settingsOverlay;

        private CancellationTokenSource? operationCts;
        private bool isBusy;

        [BackgroundDependencyLoader]
        private void load()
        {
            uiTestMode.Value = launchOptions.UiTestMode;

            var defaults = launchOptions.CreateDefaultPaths();
            endpointAPath.Value = defaults.EzDataPath;
            endpointBPath.Value = defaults.OfficialDataPath;

            sidebar = new SyncSidebarPanel();
            diffPanel = new DiffResultPanel(sidebar.Direction)
            {
                SelectAllRequested = toggleSelectAll,
            };

            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = EzTheme.Background,
            };

            AddInternal(new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.HEADER_HEIGHT),
                    new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.PATH_SECTION_HEIGHT),
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.ACTION_BAR_HEIGHT),
                    new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.STATUS_BAR_HEIGHT),
                },
                Content = new[]
                {
                    createHeaderRow(),
                    createPathSection(),
                    createMainSection(),
                    createActionSection(),
                    createStatusSection(),
                },
            });
        }

        private Drawable[] createHeaderRow()
        {
            return new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = EzRealmSyncLayout.CONTENT_PADDING * 2 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 100),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new EzText
                            {
                                Text = launchOptions.UiTestMode ? "Ez Realm Sync [UI Test]" : "Ez Realm Sync",
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            }.WithSize(24, "Bold"),
                            new EzButton { Text = "设置", Action = toggleSettings }.Fill(),
                        },
                    },
                },
            };
        }

        private Drawable[] createPathSection()
        {
            return new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding
                    {
                        Horizontal = EzRealmSyncLayout.CONTENT_PADDING * 2,
                        Vertical = 4,
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.PATH_ROW_HEIGHT * 2 + 8),
                        new Dimension(GridSizeMode.Absolute, 36),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new EndpointPairPanel(
                                endpointAPath,
                                endpointBPath,
                                () => browsePath(endpointAPath, isEndpointA: true),
                                () => browsePath(endpointBPath, isEndpointA: false)),
                        },
                        new Drawable[]
                        {
                            scanButton = new EzButton
                            {
                                Text = "扫描 Diff",
                                Width = 140,
                                Height = 36,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Action = () => _ = runScanAsync(),
                            },
                        },
                    },
                },
            };
        }

        private Drawable[] createMainSection()
        {
            return new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding
                    {
                        Horizontal = EzRealmSyncLayout.CONTENT_PADDING * 2,
                        Bottom = EzRealmSyncLayout.CONTENT_PADDING,
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, EzRealmSyncLayout.SIDEBAR_WIDTH),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            sidebar,
                            diffPanel,
                        },
                    },
                },
            };
        }

        private Drawable[] createActionSection()
        {
            return new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = EzRealmSyncLayout.CONTENT_PADDING * 2 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 150),
                        new Dimension(GridSizeMode.Absolute, 8),
                        new Dimension(GridSizeMode.Absolute, 140),
                        new Dimension(GridSizeMode.Absolute, 8),
                        new Dimension(GridSizeMode.Absolute, 160),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            selectionCountText = new EzText
                            {
                                Text = "已选 0 项",
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            }.WithSize(14),
                            new EzButton { Text = "导出所选", Alpha = 0.4f }.Fill(),
                            new Spacer(),
                            deleteButton = new EzButton
                            {
                                Text = "从 A 端删除",
                                Action = () => confirmApply(deleteFromSource: true),
                            }.Fill(),
                            new Spacer(),
                            applyButton = new EzButton
                            {
                                Text = "添加到 B 端",
                                Action = () => confirmApply(deleteFromSource: false),
                            }.Fill(),
                        },
                    },
                },
            };
        }

        private Drawable[] createStatusSection()
        {
            return new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding
                    {
                        Horizontal = EzRealmSyncLayout.CONTENT_PADDING * 2,
                        Bottom = EzRealmSyncLayout.CONTENT_PADDING,
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 360),
                        new Dimension(GridSizeMode.Absolute, 12),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            progressBar = new EzProgressBar
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            },
                            new Spacer(),
                            statusText = new EzText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = launchOptions.UiTestMode ? "UI 测试模式 — 未连接真实数据库" : "就绪",
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            }.WithSize(13),
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            diffPanel.CategoryTabs.Current.BindValueChanged(_ =>
            {
                refreshList();
                updateApplyButtonState();
            }, true);

            sidebar.Direction.BindValueChanged(_ =>
            {
                updateActionButtonLabels();
                updateApplyButtonState();
            }, true);

            updateActionButtonLabels();

            if (launchOptions.UiTestMode)
                _ = runScanAsync();
        }

        private void updateActionButtonLabels()
        {
            DiffCategoryTabBar.getEndpointLabels(sidebar.Direction.Value, out string source, out string target);
            applyButton.Text = $"添加到 {target} 端";
            deleteButton.Text = $"从 {source} 端删除";
        }

        private void browsePath(Bindable<string> target, bool isEndpointA)
        {
            if (launchOptions.UiTestMode)
            {
                target.Value = isEndpointA
                    ? @"C:\Fake\Ez2Lazer\data"
                    : @"C:\Fake\osu\data";
                return;
            }

            string? initial = string.IsNullOrWhiteSpace(target.Value) ? null : target.Value;
            string? picked = NativeFolderPicker.PickFolder(initial);

            if (!string.IsNullOrEmpty(picked))
                target.Value = picked;
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
                    Direction = sidebar.Direction.Value,
                    Paths = createPaths(),
                    EntityKinds = getEntityKindsFromFilter(),
                };

                var progress = new Progress<ScanProgress>(p => Schedule(() =>
                {
                    progressBar.Progress = p.Progress;
                    statusText.Text = p.Message;
                }));

                lastScanResult = await syncService.ScanAsync(request, progress, token).ConfigureAwait(false);

                Schedule(() =>
                {
                    refreshList();
                    DiffCategoryTabBar.getEndpointLabels(sidebar.Direction.Value, out string source, out string target);
                    statusText.Text = $"扫描完成 — {source}端有{target}端无 {lastScanResult.SourceOnly.Count}，{target}端有{source}端无 {lastScanResult.TargetOnly.Count}，不一致 {lastScanResult.Conflicted.Count}";
                    progressBar.Progress = 1;
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
                showError("请先选择要处理的条目。");
                return;
            }

            if (!deleteFromSource && diffPanel.CategoryTabs.Current.Value == DiffCategory.Conflicted)
            {
                showError("「不一致」条目仅展示，不能添加到目标端。");
                return;
            }

            DiffCategoryTabBar.getEndpointLabels(sidebar.Direction.Value, out string source, out string target);

            if (deleteFromSource)
            {
                dialogs.PushDangerous(
                    $"确定从 {source} 端删除 {selected.Count} 项？此操作不可撤销。",
                    () => _ = runApplyAsync(selected, deleteFromSource: true));
            }
            else
            {
                dialogs.PushConfirm(
                    $"将 {selected.Count} 项添加到 {target} 端。",
                    () => _ = runApplyAsync(selected, deleteFromSource: false));
            }
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
                    Direction = sidebar.Direction.Value,
                    Paths = createPaths(),
                    ItemIds = selected.Select(i => i.Id).ToList(),
                    CreateBackup = !deleteFromSource,
                    DeleteFromSource = deleteFromSource,
                };

                var progress = new Progress<ApplyProgress>(p => Schedule(() =>
                {
                    progressBar.Progress = p.Progress;
                    statusText.Text = p.Message;
                }));

                var result = await syncService.ApplyAsync(request, progress, token).ConfigureAwait(false);

                Schedule(() =>
                {
                    DiffCategoryTabBar.getEndpointLabels(sidebar.Direction.Value, out string source, out string _);

                    statusText.Text = deleteFromSource
                        ? $"已从 {source} 端删除 {result.AppliedCount} 项"
                        : $"已添加 {result.AppliedCount} 项" + (result.BackupPath != null ? $"，备份: {result.BackupPath}" : string.Empty);
                    progressBar.Progress = 1;
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
                diffPanel.ListHost.Clear();
                activeListPanel = null;
                updateSelectionCount();
                return;
            }

            var items = diffPanel.CategoryTabs.Current.Value switch
            {
                DiffCategory.SourceOnly => lastScanResult.SourceOnly,
                DiffCategory.TargetOnly => lastScanResult.TargetOnly,
                DiffCategory.Conflicted => lastScanResult.Conflicted,
                _ => Array.Empty<DiffItem>(),
            };

            diffPanel.ListHost.Child = activeListPanel = new DiffListPanel(items);
            activeListPanel.SelectionChanged += updateSelectionCount;
            diffPanel.SelectAllButton.Text = "全选";
            updateSelectionCount();
            updateApplyButtonState();
        }

        private void toggleSelectAll()
        {
            if (activeListPanel == null)
                return;

            bool anyUnselected = activeListPanel.GetSelectedItems().Count() < activeListPanel.ItemCount;
            activeListPanel.SelectAll(anyUnselected);
            diffPanel.SelectAllButton.Text = anyUnselected ? "取消全选" : "全选";
            updateSelectionCount();
        }

        private void updateApplyButtonState()
        {
            bool conflicted = diffPanel.CategoryTabs.Current.Value == DiffCategory.Conflicted;
            applyButton.Enabled.Value = !isBusy && !conflicted;
            applyButton.Alpha = conflicted ? 0.4f : 1;
        }

        private void updateSelectionCount()
        {
            int count = activeListPanel?.GetSelectedItems().Count() ?? 0;
            selectionCountText.Text = $"已选 {count} 项";
        }

        private PathConfiguration createPaths() => new()
        {
            EzDataPath = endpointAPath.Value,
            OfficialDataPath = endpointBPath.Value,
        };

        private List<EntityKind> getEntityKindsFromFilter()
        {
            return sidebar.EntityFilter.Value switch
            {
                EntityKindFilter.All => new List<EntityKind> { EntityKind.BeatmapSet, EntityKind.Beatmap, EntityKind.Score, EntityKind.BeatmapCollection },
                EntityKindFilter.BeatmapSet => new List<EntityKind> { EntityKind.BeatmapSet },
                EntityKindFilter.Beatmap => new List<EntityKind> { EntityKind.Beatmap },
                EntityKindFilter.Score => new List<EntityKind> { EntityKind.Score },
                EntityKindFilter.BeatmapCollection => new List<EntityKind> { EntityKind.BeatmapCollection },
                _ => new List<EntityKind>(),
            };
        }

        private void setBusy(bool busy)
        {
            isBusy = busy;
            scanButton.Enabled.Value = !busy;
            deleteButton.Enabled.Value = !busy;
            diffPanel.SelectAllButton.Enabled.Value = !busy;
            updateApplyButtonState();
        }

        private void showError(string message)
        {
            statusText.Text = message;
            dialogs.PushConfirm(message, () => { });
        }

        private void toggleSettings()
        {
            if (settingsOverlay != null)
            {
                settingsOverlay.CloseOverlay();
                RemoveInternal(settingsOverlay, true);
                settingsOverlay = null;
                return;
            }

            MockEzRealmSyncService? mock = syncService as MockEzRealmSyncService;

            AddInternal(settingsOverlay = new EzRealmSyncSettingsOverlay(uiTestMode, mock)
            {
                CloseRequested = toggleSettings,
            });

            settingsOverlay.OpenOverlay();
        }

        private partial class Spacer : Drawable
        {
        }
    }
}
