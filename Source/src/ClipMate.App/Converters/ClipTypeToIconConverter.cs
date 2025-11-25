using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ClipMate.Core.Models;
using Wpf.Ui.Controls;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts a ClipType to the appropriate icon symbol for display in the DataGrid.
/// Based on ClipMate 7.5 icon conventions (section 2.5.4 of user manual).
/// </summary>
public class ClipTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ClipType clipType)
        {
            return SymbolRegular.Document24; // Default
        }

        return clipType switch
        {
            ClipType.Text => SymbolRegular.DocumentText24,           // Plain text - document icon
            ClipType.RichText => SymbolRegular.TextFont24,           // Rich text - "A" with formatting
            ClipType.Html => SymbolRegular.Code24,                   // HTML - code/markup icon
            ClipType.Image => SymbolRegular.Image24,                 // Bitmap/Image - picture icon
            ClipType.Files => SymbolRegular.Folder24,                // HDROP - folder icon
            _ => SymbolRegular.Document24
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a ClipType to a tooltip describing the format.
/// </summary>
public class ClipTypeToTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ClipType clipType)
        {
            return "Unknown format";
        }

        return clipType switch
        {
            ClipType.Text => "Text – Plain text with no formatting",
            ClipType.RichText => "Rich Text Format – contains font, alignment, color, etc.",
            ClipType.Html => "HTML Format",
            ClipType.Image => "Bitmap – Image",
            ClipType.Files => "HDROP – Contains a list of files, copied from Windows Explorer",
            _ => "Unknown format"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to "*" or empty string for format indicators.
/// </summary>
public class BooleanToAsteriskConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "*" : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
