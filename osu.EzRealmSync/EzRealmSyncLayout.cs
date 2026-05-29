namespace osu.EzRealmSync
{
    /// <summary>
    /// 与 PerformanceCalculatorGUI 类似，使用固定尺寸行避免 AutoSize 嵌套导致重叠。
    /// </summary>
    internal static class EzRealmSyncLayout
    {
        public const float HEADER_HEIGHT = 48;
        public const float PATH_ROW_HEIGHT = 40;
        public const float PATH_SECTION_HEIGHT = PATH_ROW_HEIGHT * 2 + 8 + 36;
        public const float ACTION_BAR_HEIGHT = 48;
        public const float STATUS_BAR_HEIGHT = 40;
        public const float SIDEBAR_WIDTH = 250;
        public const float DIFF_TAB_BAR_HEIGHT = 40;
        public const float CONTENT_PADDING = 12;
        public const float LABEL_WIDTH = 56;
        public const float BROWSE_BUTTON_WIDTH = 88;
    }
}
