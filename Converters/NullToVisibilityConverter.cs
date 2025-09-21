using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskMaster.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isNull = value == null || (value is string str && string.IsNullOrWhiteSpace(str));
        var inverse = parameter?.ToString() == "Inverse";

        if (inverse)
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}