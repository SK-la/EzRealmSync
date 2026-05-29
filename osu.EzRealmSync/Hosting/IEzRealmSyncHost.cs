// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.EzRealmSync.Hosting
{
    /// <summary>
    /// 工具宿主抽象。当前仅实现独立进程；未来可在 osu 内以规则集/模块形式挂载另一实现。
    /// </summary>
    public interface IEzRealmSyncHost
    {
        void Run(EzRealmSyncLaunchOptions options);
    }
}
