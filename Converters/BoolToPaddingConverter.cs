using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskMaster.Converters;

public class BoolToPaddingConverter : IValueConverter
{
    public static readonly BoolToPaddingConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCompact)
        {
            return isCompact ? new Thickness(0) : new Thickness(24, 20, 24, 20);
        }
        return new Thickness(24, 20, 24, 20);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}