using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace AWS.UI.ViewModels.Statistics;

public class DashboardViewModel : BindableBase, INavigationAware
{
    private readonly IWeighingService _weighing;
    private readonly IArchiveQueryService _archive;
    private readonly IDeliveryService _delivery;
    private readonly ILogService _log;
    private readonly IContainerProvider _container;
    private readonly AwsDbContext _db;
    private readonly DispatcherTimer _timer;

    // ── 统计卡片 ──────────────────────────────────────────────
    private string _todayReceiveWeight = "0 kg";
    public string TodayReceiveWeight { get => _todayReceiveWeight; private set => SetProperty(ref _todayReceiveWeight, value); }

    private string _todayDeliveryWeight = "0 kg";
    public string TodayDeliveryWeight { get => _todayDeliveryWeight; private set => SetProperty(ref _todayDeliveryWeight, value); }

    private string _currentInventory = "0 kg";
    public string CurrentInventory { get => _currentInventory; private set => SetProperty(ref _currentInventory, value); }

    private string _todayExpenditure = "¥ --";
    public string TodayExpenditure { get => _todayExpenditure; private set => SetProperty(ref _todayExpenditure, value); }

    private string _todayIncome = "¥ --";
    public string TodayIncome { get => _todayIncome; private set => SetProperty(ref _todayIncome, value); }

    private string _lastRefreshTime = "--:--:--";
    public string LastRefreshTime { get => _lastRefreshTime; private set => SetProperty(ref _lastRefreshTime, value); }

    // ── 今日趋势图（按品类动态折线 + 汇总 + 送货） ───────────
    public ObservableCollection<ISeries> HourlySeries { get; } = [];
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    private static readonly SKColor[] _categoryColors =
    [
        new(0x42, 0xA5, 0xF5), // 蓝
        new(0xFF, 0xB7, 0x4D), // 橙
        new(0xAB, 0x47, 0xBC), // 紫
        new(0x26, 0xC6, 0xDA), // 青
        new(0xEF, 0x53, 0x50), // 红
        new(0xFF, 0xEE, 0x58), // 黄
        new(0xEC, 0x40, 0x7A), // 粉
        new(0x9C, 0xCC, 0x65), // 浅绿
    ];

    // ── 库存趋势图 ────────────────────────────────────────────
    public ObservableCollection<ISeries> InventorySeries { get; } = [];
    public Axis[] InventoryXAxes { get; private set; } = [];
    public Axis[] InventoryYAxes { get; } =
    [
        new Axis
        {
            Name = "kg",
            NameTextSize = 10,
            TextSize = 9,
            LabelsPaint = Charts.ChartPaints.Text(),
            NamePaint = Charts.ChartPaints.Text(),
        }
    ];

    public ObservableCollection<string?> InventoryCategories { get; } = [];
    private string? _selectedInventoryCategory;
    public string? SelectedInventoryCategory
    {
        get => _selectedInventoryCategory;
        set
        {
            if (SetProperty(ref _selectedInventoryCategory, value))
                _ = RefreshInventoryChartAsync();
        }
    }

    public int[] AvailableInventoryDays { get; } = [7, 14, 30];
    private int _inventoryDays = 7;
    public int InventoryDays
    {
        get => _inventoryDays;
        set
        {
            if (SetProperty(ref _inventoryDays, value))
                _ = RefreshInventoryChartAsync();
        }
    }

    // ── 今日明细 Tab ─────────────────────────────────────────
    private bool _showDeliveryTab;
    public bool ShowDeliveryTab
    {
        get => _showDeliveryTab;
        set => SetProperty(ref _showDeliveryTab, value);
    }

    // 收货明细分页
    private List<WeighingArchiveRecord> _allReceiveRecords = [];
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
                UpdatePagedReceiveRecords();
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
                UpdatePagedReceiveRecords();
        }
    }

    public int TotalPageCount => Math.Max(1, (int)Math.Ceiling(_allReceiveRecords.Count / (double)_pageSize));

    public ObservableCollection<WeighingArchiveRecord> TodayReceiveRecords { get; } = [];

    // 送货明细分页
    private List<DeliveryRecord> _allDeliveryRecords = [];

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
        set
        {
            if (SetProperty(ref _deliveryPageIndex, value))
                UpdatePagedDeliveryRecords();
        }
    }

    public int DeliveryTotalPageCount => Math.Max(1, (int)Math.Ceiling(_allDeliveryRecords.Count / (double)_deliveryPageSize));

    public ObservableCollection<DeliveryRecord> TodayDeliveryRecords { get; } = [];

    // ── 命令 ─────────────────────────────────────────────────
    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand AddDeliveryCommand { get; }
    public DelegateCommand ShowInventoryTrendCommand { get; }
    public DelegateCommand SwitchToReceiveTabCommand { get; }
    public DelegateCommand SwitchToDeliveryTabCommand { get; }

    public DashboardViewModel(
        IWeighingService weighing,
        IArchiveQueryService archive,
        IDeliveryService delivery,
        ILogService log,
        IContainerProvider container,
        AwsDbContext db)
    {
        _weighing = weighing;
        _archive = archive;
        _delivery = delivery;
        _log = log;
        _container = container;
        _db = db;

        XAxes =
        [
            new Axis
            {
                Labeler = v => $"{(int)v}时",
                MinStep = 1,
                MinLimit = 0,
                MaxLimit = 23,
                TextSize = 9,
                LabelsPaint = Charts.ChartPaints.Text(),
            }
        ];
        YAxes =
        [
            new Axis
            {
                Name = "kg",
                NameTextSize = 10,
                TextSize = 9,
                LabelsPaint = Charts.ChartPaints.Text(),
                NamePaint = Charts.ChartPaints.Text(),
            }
        ];

        RefreshCommand = new DelegateCommand(async () => await RefreshAsync());
        AddDeliveryCommand = new DelegateCommand(OpenAddDeliveryDialog);
        ShowInventoryTrendCommand = new DelegateCommand(OpenInventoryTrendWindow);
        SwitchToReceiveTabCommand = new DelegateCommand(() => ShowDeliveryTab = false);
        SwitchToDeliveryTabCommand = new DelegateCommand(() => ShowDeliveryTab = true);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public async void OnNavigatedTo(NavigationContext ctx)
    {
        await LoadInventoryCategoriesAsync();
        await RefreshAsync();
        _timer.Start();
    }

    public void OnNavigatedFrom(NavigationContext ctx) => _timer.Stop();

    public bool IsNavigationTarget(NavigationContext ctx) => false;

    private async Task LoadInventoryCategoriesAsync()
    {
        var goods = await _db.GoodsCategories
            .Where(g => g.IsActive)
            .Select(g => g.Name)
            .OrderBy(n => n)
            .ToListAsync();

        InventoryCategories.Clear();
        InventoryCategories.Add(null); // 全部
        foreach (var g in goods) InventoryCategories.Add(g);
    }

    private async Task RefreshAsync()
    {
        try
        {
            var unit = _db.SystemSettings.Find(SettingKeys.WeightUnit)?.Value ?? "kg";
            bool isTon = unit == "ton";
            double divisor = isTon ? 1000.0 : 1.0;
            string unitLabel = isTon ? "t" : "kg";

            // 今日收货统计
            var (totalWeight, count, totalAmount) = await _weighing.GetTodayStatsAsync();
            TodayReceiveWeight = $"{totalWeight / divisor:N1} {unitLabel}";
            TodayExpenditure = totalAmount > 0 ? $"¥ {totalAmount:N2}" : "¥ --";

            // 今日送货
            var hourlyDelivery = await _delivery.GetTodayHourlyWeightAsync();
            var deliveryWeight = await _delivery.GetTodayTotalWeightAsync();
            var income = await _delivery.GetTodayTotalAmountAsync();
            TodayDeliveryWeight = $"{deliveryWeight / divisor:N1} {unitLabel}";
            TodayIncome = income > 0 ? $"¥ {income:N2}" : "¥ --";

            // 当前库存
            var inventory = await _delivery.GetCurrentInventoryAsync();
            double totalStock = inventory.Values.Sum();
            CurrentInventory = $"{totalStock / divisor:N1} {unitLabel}";

            // 今日收货明细（同时用于品类折线图）
            var today = DateTime.Today;
            var receiveRecords = await _archive.QueryAsync(today.Year, today, today.AddDays(1).AddSeconds(-1));
            _allReceiveRecords = receiveRecords.OrderByDescending(r => r.ArchivedAt).ToList();

            // ── 品类折线：每笔记录一个点，X = 实际时分（小数小时） ─
            var categoryPoints = receiveRecords
                .GroupBy(r => r.GoodsName)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        double cat = 0;
                        return g.OrderBy(r => r.ArchivedAt)
                                .Select(r =>
                                {
                                    cat += r.NetWeight;
                                    return new ObservablePoint(
                                        r.ArchivedAt.Hour + r.ArchivedAt.Minute / 60.0,
                                        cat);
                                })
                                .ToArray();
                    });

            // ── 汇总折线：每笔记录一个点，Y = 到该时刻的累计净重 ─
            double running = 0;
            var summaryPoints = receiveRecords
                .OrderBy(r => r.ArchivedAt)
                .Select(r =>
                {
                    running += r.NetWeight;
                    return new ObservablePoint(
                        r.ArchivedAt.Hour + r.ArchivedAt.Minute / 60.0,
                        running);
                })
                .ToArray();

            // ── 送货折线：按整点小时 ──────────────────────────────
            var deliveryPoints = hourlyDelivery
                .Select((v, i) => new ObservablePoint(i, v))
                .ToArray();

            HourlySeries.Clear();

            int ci = 0;
            foreach (var (goodsName, pts) in categoryPoints)
            {
                var clr = _categoryColors[ci++ % _categoryColors.Length];
                HourlySeries.Add(new LineSeries<ObservablePoint>
                {
                    Values = pts,
                    Name = goodsName,
                    Fill = null,
                    GeometrySize = 8,
                    Stroke = new SolidColorPaint(clr, 2),
                    GeometryFill = new SolidColorPaint(clr),
                    GeometryStroke = null,
                    LineSmoothness = 0,
                });
            }

            HourlySeries.Add(new LineSeries<ObservablePoint>
            {
                Values = summaryPoints,
                Name = "汇总",
                Fill = null,
                GeometrySize = 6,
                Stroke = new SolidColorPaint(new SKColor(0xE0, 0xE0, 0xE0), 2.5f),
                GeometryFill = new SolidColorPaint(new SKColor(0xE0, 0xE0, 0xE0)),
                GeometryStroke = null,
                LineSmoothness = 0,
            });

            HourlySeries.Add(new LineSeries<ObservablePoint>
            {
                Values = deliveryPoints,
                Name = "送货重量",
                Fill = null,
                GeometrySize = 6,
                Stroke = new SolidColorPaint(new SKColor(0x66, 0xBB, 0x6A), 2),
                GeometryFill = new SolidColorPaint(new SKColor(0x66, 0xBB, 0x6A)),
                GeometryStroke = null,
                LineSmoothness = 0,
            });

            XAxes[0].MinLimit = null;
            XAxes[0].MaxLimit = null;
            YAxes[0].MinLimit = null;
            YAxes[0].MaxLimit = null;

            _pageIndex = 1;
            RaisePropertyChanged(nameof(PageIndex));
            RaisePropertyChanged(nameof(TotalPageCount));
            UpdatePagedReceiveRecords();

            // 今日送货明细
            _allDeliveryRecords = await _delivery.GetTodayRecordsAsync();
            _deliveryPageIndex = 1;
            RaisePropertyChanged(nameof(DeliveryPageIndex));
            RaisePropertyChanged(nameof(DeliveryTotalPageCount));
            UpdatePagedDeliveryRecords();

            await RefreshInventoryChartAsync();

            LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            _log.Error($"今日汇总刷新失败：{ex.Message}", "今日汇总");
        }
    }

    private async Task RefreshInventoryChartAsync()
    {
        try
        {
            var from = DateTime.Today.AddDays(-(_inventoryDays - 1));
            var to = DateTime.Today;

            var cats = _selectedInventoryCategory != null
                ? new[] { _selectedInventoryCategory }
                : null;

            var trend = await _delivery.GetInventoryTrendAsync(from, to, cats);

            var colors = new[]
            {
                new SKColor(0x42, 0xA5, 0xF5),
                new SKColor(0x66, 0xBB, 0x6A),
                new SKColor(0xFF, 0xB7, 0x4D),
                new SKColor(0xAB, 0x47, 0xBC),
                new SKColor(0x26, 0xC6, 0xDA),
            };

            var labels = Enumerable.Range(0, _inventoryDays)
                .Select(d => from.AddDays(d).ToString("MM/dd"))
                .ToArray();

            InventoryXAxes =
            [
                new Axis
                {
                    Labels = labels,
                    TextSize = 9,
                    LabelsPaint = Charts.ChartPaints.Text(),
                }
            ];
            RaisePropertyChanged(nameof(InventoryXAxes));

            InventorySeries.Clear();
            int ci = 0;
            foreach (var (goodsName, points) in trend)
            {
                var color = colors[ci % colors.Length];
                ci++;
                InventorySeries.Add(new LineSeries<double>
                {
                    Values = points.Select(p => Math.Max(0, p.Stock)).ToArray(),
                    Name = goodsName,
                    Fill = null,
                    GeometrySize = 5,
                    Stroke = new SolidColorPaint(color, 2),
                    GeometryFill = new SolidColorPaint(color),
                    GeometryStroke = null,
                });
            }
        }
        catch (Exception ex)
        {
            _log.Error($"库存趋势刷新失败：{ex.Message}", "今日汇总");
        }
    }

    private void OpenInventoryTrendWindow()
    {
        _ = RefreshInventoryChartAsync();
        var win = new Views.Statistics.InventoryTrendWindow
        {
            DataContext = this,
            Owner = System.Windows.Application.Current.MainWindow
        };
        win.Show();
    }

    private void OpenAddDeliveryDialog()
    {
        var vm = _container.Resolve<AddDeliveryDialogViewModel>();
        var dlg = new Views.Statistics.AddDeliveryDialogWindow(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dlg.ShowDialog() == true)
            _ = RefreshAsync();
    }

    private void UpdatePagedReceiveRecords()
    {
        TodayReceiveRecords.Clear();
        foreach (var r in _allReceiveRecords.Skip((_pageIndex - 1) * _pageSize).Take(_pageSize))
            TodayReceiveRecords.Add(r);
        RaisePropertyChanged(nameof(TotalPageCount));
    }

    private void UpdatePagedDeliveryRecords()
    {
        TodayDeliveryRecords.Clear();
        foreach (var r in _allDeliveryRecords.Skip((_deliveryPageIndex - 1) * _deliveryPageSize).Take(_deliveryPageSize))
            TodayDeliveryRecords.Add(r);
        RaisePropertyChanged(nameof(DeliveryTotalPageCount));
    }
}
