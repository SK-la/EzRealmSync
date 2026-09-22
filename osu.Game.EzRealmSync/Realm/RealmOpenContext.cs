using RealmConfiguration = Realms.RealmConfiguration;
using RealmInstance = Realms.Realm;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 打开 Realm 时临时摘掉调用线程的 <see cref="SynchronizationContext"/>。
    ///
    /// realm-core 会记住打开瞬间线程上的上下文，之后用它 Post 回调
    /// （<c>Realms.SynchronizationContextScheduler</c>）。若该上下文先失效——测试进程里是 NUnit 的
    /// <c>SafeSynchronizationContext</c>，成品进程里是已关闭的 WPF Dispatcher——回调会打进已销毁的
    /// 原生 scheduler，表现为 0xC0000005 直接崩掉进程。本工具对 Realm 的访问全部是同步的，
    /// 不需要上下文调度，所以一律不绑定。
    /// </summary>
    internal static class RealmOpenContext
    {
        public static RealmInstance GetInstance(RealmConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            return WithoutCapturedContext(() => RealmInstance.GetInstance(configuration));
        }

        public static T WithoutCapturedContext<T>(Func<T> open)
        {
            ArgumentNullException.ThrowIfNull(open);

            SynchronizationContext? context = SynchronizationContext.Current;

            if (context == null)
                return open();

            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                return open();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }
        }

        public static void WithoutCapturedContext(Action open)
        {
            ArgumentNullException.ThrowIfNull(open);
            WithoutCapturedContext(() =>
            {
                open();
                return true;
            });
        }
    }
}
