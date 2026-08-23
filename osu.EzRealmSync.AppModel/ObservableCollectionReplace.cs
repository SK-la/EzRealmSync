using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;

namespace osu.EzRealmSync.AppModel
{
    /// <summary>
    /// 一次 Reset 通知替换 ObservableCollection 内容，避免逐条 Add 刷 UI。
    /// </summary>
    public static class ObservableCollectionReplace
    {
        public static void ReplaceAll<T>(ObservableCollection<T> target, IList<T> items)
        {
            if (target.Count == 0 && items.Count == 0)
                return;

            if (tryReplaceViaItems(target, items))
                return;

            target.Clear();
            foreach (var item in items)
                target.Add(item);
        }

        private static bool tryReplaceViaItems<T>(ObservableCollection<T> target, IList<T> items)
        {
            try
            {
                var itemsProp = typeof(ObservableCollection<T>).GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic);
                if (itemsProp?.GetValue(target) is not IList<T> list)
                    return false;

                var checkReentrancy = typeof(ObservableCollection<T>).GetMethod("CheckReentrancy", BindingFlags.Instance | BindingFlags.NonPublic);
                checkReentrancy?.Invoke(target, null);

                list.Clear();
                foreach (var item in items)
                    list.Add(item);

                var onPropertyChanged = typeof(ObservableCollection<T>).GetMethod(
                    "OnPropertyChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);

                // Prefer PropertyChangedEventArgs overload
                var onProp = typeof(ObservableCollection<T>).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "OnPropertyChanged" && m.GetParameters().Length == 1);

                var onCollectionChanged = typeof(ObservableCollection<T>).GetMethod(
                    "OnCollectionChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(NotifyCollectionChangedEventArgs) },
                    null);

                onProp?.Invoke(target, new object[] { new System.ComponentModel.PropertyChangedEventArgs("Count") });
                onProp?.Invoke(target, new object[] { new System.ComponentModel.PropertyChangedEventArgs("Item[]") });
                onCollectionChanged?.Invoke(target, new object[] { new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset) });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
