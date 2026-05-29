using osu.Framework.Graphics.Sprites;

namespace osu.EzRealmSync.UI
{
    public partial class EzText : SpriteText
    {
        public EzText()
        {
            Font = EzTheme.Font();
            Colour = EzTheme.Text;
        }

        public EzText WithSize(float size, string? weight = null)
        {
            Font = EzTheme.Font(size, weight);
            return this;
        }
    }
}
