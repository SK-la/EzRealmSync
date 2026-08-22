using Realms;

namespace osu.Game.EzRealmSync.OfficialSchema
{
    /// <summary>
    /// 与 osu.Game RealmExtensions.Write 相同：用 lambda 参数而非捕获外层 using 变量，避免闭包分析误报。
    /// </summary>
    internal static class OfficialMirrorRealmWriteExtensions
    {
        public static void Write(this Realm realm, Action<Realm> function)
        {
            realm.Write<object?>(r =>
            {
                function(r);
                return null;
            });
        }

        public static T Write<T>(this Realm realm, Func<Realm, T> function)
        {
            Transaction? transaction = null;

            try
            {
                if (!realm.IsInTransaction)
                    transaction = realm.BeginWrite();

                T result = function(realm);

                transaction?.Commit();

                return result;
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }
}
