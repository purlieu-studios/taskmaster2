using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskMaster.Converters;

public class IntToVisibilityConverter : IValueConverter
{
    public static readonly IntToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            // Check for parameter to determine threshold
            int threshold = 0;
            if (parameter != null && int.TryParse(parameter.ToString(), out int paramThreshold))
            {
                threshold = paramThreshold;
            }

            return intValue > threshold ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}