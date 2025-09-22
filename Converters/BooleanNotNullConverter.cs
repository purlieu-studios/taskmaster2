using System;
using System.Globalization;
using System.Windows.Data;

namespace TaskMaster.Converters;

public class BooleanNotNullConverter : IValueConverter
{
    public static readonly BooleanNotNullConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool inverse = parameter?.ToString() == "Inverse";
        bool isNotNull = value != null;
        return inverse ? !isNotNull : isNotNull;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("BooleanNotNullConverter does not support ConvertBack");
    }
}