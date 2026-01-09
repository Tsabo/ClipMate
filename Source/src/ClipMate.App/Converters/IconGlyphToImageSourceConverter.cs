using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ClipMate.App.Helpers;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts an icon glyph string to an ImageSource using CustomFontIconSource.
/// Used for dynamic icon binding where the glyph changes at runtime.
/// </summary>
public class IconGlyphToImageSourceConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the size of the icon in pixels. Default is 24.
    /// </summary>
    public double Size { get; set; } = 24;

    /// <summary>
    /// Converts an icon glyph string to an ImageSource.
    /// </summary>
    /// <param name="value">The icon glyph string (e.g., "\uE008").</param>
    /// <param name="targetType">The target type (ImageSource).</param>
    /// <param name="parameter">Optional parameter (unused).</param>
    /// <param name="culture">The culture to use (unused).</param>
    /// <returns>An ImageSource representing the icon, or null if conversion fails.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string glyph || string.IsNullOrEmpty(glyph))
            return null;

        // Use CustomFontIconSourceExtension to create the ImageSource
        var iconSource = new CustomFontIconSourceExtension(glyph)
        {
            Size = Size
        };

        return iconSource.ProvideValue(null!) as ImageSource;
    }

    /// <summary>
    /// Not implemented - this is a one-way converter.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
