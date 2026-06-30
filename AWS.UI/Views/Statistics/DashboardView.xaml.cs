using AWS.Core.Entities;
using AWS.UI.Views.Charts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AWS.UI.Views.Statistics;

public partial class DashboardView : UserControl
{
    public DashboardView() => InitializeComponent();

    private void OnReceiveRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        if (FindVisualParent<DataGridRow>(src)?.DataContext is not WeighingArchiveRecord rec) return;
        new ReceiveDetailWindow { DataContext = rec, Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void OnDeliveryRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        if (FindVisualParent<DataGridRow>(src)?.DataContext is not DeliveryRecord del) return;
        new DeliveryDetailWindow { DataContext = del, Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null and not T)
            current = VisualTreeHelper.GetParent(current);
        return current as T;
    }
}
