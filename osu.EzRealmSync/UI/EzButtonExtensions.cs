namespace osu.EzRealmSync.UI
{
    internal static class EzButtonExtensions
    {
        public static EzButton Fill(this EzButton button)
        {
            button.FillCell();
            return button;
        }
    }
}
