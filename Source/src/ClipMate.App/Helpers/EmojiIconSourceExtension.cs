using System.Windows.Markup;
using System.Windows.Media;
using Image = Emoji.Wpf.Image;

namespace ClipMate.App.Helpers;

/// <summary>
/// XAML markup extension that creates an ImageSource from an emoji character using emoji-wpf.
/// Usage: Glyph="{helpers:EmojiIconSource Emoji='🎨', Size=16}"
/// </summary>
[MarkupExtensionReturnType(typeof(ImageSource))]
public class EmojiIconSourceExtension : MarkupExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmojiIconSourceExtension" /> class.
    /// </summary>
    public EmojiIconSourceExtension()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmojiIconSourceExtension" /> class with the specified emoji.
    /// </summary>
    /// <param name="emoji">The emoji character to render.</param>
    public EmojiIconSourceExtension(string emoji)
    {
        Emoji = emoji;
    }

    /// <summary>
    /// Gets or sets the emoji character to render.
    /// </summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the emoji in pixels. Default is 16.
    /// </summary>
    public double Size { get; set; } = 16;

    /// <summary>
    /// Returns an ImageSource created from the emoji using emoji-wpf.
    /// </summary>
    /// <param name="serviceProvider">Object that can provide services for the markup extension.</param>
    /// <returns>An ImageSource containing the rendered emoji.</returns>
    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Emoji))
            return null;

        // Use emoji-wpf's Image.SetSource method to create the ImageSource
        var drawingImage = new DrawingImage();
        Image.SetSource(drawingImage, Emoji);

        if (drawingImage.Drawing == null)
            return drawingImage;

        var drawingGroup = new DrawingGroup();

        // Get the original bounds
        var bounds = drawingImage.Drawing.Bounds;
        var scale = Size / Math.Max(bounds.Width, bounds.Height);

        // Apply scale transform
        drawingGroup.Transform = new ScaleTransform(scale, scale);
        drawingGroup.Children.Add(drawingImage.Drawing);

        return new DrawingImage(drawingGroup);
    }
}
