using System.Globalization;
using System.Windows.Data;

namespace ClipMate.App.Converters;

/// <summary>
/// Converts status bar values into a formatted string for clip statistics.
/// Expected binding: MultiBinding with 7 bindings:
/// - TotalBytes, TotalChars, TotalWords (for text clips or collection totals)
/// - IsImageSelected (bool indicating if image clip is selected)
/// - IsLoadingImage (bool indicating if image dimensions are loading)
/// - ImageWidth, ImageHeight (pixel dimensions for images)
/// Output formats:
/// - Text/Collection: "X Bytes, Y Chars, Z Words"
/// - Image (loaded): "X × Y Pixels"
/// - Image (loading): "Loading..."
/// </summary>
public class StatusBarStatisticsConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 7)
            return string.Empty;

        try
        {
            var bytes = System.Convert.ToInt64(values[0]);
            var chars = System.Convert.ToInt64(values[1]);
            var words = System.Convert.ToInt64(values[2]);
            var isImageSelected = System.Convert.ToBoolean(values[3]);
            var isLoadingImage = System.Convert.ToBoolean(values[4]);
            var imageWidth = System.Convert.ToInt32(values[5]);
            var imageHeight = System.Convert.ToInt32(values[6]);

            // Image clip selected
            if (isImageSelected)
            {
                if (isLoadingImage || imageWidth == 0 || imageHeight == 0)
                    return "Loading...";

                return $"{imageWidth:N0} × {imageHeight:N0} Pixels";
            }

            // Text clip or collection totals
            return $"{bytes:N0} Bytes, {chars:N0} Chars, {words:N0} Words";
        }
        catch
        {
            return string.Empty;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
