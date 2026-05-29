using System.Collections.Concurrent;
using System.Text;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using FontStyle = SixLabors.Fonts.FontStyle;

namespace osu.EzRealmSync.Platform
{
    /// <summary>
    /// Roboto 缺字时，用系统已安装字体即时栅格化（与 osu 位图字体并存，作为 nested fallback）。
    /// </summary>
    internal sealed class SystemFontGlyphLookupStore : ITextureStore, ITexturedGlyphLookupStore
    {
        /// <summary>与 Framework 位图字体相同的参考字号。</summary>
        private const float reference_size = 100f;

        private const float metric_scale = 1f / reference_size;

        private static readonly string[] preferred_families =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "PingFang SC",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "SimHei",
            "Segoe UI",
        };

        private readonly IRenderer renderer;
        private readonly FontFamily fontFamily;
        private readonly ConcurrentDictionary<(int codepoint, FontStyle style), ITexturedCharacterGlyph?> cache = new();

        public string FamilyName { get; }

        public SystemFontGlyphLookupStore(IRenderer renderer)
        {
            this.renderer = renderer;
            fontFamily = resolveFamily();
            FamilyName = fontFamily.Name;
        }

        public ITexturedCharacterGlyph? Get(string? fontName, char character) => Get(fontName, (int)character);

        public ITexturedCharacterGlyph? Get(string? fontName, int codepoint)
        {
            if (codepoint < 0 || !Rune.IsValid(codepoint))
                return null;

            var style = getStyle(fontName);
            return cache.GetOrAdd((codepoint, style), _ => rasterise(codepoint, style));
        }

        public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => Task.FromResult(Get(fontName, character));

        public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, int codepoint) => Task.FromResult(Get(fontName, codepoint));

        private ITexturedCharacterGlyph? rasterise(int codepoint, FontStyle style)
        {
            if (codepoint > char.MaxValue)
                return null;

            try
            {
                var font = fontFamily.CreateFont(reference_size, style);
                string text = char.ConvertFromUtf32(codepoint);

                var options = new TextOptions(font);
                var bounds = TextMeasurer.MeasureBounds(text, options);
                var advance = TextMeasurer.MeasureAdvance(text, options);

                int width = Math.Max(1, (int)Math.Ceiling(bounds.Width) + 2);
                int height = Math.Max(1, (int)Math.Ceiling(bounds.Height) + 2);

                float drawX = -bounds.X + 1;
                float drawY = -bounds.Y + 1;

                // 勿 using：TextureUpload 会延迟读取像素，上传完成前 Image 不能被释放。
                var image = new Image<Rgba32>(width, height);
                image.Mutate(ctx => ctx.DrawText(text, font, Color.White, new PointF(drawX, drawY)));

                var texture = renderer.CreateTexture(width, height);
                texture.SetData(new TextureUpload(image));

                float unitsPerEm = font.FontMetrics.UnitsPerEm;
                float baseline = font.FontMetrics.HorizontalMetrics.Ascender / unitsPerEm * reference_size;

                var glyph = new CharacterGlyph(
                    (char)codepoint,
                    (float)bounds.X,
                    (float)bounds.Y,
                    (float)advance.Width,
                    baseline,
                    containingStore: null);

                return new TexturedCharacterGlyph(glyph, texture, metric_scale);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static FontStyle getStyle(string? fontName)
        {
            if (fontName != null && fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase))
                return FontStyle.Bold;

            if (fontName != null && fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase))
                return FontStyle.Italic;

            return FontStyle.Regular;
        }

        private static FontFamily resolveFamily()
        {
            foreach (string name in preferred_families)
            {
                if (SystemFonts.TryGet(name, out var family))
                    return family;
            }

            foreach (var family in SystemFonts.Families)
            {
                if (family.Name.Contains("YaHei", StringComparison.OrdinalIgnoreCase)
                    || family.Name.Contains("PingFang", StringComparison.OrdinalIgnoreCase)
                    || family.Name.Contains("Noto Sans CJK", StringComparison.OrdinalIgnoreCase)
                    || family.Name.Contains("Source Han", StringComparison.OrdinalIgnoreCase))
                {
                    return family;
                }
            }

            return SystemFonts.Families.First();
        }

        Texture? IResourceStore<Texture>.Get(string name) => null;

        Task<Texture?> IResourceStore<Texture>.GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<Texture?>(null);

        Stream IResourceStore<Texture>.GetStream(string name) => throw new NotSupportedException();

        IEnumerable<string> IResourceStore<Texture>.GetAvailableResources() => Array.Empty<string>();

        public Texture? Get(string name, WrapMode wrapModeS, WrapMode wrapModeT) => null;

        public Task<Texture?> GetAsync(string name, WrapMode wrapModeS, WrapMode wrapModeT, CancellationToken cancellationToken = default) => Task.FromResult<Texture?>(null);

        public void Dispose()
        {
        }
    }
}
