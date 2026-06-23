using AWS.Core.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AWS.UI.Converters;

[ValueConversion(typeof(UserRole), typeof(Brush))]
public class UserRoleToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not UserRole role) return Brushes.Gray;
        return role switch
        {
            UserRole.Operator  => new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
            UserRole.Admin     => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
            UserRole.SuperUser => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
            _                  => Brushes.Gray,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(UserRole), typeof(string))]
public class UserRoleToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not UserRole role) return string.Empty;
        return role switch
        {
            UserRole.Operator  => "操作员",
            UserRole.Admin     => "管理员",
            UserRole.SuperUser => "超级管理员",
            _                  => "未知",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(UserRole), typeof(string))]
public class UserRoleToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not UserRole role) return "?";
        return role switch
        {
            UserRole.Operator  => "员",
            UserRole.Admin     => "管",
            UserRole.SuperUser => "超",
            _                  => "?",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(string))]
public class BoolToActiveTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "已启用" : "已禁用";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
