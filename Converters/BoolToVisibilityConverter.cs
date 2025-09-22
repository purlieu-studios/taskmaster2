using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskMaster.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            bool inverse = parameter?.ToString() == "Inverse";
            bool shouldShow = inverse ? !boolValue : boolValue;
            return shouldShow ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool inverse = parameter?.ToString() == "Inverse";
            bool isVisible = visibility == Visibility.Visible;
            return inverse ? !isVisible : isVisible;
        }
        return false;
    }
}