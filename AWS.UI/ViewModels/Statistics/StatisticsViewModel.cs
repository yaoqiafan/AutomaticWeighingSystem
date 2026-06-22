using AWS.Core.Entities;
using AWS.Core.Interfaces;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace AWS.UI.ViewModels.Statistics;

public class StatisticsViewModel : BindableBase, INavigationAware
{
    private readonly IWeighingService _weighing;
    private readonly IArchiveQueryService _archive;
    private readonly IExportService _export;

    // ── 图表（今日每小时净重） ─────────────────────────────
    private readonly ObservableValue[] _hourlyValues =
        Enumerable.Range(0, 24).Select(_ => new ObservableValue(0)).ToArray();

    public ISeries[] HourlySeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    private string _todayTotalWeight = "0 kg";
    public string TodayTotalWeight { get => _todayTotalWeight; private set => SetProperty(ref _todayTotalWeight, value); }

    private int _todayVehicleCount;
    public int TodayVehicleCount { get => _todayVehicleCount; private set => SetProperty(ref _todayVehicleCount, value); }

    // ── 筛选 ───────────────────────────────────────────────
    public ObservableCollection<int> AvailableYears { get; } = [];

    private int _selectedYear = DateTime.Now.Year;
    public int SelectedYear
    {
        get => _selectedYear;
        set { SetProperty(ref _selectedYear, value); }
    }

    private DateTime? _dateFrom = DateTime.Today.AddDays(-7);
    public DateTime? DateFrom { get => _dateFrom; set => SetProperty(ref _dateFrom, value); }

    private DateTime? _dateTo = DateTime.Today;
    public DateTime? DateTo { get => _dateTo; set => SetProperty(ref _dateTo, value); }

    private string _vehiclePlateFilter = string.Empty;
    public string VehiclePlateFilter { get => _vehiclePlateFilter; set => SetProperty(ref _vehiclePlateFilter, value); }

    // ── 结果 ───────────────────────────────────────────────
    public ObservableCollection<WeighingArchiveRecord> Records { get; } = [];

    private WeighingArchiveRecord? _selectedRecord;
    public WeighingArchiveRecord? SelectedRecord
    {
        get => _selectedRecord;
        set { SetProperty(ref _selectedRecord, value); DeleteCommand.RaiseCanExecuteChanged(); }
    }

    private int _recordCount;
    public int RecordCount { get => _recordCount; private set => SetProperty(ref _recordCount, value); }

    private double _totalNetWeight;
    public double TotalNetWeight { get => _totalNetWeight; private set => SetProperty(ref _totalNetWeight, value); }

    public DelegateCommand QueryCommand { get; }
    public DelegateCommand ResetFilterCommand { get; }
    public DelegateCommand ExportCommand { get; }
    public DelegateCommand DeleteCommand { get; }

    public StatisticsViewModel(
        IWeighingService weighing,
        IArchiveQueryService archive,
        IExportService export)
    {
        _weighing = weighing;
        _archive = archive;
        _export = export;

        HourlySeries =
        [
            new LineSeries<ObservableValue>
            {
                Values = _hourlyValues,
                Name = "净重 kg",
                Fill = null,
                GeometrySize = 6,
                Stroke = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5), 2),
                GeometryFill = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5)),
                GeometryStroke = null,
            }
        ];

        XAxes =
        [
            new Axis { Labels = Enumerable.Range(0, 24).Select(h => $"{h}时").ToArray(), TextSize = 9 }
        ];
        YAxes = [new Axis { Name = "kg", NameTextSize = 10, TextSize = 9 }];

        QueryCommand = new DelegateCommand(async () => await QueryAsync());
        ResetFilterCommand = new DelegateCommand(ResetFilter);
        ExportCommand = new DelegateCommand(async () => await ExportAsync(), () => Records.Count > 0);
        DeleteCommand = new DelegateCommand(async () => await DeleteAsync(), () => SelectedRecord != null);
    }

    public async void OnNavigatedTo(NavigationContext ctx)
    {
        await LoadYearsAsync();
        await RefreshTodayStatsAsync();
        await QueryAsync();
    }

    public void OnNavigatedFrom(NavigationContext ctx) { }
    public bool IsNavigationTarget(NavigationContext ctx) => true;

    private async Task LoadYearsAsync()
    {
        var years = await _archive.GetAvailableYearsAsync();
        if (!years.Contains(DateTime.Now.Year)) years = [DateTime.Now.Year, ..years];
        years = years.Distinct().OrderByDescending(y => y).ToList();

        AvailableYears.Clear();
        foreach (var y in years) AvailableYears.Add(y);
        if (AvailableYears.Count > 0 && !AvailableYears.Contains(SelectedYear))
            SelectedYear = AvailableYears[0];
    }

    private async Task RefreshTodayStatsAsync()
    {
        var hourly = await _weighing.GetTodayHourlyNetWeightAsync();
        var (total, count) = await _weighing.GetTodayStatsAsync();
        for (int h = 0; h < 24; h++) _hourlyValues[h].Value = hourly[h];
        TodayTotalWeight = $"{total:N0} kg";
        TodayVehicleCount = count;
    }

    private async Task QueryAsync()
    {
        try
        {
            var records = await _archive.QueryAsync(
                SelectedYear, DateFrom, DateTo,
                string.IsNullOrWhiteSpace(VehiclePlateFilter) ? null : VehiclePlateFilter.Trim());

            Records.Clear();
            foreach (var r in records) Records.Add(r);

            RecordCount = Records.Count;
            TotalNetWeight = Math.Round(Records.Sum(r => r.NetWeight), 1);
            ExportCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"查询失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ResetFilter()
    {
        DateFrom = DateTime.Today.AddMonths(-1);
        DateTo = DateTime.Today;
        VehiclePlateFilter = string.Empty;
    }

    private async Task ExportAsync()
    {
        if (Records.Count == 0) return;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx",
            FileName = $"过磅记录_{SelectedYear}_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await _export.ExportToExcelAsync(Records.ToList(), dlg.FileName);
            System.Windows.MessageBox.Show($"已导出 {Records.Count} 条记录到：\n{dlg.FileName}",
                "导出成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedRecord == null) return;
        var r = SelectedRecord;
        var confirm = System.Windows.MessageBox.Show(
            $"确认删除磅单 {r.TicketNo}？", "确认删除",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _archive.DeleteAsync(SelectedYear, r.Id);
            Records.Remove(r);
            RecordCount = Records.Count;
            TotalNetWeight = Math.Round(Records.Sum(x => x.NetWeight), 1);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
