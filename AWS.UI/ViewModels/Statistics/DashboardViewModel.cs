using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using AWS.UI.Views.Charts;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows;
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

    private Dictionary<double, WeighingArchiveRecord> _receivePointMap = [];
    private Dictionary<double, DeliveryRecord> _deliveryPointMap = [];

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

    public ObservableCollection<string> InventoryCategories { get; } = [];
    private string _selectedInventoryCategory = "";
    public string SelectedInventoryCategory
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
    public DelegateCommand<ChartPoint> ChartPointDownCommand { get; }

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
        ChartPointDownCommand = new DelegateCommand<ChartPoint>(OnChartPointDown);

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
        InventoryCategories.Add(""); // 全部品类（空字符串作哨兵）
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

            // 今日送货明细（同时用于送货折线图）
            _allDeliveryRecords = await _delivery.GetTodayRecordsAsync();

            // ── 品类柱：每笔收货一个点，X 含秒精度确保唯一，Y = 该笔净重 ─
            static double ToX(DateTime t) => t.Hour + t.Minute / 60.0 + t.Second / 3600.0;
            _receivePointMap  = receiveRecords.ToDictionary(r => ToX(r.ArchivedAt), r => r);
            _deliveryPointMap = _allDeliveryRecords.ToDictionary(r => ToX(r.DeliveryTime), r => r);

            var categoryPoints = receiveRecords
                .GroupBy(r => r.GoodsName)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(r => r.ArchivedAt)
                           .Select(r => new ObservablePoint(ToX(r.ArchivedAt), r.NetWeight))
                           .ToArray());

            // ── 送货柱：每笔送货一个点，X 含秒精度，Y = 该笔送货重量 ─
            var deliveryPoints = _allDeliveryRecords
                .OrderBy(r => r.DeliveryTime)
                .Select(r => new ObservablePoint(ToX(r.DeliveryTime), r.TotalWeight))
                .ToArray();

            // ── 汇总阶梯线：收货 +，送货 −，按时间顺序逐笔累计 ─────
            double sRunning = 0;
            var summaryPoints = receiveRecords.Select(r => (Time: r.ArchivedAt, Weight: r.NetWeight, IsDelivery: false))
                .Concat(_allDeliveryRecords.Select(r => (Time: r.DeliveryTime, Weight: r.TotalWeight, IsDelivery: true)))
                .OrderBy(e => e.Time)
                .Select(e =>
                {
                    sRunning += e.IsDelivery ? -e.Weight : e.Weight;
                    return new ObservablePoint(ToX(e.Time), sRunning);
                })
                .ToArray();

            // 送货柱取负值，使其显示在 X 轴下方
            var deliveryNegPoints = deliveryPoints
                .Select(p => new ObservablePoint(p.X, p.Y.HasValue ? -p.Y.Value : (double?)null))
                .ToArray();

            HourlySeries.Clear();

            int ci = 0;
            foreach (var (goodsName, pts) in categoryPoints)
            {
                var clr = _categoryColors[ci++ % _categoryColors.Length];
                HourlySeries.Add(new ColumnSeries<ObservablePoint>
                {
                    Values = pts,
                    Name = goodsName,
                    Fill = new SolidColorPaint(clr),
                    Stroke = null,
                    MaxBarWidth = 18,
                    IgnoresBarPosition = true,
                });
            }

            if (deliveryNegPoints.Length > 0)
            {
                HourlySeries.Add(new ColumnSeries<ObservablePoint>
                {
                    Values = deliveryNegPoints,
                    Name = "送货重量",
                    Fill = new SolidColorPaint(new SKColor(0x66, 0xBB, 0x6A)),
                    Stroke = null,
                    MaxBarWidth = 18,
                    IgnoresBarPosition = true,
                });
            }

            HourlySeries.Add(new StepLineSeries<ObservablePoint>
            {
                Values = summaryPoints,
                Name = "净库存变化",
                Fill = null,
                GeometrySize = 6,
                Stroke = new SolidColorPaint(new SKColor(0xE0, 0xE0, 0xE0), 2.5f),
                GeometryFill = new SolidColorPaint(new SKColor(0xE0, 0xE0, 0xE0)),
                GeometryStroke = null,
            });

            XAxes[0].MinLimit = null;
            XAxes[0].MaxLimit = null;
            YAxes[0].MinLimit = null;
            YAxes[0].MaxLimit = null;

            _pageIndex = 1;
            RaisePropertyChanged(nameof(PageIndex));
            RaisePropertyChanged(nameof(TotalPageCount));
            UpdatePagedReceiveRecords();

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
            var to = DateTime.Today.AddDays(1).AddSeconds(-1); // 今天 23:59:59，包含当天全部数据

            var cats = !string.IsNullOrEmpty(_selectedInventoryCategory)
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

    private void OnChartPointDown(ChartPoint pt)
    {
        if (pt is null) return;
        double x = pt.Coordinate.SecondaryValue;

        RoundedRectangleGeometry? geo = pt.Context.Visual as RoundedRectangleGeometry;
        if (geo is not null)
            geo.Fill = new SolidColorPaint(new SKColor(0xFF, 0xFF, 0xFF, 0x80));

        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            System.Windows.Input.Mouse.Capture(null);

            Window? win = null;
            if (_receivePointMap.TryGetValue(x, out var rec))
                win = new ReceiveDetailWindow { DataContext = rec, Owner = Application.Current.MainWindow };
            else if (_deliveryPointMap.TryGetValue(x, out var del))
                win = new DeliveryDetailWindow { DataContext = del, Owner = Application.Current.MainWindow };

            win?.ShowDialog();

            if (geo is not null) geo.Fill = null;
            System.Windows.Input.Mouse.Capture(null);
        });
    }
}
