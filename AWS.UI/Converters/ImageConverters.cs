using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AWS.UI.Converters;

[ValueConversion(typeof(string), typeof(BitmapImage))]
public class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(Uri))]
public class PathToUriConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        try { return new Uri(path, UriKind.Absolute); }
        catch { return null; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(Visibility))]
public class ImagePathToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasImage = value is string s && !string.IsNullOrWhiteSpace(s) && File.Exists(s);
        bool invert = parameter is string p && p == "Invert";
        return (hasImage ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
