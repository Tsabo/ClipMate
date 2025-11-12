using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts null values to Visibility. Non-null becomes Visible, null becomes Collapsed.
/// </summary>
public class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
