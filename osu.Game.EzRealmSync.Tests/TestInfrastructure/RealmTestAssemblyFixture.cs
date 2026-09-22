using NUnit.Framework;

namespace osu.Game.EzRealmSync.Tests.TestInfrastructure;

/// <summary>
/// 整个测试程序集跑完后先强制终结一遍 Realm 原生句柄，再让进程退出。
///
/// 放在**全局命名空间**（不是某个子命名空间）：NUnit 的 <c>[SetUpFixture]</c> 只覆盖它所在的命名空间子树，
/// 放在 <c>...Tests.TestInfrastructure</c> 里就管不到 <c>...Tests</c> 下的测试类，那些夹具留下的
/// SharedRealm 会拖到进程退出才被终结，回调打到已销毁的 SynchronizationContext 上，
/// 以 <c>0xC0000005</c> 崩掉测试主机——表现是"测试全绿但运行中止"。
///
/// 真正的修复在各夹具自己释放；这里是兜底，保证即使有漏网的句柄也在进程还健康时被清掉。
/// </summary>
[SetUpFixture]
public sealed class RealmTestAssemblyFixture
{
    [OneTimeTearDown]
    public void FlushNativeRealmHandles() => RealmNativeLifetime.Flush();
}
