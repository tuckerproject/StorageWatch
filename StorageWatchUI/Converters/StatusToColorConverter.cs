using StorageWatchUI.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StorageWatchUI.Converters;

/// <summary>
/// Converts DiskStatusLevel to a color brush for UI display.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DiskStatusLevel status)
        {
            return status switch
            {
                DiskStatusLevel.OK => new SolidColorBrush(Colors.Green),
                DiskStatusLevel.Warning => new SolidColorBrush(Colors.Orange),
                DiskStatusLevel.Critical => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
