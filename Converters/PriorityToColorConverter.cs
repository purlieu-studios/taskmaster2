using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaskMaster.Converters;

public class PriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int priority)
        {
            // Priority levels:
            // 1000+ = Critical (Red)
            // 100-999 = High (Orange)
            // 10-99 = Medium (Yellow)
            // 0-9 = Low (Green)

            return priority switch
            {
                >= 1000 => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // #EF4444 - Red
                >= 100 => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // #F59E0B - Orange
                >= 10 => new SolidColorBrush(Color.FromRgb(234, 179, 8)),     // #EAB308 - Yellow
                _ => new SolidColorBrush(Color.FromRgb(34, 197, 94))          // #22C55E - Green
            };
        }

        // Default to medium priority color
        return new SolidColorBrush(Color.FromRgb(234, 179, 8)); // #EAB308 - Yellow
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}