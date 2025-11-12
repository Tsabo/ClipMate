using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts an enum value to Visibility based on whether it matches a parameter.
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var enumValue = value.ToString();
        var parameterValue = parameter.ToString();

        return string.Equals(enumValue, parameterValue, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
