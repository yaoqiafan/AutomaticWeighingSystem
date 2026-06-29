using AWS.Core.Entities;
using AWS.UI.ViewModels.Statistics;
using System.Windows;
using System.Windows.Controls;

namespace AWS.UI.Views.Statistics;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();

        if (DataContext is HistoryViewModel current)
            current.ShowChartRequested += OpenChartWindow;

        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is HistoryViewModel vm)
                vm.ShowChartRequested += OpenChartWindow;
        };
    }

    private void OpenChartWindow(List<WeighingArchiveRecord> records, int chartTypeIndex)
    {
        var vm = new ChartPopupViewModel();
        vm.Initialize(records, chartTypeIndex, IsDarkTheme());
        new ChartPopupWindow { DataContext = vm }.Show();
    }

    private static bool IsDarkTheme() =>
        Application.Current?.Resources
            .MergedDictionaries[0]
            .MergedDictionaries.FirstOrDefault()?.Source?.ToString()
            .Contains("Dark", StringComparison.OrdinalIgnoreCase) ?? false;

    private void WeighingGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm && sender is DataGrid dg)
            vm.UpdateWeighingSelection(dg.SelectedItems.Cast<WeighingArchiveRecord>());
    }

    private void DeliveryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm && sender is DataGrid dg)
            vm.UpdateDeliverySelection(dg.SelectedItems.Cast<DeliveryRecord>());
    }
}
