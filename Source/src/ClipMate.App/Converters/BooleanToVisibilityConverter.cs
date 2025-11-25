using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts a boolean to Visibility (Visible if true, Collapsed if false)
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}
