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

public enum RecordType { Weighing, Delivery }

public class HistoryViewModel : BindableBase, INavigationAware
{
    private readonly IArchiveQueryService _archive;
    private readonly IDeliveryService _delivery;
    private readonly IExportService _export;
    private readonly ILogService _log;
    private readonly AwsDbContext _db;

    public event Action<List<WeighingArchiveRecord>, int>? ShowChartRequested;

    // ── 类型切换 ──────────────────────────────────────────────
    private RecordType _activeType = RecordType.Weighing;
    public RecordType ActiveType
    {
        get => _activeType;
        set
        {
            if (SetProperty(ref _activeType, value))
            {
                RaisePropertyChanged(nameof(IsWeighingMode));
                RaisePropertyChanged(nameof(IsDeliveryMode));
                RaisePropertyChanged(nameof(TableTitle));
                RaisePropertyChanged(nameof(WeightSummaryLabel));
                _ = QueryAsync();
            }
        }
    }

    public bool IsWeighingMode => _activeType == RecordType.Weighing;
    public bool IsDeliveryMode => _activeType == RecordType.Delivery;
    public string TableTitle => IsWeighingMode ? "收货档案明细" : "送货档案明细";
    public string WeightSummaryLabel => IsWeighingMode ? "净重合计" : "重量合计";

    // ── 筛选 ─────────────────────────────────────────────────
    private DateTime _dateFrom = DateTime.Today.AddDays(-7);
    public DateTime DateFrom { get => _dateFrom; set => SetProperty(ref _dateFrom, value); }

    private DateTime _dateTo = DateTime.Today.AddDays(1).AddSeconds(-1);
    public DateTime DateTo { get => _dateTo; set => SetProperty(ref _dateTo, value); }

    private string _vehiclePlateFilter = string.Empty;
    public string VehiclePlateFilter { get => _vehiclePlateFilter; set => SetProperty(ref _vehiclePlateFilter, value); }

    private string _customerNameFilter = string.Empty;
    public string CustomerNameFilter { get => _customerNameFilter; set => SetProperty(ref _customerNameFilter, value); }

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

    // ── 收货结果 ──────────────────────────────────────────────
    private List<WeighingArchiveRecord> _allWeighingRecords = [];
    public List<WeighingArchiveRecord> SelectedWeighingRecords { get; private set; } = [];

    public void UpdateWeighingSelection(IEnumerable<WeighingArchiveRecord> records)
    {
        SelectedWeighingRecords = records.ToList();
        DeleteWeighingCommand.RaiseCanExecuteChanged();
        ShowChartCommand.RaiseCanExecuteChanged();
    }

    public ObservableCollection<WeighingArchiveRecord> WeighingRecords { get; } = [];

    // ── 送货结果 ──────────────────────────────────────────────
    private List<DeliveryRecord> _allDeliveryRecords = [];
    public List<DeliveryRecord> SelectedDeliveryRecords { get; private set; } = [];

    public void UpdateDeliverySelection(IEnumerable<DeliveryRecord> records)
    {
        SelectedDeliveryRecords = records.ToList();
        DeleteDeliveryCommand.RaiseCanExecuteChanged();
    }

    public ObservableCollection<DeliveryRecord> DeliveryRecords { get; } = [];

    // ── 汇总 ─────────────────────────────────────────────────
    private int _recordCount;
    public int RecordCount { get => _recordCount; private set => SetProperty(ref _recordCount, value); }

    private double _totalWeight;
    public double TotalWeight { get => _totalWeight; private set => SetProperty(ref _totalWeight, value); }

    private double _totalAmount;
    public double TotalAmount { get => _totalAmount; private set => SetProperty(ref _totalAmount, value); }

    // ── 分页（收货） ─────────────────────────────────────────
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
                UpdatePagedWeighingRecords();
            }
        }
    }

    private int _pageIndex = 1;
    public int PageIndex
    {
        get => _pageIndex;
        set { if (SetProperty(ref _pageIndex, value)) UpdatePagedWeighingRecords(); }
    }

    public int TotalPageCount => Math.Max(1, (int)Math.Ceiling(_allWeighingRecords.Count / (double)_pageSize));

    // ── 分页（送货） ─────────────────────────────────────────
    private int _deliveryPageSize = 20;
    public int DeliveryPageSize
    {
        get => _deliveryPageSize;
        set
        {
            if (SetProperty(ref _deliveryPageSize, value))
            {
                _deliveryPageIndex = 1;
                RaisePropertyChanged(nameof(DeliveryPageIndex));
                RaisePropertyChanged(nameof(DeliveryTotalPageCount));
                UpdatePagedDeliveryRecords();
            }
        }
    }

    private int _deliveryPageIndex = 1;
    public int DeliveryPageIndex
    {
        get => _deliveryPageIndex;
        set { if (SetProperty(ref _deliveryPageIndex, value)) UpdatePagedDeliveryRecords(); }
    }

    public int DeliveryTotalPageCount => Math.Max(1, (int)Math.Ceiling(_allDeliveryRecords.Count / (double)_deliveryPageSize));

    // ── 命令 ─────────────────────────────────────────────────
    public DelegateCommand QueryCommand { get; }
    public DelegateCommand ResetFilterCommand { get; }
    public DelegateCommand ExportCommand { get; }
    public DelegateCommand DeleteWeighingCommand { get; }
    public DelegateCommand DeleteDeliveryCommand { get; }
    public DelegateCommand<string> ShowChartCommand { get; }
    public DelegateCommand ShowAllChartCommand { get; }
    public DelegateCommand SwitchToWeighingCommand { get; }
    public DelegateCommand SwitchToDeliveryCommand { get; }

    public HistoryViewModel(IArchiveQueryService archive, IDeliveryService delivery,
        IExportService export, ILogService log, AwsDbContext db)
    {
        _archive = archive;
        _delivery = delivery;
        _export = export;
        _log = log;
        _db = db;

        QueryCommand        = new DelegateCommand(async () => await QueryAsync());
        ResetFilterCommand  = new DelegateCommand(ResetFilter);
        ExportCommand       = new DelegateCommand(async () => await ExportAsync(), () => RecordCount > 0);
        DeleteWeighingCommand = new DelegateCommand(async () => await DeleteWeighingAsync(),
            () => SelectedWeighingRecords.Count > 0);
        DeleteDeliveryCommand = new DelegateCommand(async () => await DeleteDeliveryAsync(),
            () => SelectedDeliveryRecords.Count > 0);
        ShowChartCommand    = new DelegateCommand<string>(
            chartTypeStr =>
            {
                if (int.TryParse(chartTypeStr, out int idx))
                    ShowChartRequested?.Invoke(SelectedWeighingRecords.ToList(), idx);
            },
            _ => SelectedWeighingRecords.Count >= 2);
        ShowAllChartCommand = new DelegateCommand(
            () => ShowChartRequested?.Invoke(_allWeighingRecords.ToList(), 0),
            () => _allWeighingRecords.Count >= 2);
        SwitchToWeighingCommand = new DelegateCommand(() => ActiveType = RecordType.Weighing);
        SwitchToDeliveryCommand = new DelegateCommand(() => ActiveType = RecordType.Delivery);
    }

    public async void OnNavigatedTo(NavigationContext ctx)
    {
        await LoadGoodsAsync();
        await QueryAsync();
    }

    public void OnNavigatedFrom(NavigationContext ctx) { }
    public bool IsNavigationTarget(NavigationContext ctx) => false;

    private async Task LoadGoodsAsync()
    {
        var goods = await _db.GoodsCategories
            .Where(g => g.IsActive).Select(g => g.Name).OrderBy(n => n).ToListAsync();
        AvailableGoods.Clear();
        AvailableGoods.Add(null);
        foreach (var g in goods) AvailableGoods.Add(g);
        SelectedGoods = null;
    }

    private async Task QueryAsync()
    {
        try
        {
            if (IsWeighingMode)
                await QueryWeighingAsync();
            else
                await QueryDeliveryAsync();
        }
        catch (Exception ex)
        {
            _log.Error($"查询失败：{ex.Message}", "历史查询");
        }
    }

    private async Task QueryWeighingAsync()
    {
        var availableYears = await _archive.GetAvailableYearsAsync();
        var yearsToQuery = availableYears.Where(y => y >= DateFrom.Year && y <= DateTo.Year).ToList();

        var plate    = string.IsNullOrWhiteSpace(VehiclePlateFilter) ? null : VehiclePlateFilter.Trim();
        var customer = string.IsNullOrWhiteSpace(CustomerNameFilter) ? null : CustomerNameFilter.Trim();
        var goods    = string.IsNullOrWhiteSpace(_goodsNameFilter)   ? null : _goodsNameFilter.Trim();

        var all = new List<WeighingArchiveRecord>();
        foreach (var year in yearsToQuery)
            all.AddRange(await _archive.QueryAsync(year, DateFrom, DateTo, plate, customer, goods));

        _allWeighingRecords = all.OrderByDescending(r => r.ArchivedAt).ToList();
        RecordCount = _allWeighingRecords.Count;
        TotalWeight = Math.Round(_allWeighingRecords.Sum(r => r.NetWeight), 1);
        TotalAmount = Math.Round(_allWeighingRecords.Sum(r => r.TotalAmount ?? 0), 2);
        _pageIndex = 1;
        RaisePropertyChanged(nameof(PageIndex));
        RaisePropertyChanged(nameof(TotalPageCount));
        UpdatePagedWeighingRecords();
        ExportCommand.RaiseCanExecuteChanged();
        ShowAllChartCommand.RaiseCanExecuteChanged();
        _log.Info($"收货查询完成：{RecordCount} 条", "历史查询");
    }

    private async Task QueryDeliveryAsync()
    {
        var customer = string.IsNullOrWhiteSpace(CustomerNameFilter) ? null : CustomerNameFilter.Trim();
        var goods    = string.IsNullOrWhiteSpace(_goodsNameFilter)   ? null : _goodsNameFilter.Trim();

        _allDeliveryRecords = await _delivery.QueryAsync(DateFrom, DateTo, customer, goods);
        RecordCount = _allDeliveryRecords.Count;
        TotalWeight = Math.Round(_allDeliveryRecords.Sum(r => r.TotalWeight), 1);
        TotalAmount = Math.Round(_allDeliveryRecords.Sum(r => r.TotalAmount ?? 0), 2);
        _deliveryPageIndex = 1;
        RaisePropertyChanged(nameof(DeliveryPageIndex));
        RaisePropertyChanged(nameof(DeliveryTotalPageCount));
        UpdatePagedDeliveryRecords();
        ExportCommand.RaiseCanExecuteChanged();
        _log.Info($"送货查询完成：{RecordCount} 条", "历史查询");
    }

    private void UpdatePagedWeighingRecords()
    {
        WeighingRecords.Clear();
        foreach (var r in _allWeighingRecords.Skip((_pageIndex - 1) * _pageSize).Take(_pageSize))
            WeighingRecords.Add(r);
        RaisePropertyChanged(nameof(TotalPageCount));
    }

    private void UpdatePagedDeliveryRecords()
    {
        DeliveryRecords.Clear();
        foreach (var r in _allDeliveryRecords.Skip((_deliveryPageIndex - 1) * _deliveryPageSize).Take(_deliveryPageSize))
            DeliveryRecords.Add(r);
        RaisePropertyChanged(nameof(DeliveryTotalPageCount));
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
        if (RecordCount == 0) return;
        var suffix = IsWeighingMode ? "收货记录" : "送货记录";
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx",
            FileName = $"{suffix}_{DateFrom:yyyyMMdd}_{DateTo:yyyyMMdd}_{DateTime.Now:HHmmss}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (IsWeighingMode)
                await _export.ExportToExcelAsync(_allWeighingRecords, dlg.FileName);
            else
                await _export.ExportDeliveryToExcelAsync(_allDeliveryRecords, dlg.FileName);
            _log.Info($"已导出 {RecordCount} 条：{dlg.FileName}", "历史查询");
        }
        catch (Exception ex)
        {
            _log.Error($"导出失败：{ex.Message}", "历史查询");
        }
    }

    private async Task DeleteWeighingAsync()
    {
        if (SelectedWeighingRecords.Count == 0) return;
        var toDelete = SelectedWeighingRecords.ToList();
        if (System.Windows.MessageBox.Show(
                $"确认删除选中的 {toDelete.Count} 条收货记录？此操作不可撤销。",
                "确认删除", System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
            return;

        int success = 0;
        foreach (var r in toDelete)
        {
            try { await _archive.DeleteAsync(r.ArchivedAt.Year, r.Id); _allWeighingRecords.Remove(r); success++; }
            catch (Exception ex) { _log.Error($"删除失败 {r.TicketNo}：{ex.Message}", "历史查询"); }
        }

        RecordCount = _allWeighingRecords.Count;
        TotalWeight = Math.Round(_allWeighingRecords.Sum(r => r.NetWeight), 1);
        TotalAmount = Math.Round(_allWeighingRecords.Sum(r => r.TotalAmount ?? 0), 2);
        RaisePropertyChanged(nameof(TotalPageCount));
        UpdatePagedWeighingRecords();
        ExportCommand.RaiseCanExecuteChanged();
        ShowAllChartCommand.RaiseCanExecuteChanged();
        _log.Warn($"已删除 {success}/{toDelete.Count} 条收货记录", "历史查询");
    }

    private async Task DeleteDeliveryAsync()
    {
        if (SelectedDeliveryRecords.Count == 0) return;
        var toDelete = SelectedDeliveryRecords.ToList();
        if (System.Windows.MessageBox.Show(
                $"确认删除选中的 {toDelete.Count} 条送货记录？此操作不可撤销。",
                "确认删除", System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
            return;

        int success = 0;
        foreach (var r in toDelete)
        {
            try { await _delivery.DeleteAsync(r.Id); _allDeliveryRecords.Remove(r); success++; }
            catch (Exception ex) { _log.Error($"删除失败 {r.TicketNo}：{ex.Message}", "历史查询"); }
        }

        RecordCount = _allDeliveryRecords.Count;
        TotalWeight = Math.Round(_allDeliveryRecords.Sum(r => r.TotalWeight), 1);
        TotalAmount = Math.Round(_allDeliveryRecords.Sum(r => r.TotalAmount ?? 0), 2);
        RaisePropertyChanged(nameof(DeliveryTotalPageCount));
        UpdatePagedDeliveryRecords();
        ExportCommand.RaiseCanExecuteChanged();
        _log.Warn($"已删除 {success}/{toDelete.Count} 条送货记录", "历史查询");
    }
}
