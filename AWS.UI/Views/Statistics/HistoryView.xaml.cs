using AWS.Core.Entities;
using AWS.UI.ViewModels.Statistics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AWS.UI.Views.Statistics;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();

        // ViewModelLocator 在 InitializeComponent 内部同步设置 DataContext，
        // 因此必须在 InitializeComponent 之后立即订阅当前 VM 的事件，
        // 再设置 DataContextChanged 以应对后续可能的替换。
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

    private void RecordsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm && sender is DataGrid dg)
            vm.UpdateSelection(dg.SelectedItems.Cast<WeighingArchiveRecord>());
    }

    // 右键点击已选中的行时，阻止 DataGrid 将多选重置为单选，
    // 确保 ContextMenu 打开时 SelectedRecords 中仍保留之前的多选结果。
    private void RecordsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep &&
            ItemsControl.ContainerFromElement(RecordsGrid, dep) is DataGridRow row &&
            row.IsSelected)
            e.Handled = true;
    }
}
