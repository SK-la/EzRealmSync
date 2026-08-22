using osu.EzRealmSync.AppModel;
using osu.EzRealmSync.Desktop.Pages;
using osu.EzRealmSync.Desktop.ViewModels;
using Wpf.Ui.Abstractions;

namespace osu.EzRealmSync.Desktop.Services
{
    public sealed class WorkspacePageProvider : INavigationViewPageProvider
    {
        private readonly Dictionary<Type, object> pages = new Dictionary<Type, object>();
        private ShellViewModel? shell;

        public void Attach(ShellViewModel viewModel)
        {
            shell = viewModel;

            foreach (var page in pages.Values.OfType<FrameworkElement>())
                page.DataContext = viewModel;
        }

        public object? GetPage(Type pageType)
        {
            if (!pages.TryGetValue(pageType, out object? page))
            {
                page = pageType.Name switch
                {
                    nameof(ImportPage) => new ImportPage(),
                    nameof(DataPage) => new DataPage(),
                    nameof(SyncPage) => new SyncPage(),
                    nameof(FixPage) => new FixPage(),
                    nameof(ExportPage) => new ExportPage(),
                    _ => Activator.CreateInstance(pageType) ?? throw new InvalidOperationException($"无法创建页面：{pageType.Name}"),
                };

                pages[pageType] = page;
            }

            if (page is FrameworkElement element)
            {
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
                element.VerticalAlignment = VerticalAlignment.Stretch;

                if (shell != null && !ReferenceEquals(element.DataContext, shell))
                    element.DataContext = shell;
            }

            return page;
        }

        public static Type PageTypeForTab(MainWorkspaceTab tab) => tab switch
        {
            MainWorkspaceTab.Data => typeof(DataPage),
            MainWorkspaceTab.Sync => typeof(SyncPage),
            MainWorkspaceTab.Fix => typeof(FixPage),
            MainWorkspaceTab.Export => typeof(ExportPage),
            _ => typeof(ImportPage),
        };

        public static MainWorkspaceTab TabForPageType(Type pageType)
        {
            if (pageType == typeof(DataPage))
                return MainWorkspaceTab.Data;

            if (pageType == typeof(SyncPage))
                return MainWorkspaceTab.Sync;

            if (pageType == typeof(FixPage))
                return MainWorkspaceTab.Fix;

            if (pageType == typeof(ExportPage))
                return MainWorkspaceTab.Export;

            return MainWorkspaceTab.Import;
        }
    }
}
