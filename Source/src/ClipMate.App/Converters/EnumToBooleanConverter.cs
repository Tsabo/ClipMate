using System.Globalization;
using System.Windows.Data;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts an enum value to a boolean for RadioButton binding.
/// Used to bind radio buttons to enum properties.
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return false;
        }

        return value.Equals(parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            return parameter;
        }

        return System.Windows.Data.Binding.DoNothing;
    }
}
