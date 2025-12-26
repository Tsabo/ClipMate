using System.Windows.Markup;
using System.Windows.Media;

namespace ClipMate.App.Helpers;

/// <summary>
/// XAML markup extension that creates an ImageSource from a custom color font glyph.
/// Uses vendored Emoji.Wpf library to render COLR/CPAL color layers from ClipMate.ttf.
/// Usage: Glyph="{helpers:CustomFontIconSource Glyph={x:Static local:Icons.Clipboard}, Size=16}"
/// </summary>
[MarkupExtensionReturnType(typeof(ImageSource))]
public class CustomFontIconSourceExtension : MarkupExtension
{
    private static Emoji.Wpf.EmojiTypeface? s_customTypeface;
    private static readonly object s_lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFontIconSourceExtension" /> class.
    /// </summary>
    public CustomFontIconSourceExtension()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFontIconSourceExtension" /> class with the specified glyph.
    /// </summary>
    /// <param name="glyph">The glyph character to render (e.g., "\uE000").</param>
    public CustomFontIconSourceExtension(string glyph)
    {
        Glyph = glyph;
    }

    /// <summary>
    /// Gets or sets the glyph character to render.
    /// </summary>
    public string Glyph { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the glyph in pixels. Default is 16.
    /// </summary>
    public double Size { get; set; } = 16;

    /// <summary>
    /// Returns an ImageSource created from the custom font glyph with COLR color layers.
    /// Uses a dedicated EmojiTypeface instance for ClipMate.ttf to avoid interfering with standard emojis.
    /// </summary>
    /// <param name="serviceProvider">Object that can provide services for the markup extension.</param>
    /// <returns>An ImageSource containing the rendered colored glyph.</returns>
    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Glyph))
            return null;

        // Lazy-load the custom font typeface once
        if (s_customTypeface == null)
        {
            lock (s_lock)
            {
                if (s_customTypeface == null)
                {
                    var fontUri = "pack://application:,,,/ClipMate.App;component/Assets/ClipMate.ttf";
                    s_customTypeface = new Emoji.Wpf.EmojiTypeface(fontUri);
                }
            }
        }

        // Render the glyph using the custom font
        var drawingGroup = new DrawingGroup();
        using (var dc = drawingGroup.Open())
        {
            var glyphPlans = s_customTypeface.MakeGlyphPlanList(Glyph);
            var scale = s_customTypeface.GetScale(Size);
            var baseline = s_customTypeface.Baseline;

            foreach (var plan in glyphPlans)
            {
                var xpos = plan.OffsetX * scale;
                var ypos = baseline + plan.OffsetY * scale;

                foreach (var (glyphRun, brush) in s_customTypeface.DrawGlyph(plan.glyphIndex))
                {
                    dc.PushTransform(new TranslateTransform(xpos, ypos));
                    dc.PushTransform(new ScaleTransform(scale, scale));
                    dc.DrawGlyphRun(brush, glyphRun);
                    dc.Pop(); // scale
                    dc.Pop(); // translate
                }
            }
        }

        return new DrawingImage(drawingGroup);
    }
}
