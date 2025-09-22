using System;
using System.Globalization;
using System.Windows.Data;

namespace TaskMaster.Converters;

public class BoolToViewTextConverter : IValueConverter
{
    public static readonly BoolToViewTextConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isCompact)
        {
            return isCompact ? "📋 Card" : "📄 Compact";
        }
        return "📋 Card";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}