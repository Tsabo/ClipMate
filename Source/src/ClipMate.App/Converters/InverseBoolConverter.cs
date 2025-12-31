using System.Globalization;
using System.Windows.Data;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts a boolean value to its inverse.
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean to its inverse.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;

        return false;
    }

    /// <summary>
    /// Converts an inverse boolean back to its original value.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;

        return false;
    }
}
