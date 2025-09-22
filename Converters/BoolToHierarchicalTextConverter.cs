using System;
using System.Globalization;
using System.Windows.Data;

namespace TaskMaster.Converters;

public class BoolToHierarchicalTextConverter : IValueConverter
{
    public static readonly BoolToHierarchicalTextConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isHierarchical)
        {
            return isHierarchical ? "Tree" : "Flat";
        }
        return "Flat";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}