using AWS.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace AWS.UI.Converters;

public class CustomerTypeConverter : IValueConverter
{
    private static readonly Dictionary<CustomerType, string> Labels = new()
    {
        { CustomerType.Supplier, "供应商" },
        { CustomerType.Buyer,    "买家"   },
        { CustomerType.Both,     "两者"   },
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is CustomerType t && Labels.TryGetValue(t, out var label) ? label : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Labels.FirstOrDefault(kv => kv.Value == value?.ToString()).Key;
}
