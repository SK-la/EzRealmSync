using System.Diagnostics;

namespace osu.EzRealmSync.AppModel
{
    /// <summary>
    /// 从同步入口（ICommand、路由事件等）安全启动异步工作，避免 <c>async void</c> 未处理异常导致进程崩溃。
    /// </summary>
    public static class SafeAsyncInvoker
    {
        /// <summary>
        /// 未传入 onError 时使用的全局处理器（由 Shell 在启动时设置）。
        /// </summary>
        public static Action<Exception>? DefaultExceptionHandler { get; set; }

        public static void Run(Func<Task> work, Action<Exception>? onError = null)
        {
            ArgumentNullException.ThrowIfNull(work);
            _ = runCore(work, onError);
        }

        private static async Task runCore(Func<Task> work, Action<Exception>? onError)
        {
            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    (onError ?? DefaultExceptionHandler)?.Invoke(ex);
                }
                catch (Exception handlerEx)
                {
                    Trace.TraceError($"SafeAsyncInvoker handler failed: {handlerEx}");
                }
            }
        }
    }
}
