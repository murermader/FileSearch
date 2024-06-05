using System;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Path = System.IO.Path;

namespace FileSearch;

public class IconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Result result = (Result)value;
        string filePath = Path.Combine(result.FilePath, result.FileName);
        Icon? icon = NativeFunctions.ExtractIcon(filePath);
        
        if (icon is null)
        {
            return DependencyProperty.UnsetValue;
        }
            
        // Converts Icon to ImageSource
        ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
            
        return imageSource;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}