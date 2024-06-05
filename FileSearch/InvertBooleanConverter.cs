using System;
using System.Globalization;
using System.Windows.Data;

namespace FileSearch;

public class InvertBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool bValue && !bValue;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool bValue && !bValue;
    }
}