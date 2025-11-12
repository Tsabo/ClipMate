using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts a string to Visibility (Collapsed if null/empty, Visible otherwise)
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
