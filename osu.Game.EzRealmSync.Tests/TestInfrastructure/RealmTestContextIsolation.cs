using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: RealmTestContextIsolation]

/// <summary>
/// 每个测试执行期间都摘掉当前线程的 <see cref="SynchronizationContext"/>。
///
/// 原因：Realm .NET 会把「延迟回调」排到调用线程当时的 SynchronizationContext 上（内部就是
/// <c>Realms.SynchronizationContextScheduler</c>）。测试进程里那个上下文是 NUnit 的
/// <c>SafeSynchronizationContext</c>，夹具结束、上下文销毁之后回调才落地，就会以
/// <c>0xC0000005</c> 崩掉测试主机——表现是"测试全绿但运行中止"。
///
/// 摘掉上下文后 Realm 改走线程池调度（与产品侧 <c>RealmOpenContext</c> 的取舍一致：
/// 我们只做同步读写，不需要回调回到原线程）。挂在程序集级别，避免每个夹具各写一遍、
/// 也避免新夹具漏写。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class RealmTestContextIsolationAttribute : Attribute, ITestAction
{
    private static readonly AsyncLocal<SynchronizationContext?> outer_context = new();

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        outer_context.Value = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
    }

    public void AfterTest(ITest test)
    {
        SynchronizationContext.SetSynchronizationContext(outer_context.Value);
        outer_context.Value = null;
    }
}
