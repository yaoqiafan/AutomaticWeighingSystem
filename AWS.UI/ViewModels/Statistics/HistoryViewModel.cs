using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;

namespace AWS.UI.ViewModels.Statistics;

public class HistoryViewModel : BindableBase, INavigationAware
{
    private readonly IArchiveQueryService _archive;
    private readonly IExportService _export;
    private readonly ILogService _log;
    private readonly AwsDbContext _db;

    // 图表弹窗请求事件（由 View code-behind 订阅）
    public event Action<List<WeighingArchiveRecord>, int>? ShowChartRequested;

    // ── 筛选 ─────────────────────────────────────────────────
    // DateTime（非可空）以匹配 DateTimeSelector 控件的 StartTime/EndTime 属性类型
    private DateTime _dateFrom = DateTime.Today.AddDays(-7);
    public DateTime DateFrom { get => _dateFrom; set => SetProperty(ref _dateFrom, value); }

    private DateTime _dateTo = DateTime.Today.AddDays(1).AddSeconds(-1);
    public DateTime DateTo { get => _dateTo; set => SetProperty(ref _dateTo, value); }

    private string _vehiclePlateFilter = string.Empty;
    public string VehiclePlateFilter { get => _vehiclePlateFilter; set => SetProperty(ref _vehiclePlateFilter, value); }

    private string _customerNameFilter = string.Empty;
    public string CustomerNameFilter { get => _customerNameFilter; set => SetProperty(ref _customerNameFilter, value); }

    // 货物下拉（null 表示"全部"）
    public ObservableCollection<string?> AvailableGoods { get; } = [];

    private string? _selectedGoods;
    public string? SelectedGoods
    {
        get => _selectedGoods;
        set
        {
            SetProperty(ref _selectedGoods, value);
            _goodsNameFilter = value ?? string.Empty;
        }
    }

    private string _goodsNameFilter = string.Empty;

    // ── 结果 ─────────────────────────────────────────────────
    private List<WeighingArchiveRecord> _allRecords = [];

    private int _recordCount;
    public int RecordCount { get => _recordCount; private set => SetProperty(ref _recordCount, value); }

    private double _totalNetWeight;
    public double TotalNetWeight { get => _totalNetWeight; private set => SetProperty(ref _totalNetWeight, value); }

    public ObservableCollection<WeighingArchiveRecord> Records { get; } = [];

    // 由 View code-behind SelectionChanged 维护
    public List<WeighingArchiveRecord> SelectedRecords { get; private set; } = [];

    public void UpdateSelection(IEnumerable<WeighingArchiveRecord> records)
    {
        SelectedRecords = records.ToList();
        DeleteCommand.RaiseCanExecuteChanged();
        ShowChartCommand.RaiseCanExecuteChanged();
    }

    // ── 分页 ─────────────────────────────────────────────────
    public int[] AvailablePageSizes { get; } = [10, 20, 50, 100];

    private int _pageSize = 20;
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (SetProperty(ref _pageSize, value))
            {
                _pageIndex = 1;
                RaisePropertyChanged(nameof(PageIndex));
                RaisePropertyChanged(nameof(TotalPageCount));
                UpdatePagedRecords();
            }
        }
    }

    private int _pageIndex = 1;
    public int PageIndex
    {
        get => _pageIndex;
        set
        {
            if (SetProperty(ref _pageIndex, value))
                UpdatePagedRecords();
        }
    }

    public int TotalPageCount => Math.Max(1, (int)Math.Ceiling(_allRecords.Count / (double)_pageSize));

    // ── 命令 ─────────────────────────────────────────────────
    public DelegateCommand QueryCommand { get; }
    public DelegateCommand ResetFilterCommand { get; }
    public DelegateCommand ExportCommand { get; }
    public DelegateCommand DeleteCommand { get; }
    public DelegateCommand<string> ShowChartCommand { get; }
    // 用全量筛选结果绘制图表（不依赖当前页选中行）
    public DelegateCommand ShowAllChartCommand { get; }

    public HistoryViewModel(IArchiveQueryService archive, IExportService export, ILogService log, AwsDbContext db)
    {
        _archive = archive;
        _export = export;
        _log = log;
        _db = db;

        QueryCommand       = new DelegateCommand(async () => await QueryAsync());
        ResetFilterCommand = new DelegateCommand(ResetFilter);
        ExportCommand      = new DelegateCommand(async () => await ExportAsync(), () => _allRecords.Count > 0);
        DeleteCommand      = new DelegateCommand(async () => await DeleteAsync(), () => SelectedRecords.Count > 0);
        ShowChartCommand   = new DelegateCommand<string>(
            chartTypeStr =>
            {
                if (int.TryParse(chartTypeStr, out int idx))
                    ShowChartRequested?.Invoke(SelectedRecords.ToList(), idx);
            },
            _ => SelectedRecords.Count >= 2);
        ShowAllChartCommand = new DelegateCommand(
            () => ShowChartRequested?.Invoke(_allRecords.ToList(), 0),
            () => _allRecords.Count >= 2);
    }

    public async void OnNavigatedTo(NavigationContext ctx)
    {
        await LoadGoodsAsync();
        await QueryAsync();
    }

    public void OnNavigatedFrom(NavigationContext ctx) { }

    // 每次导航都创建新实例，避免旧数据残留
    public bool IsNavigationTarget(NavigationContext ctx) => false;

    private async Task LoadGoodsAsync()
    {
        var goods = await _db.GoodsCategories
            .Where(g => g.IsActive)
            .Select(g => g.Name)
            .OrderBy(n => n)
            .ToListAsync();

        AvailableGoods.Clear();
        AvailableGoods.Add(null);  // "全部"
        foreach (var g in goods) AvailableGoods.Add(g);
        SelectedGoods = null;
    }

    // 跨年多表查询：根据 DateFrom.Year ~ DateTo.Year 自动合并多张年表
    private async Task QueryAsync()
    {
        try
        {
            var availableYears = await _archive.GetAvailableYearsAsync();
            var yearsToQuery = availableYears
                .Where(y => y >= DateFrom.Year && y <= DateTo.Year)
                .ToList();

            var plate    = string.IsNullOrWhiteSpace(VehiclePlateFilter)  ? null : VehiclePlateFilter.Trim();
            var customer = string.IsNullOrWhiteSpace(CustomerNameFilter)  ? null : CustomerNameFilter.Trim();
            var goods    = string.IsNullOrWhiteSpace(_goodsNameFilter)    ? null : _goodsNameFilter.Trim();

            var allResults = new List<WeighingArchiveRecord>();
            foreach (var year in yearsToQuery)
            {
                var yearRecords = await _archive.QueryAsync(year, DateFrom, DateTo, plate, customer, goods);
                allResults.AddRange(yearRecords);
            }

            _allRecords = allResults.OrderByDescending(r => r.ArchivedAt).ToList();
            RecordCount = _allRecords.Count;
            TotalNetWeight = Math.Round(_allRecords.Sum(r => r.NetWeight), 1);
            _pageIndex = 1;
            RaisePropertyChanged(nameof(PageIndex));
            RaisePropertyChanged(nameof(TotalPageCount));
            UpdatePagedRecords();
            ExportCommand.RaiseCanExecuteChanged();
            ShowAllChartCommand.RaiseCanExecuteChanged();

            var span = yearsToQuery.Count > 1
                ? $"{yearsToQuery.Last()}~{yearsToQuery.First()} 共 {yearsToQuery.Count} 张表"
                : yearsToQuery.Count == 1 ? $"{yearsToQuery[0]} 年表" : "无匹配年表";
            _log.Info($"查询完成：{_allRecords.Count} 条，{span}", "历史查询");
        }
        catch (Exception ex)
        {
            _log.Error($"查询失败：{ex.Message}", "历史查询");
        }
    }

    private void UpdatePagedRecords()
    {
        Records.Clear();
        foreach (var r in _allRecords.Skip((_pageIndex - 1) * _pageSize).Take(_pageSize))
            Records.Add(r);
        RaisePropertyChanged(nameof(TotalPageCount));
    }

    private void ResetFilter()
    {
        DateFrom = DateTime.Today.AddDays(-30);
        DateTo = DateTime.Today.AddDays(1).AddSeconds(-1);
        VehiclePlateFilter = string.Empty;
        CustomerNameFilter = string.Empty;
        SelectedGoods = null;
    }

    private async Task ExportAsync()
    {
        if (_allRecords.Count == 0) return;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx",
            FileName = $"过磅记录_{DateFrom:yyyyMMdd}_{DateTo:yyyyMMdd}_{DateTime.Now:HHmmss}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await _export.ExportToExcelAsync(_allRecords, dlg.FileName);
            _log.Info($"已导出 {_allRecords.Count} 条：{dlg.FileName}", "历史查询");
        }
        catch (Exception ex)
        {
            _log.Error($"导出失败：{ex.Message}", "历史查询");
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedRecords.Count == 0) return;
        var toDelete = SelectedRecords.ToList();
        var confirm = System.Windows.MessageBox.Show(
            $"确认删除选中的 {toDelete.Count} 条磅单记录？此操作不可撤销。",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        int success = 0;
        foreach (var r in toDelete)
        {
            try
            {
                // 从记录自身的 ArchivedAt 推断年份，支持跨年删除
                await _archive.DeleteAsync(r.ArchivedAt.Year, r.Id);
                _allRecords.Remove(r);
                success++;
            }
            catch (Exception ex)
            {
                _log.Error($"删除磅单 {r.TicketNo} 失败：{ex.Message}", "历史查询");
            }
        }

        RecordCount = _allRecords.Count;
        TotalNetWeight = Math.Round(_allRecords.Sum(r => r.NetWeight), 1);
        RaisePropertyChanged(nameof(TotalPageCount));
        UpdatePagedRecords();
        ExportCommand.RaiseCanExecuteChanged();
        ShowAllChartCommand.RaiseCanExecuteChanged();
        _log.Warn($"已删除 {success}/{toDelete.Count} 条记录", "历史查询");
    }
}
