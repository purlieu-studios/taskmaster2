using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaskMaster.Converters;

public class ComplexityToColorConverter : IValueConverter
{
    public static readonly ComplexityToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int complexity)
        {
            // Green (0-30): Low complexity
            if (complexity <= 30)
                return new SolidColorBrush(Color.FromRgb(34, 197, 94)); // #22C55E (green-500)

            // Yellow (31-60): Medium complexity
            if (complexity <= 60)
                return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // #F59E0B (amber-500)

            // Red (61-100): High complexity
            return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #EF4444 (red-500)
        }

        // Default to green for 0 or invalid values
        return new SolidColorBrush(Color.FromRgb(34, 197, 94));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}