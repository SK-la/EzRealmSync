namespace osu.EzRealmSync.Platform
{
    internal static class NativeFolderPicker
    {
        public static string? PickFolder(string? initialPath)
        {
            if (OperatingSystem.IsWindows())
                return pickWindows(initialPath);

            return null;
        }

        private static string? pickWindows(string? initialPath)
        {
#if WINDOWS
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择数据目录",
                UseDescriptionForTitle = true,
            };

            if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                dialog.InitialDirectory = initialPath;

            return dialog.ShowDialog() == DialogResult.OK
                ? dialog.SelectedPath
                : null;
#else
            return null;
#endif
        }
    }
}
