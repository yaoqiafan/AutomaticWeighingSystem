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
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace AWS.UI.ViewModels.Weighing;

public class WeighingViewModel : BindableBase, INavigationAware
{
    private readonly ISerialPortService _serial;
    private readonly IWeighingService _weighingService;
    private readonly IArchiveQueryService _archive;
    private readonly IDeliveryService _delivery;
    private readonly IUserService _userService;
    private readonly AwsDbContext _db;
    private readonly ILogService _log;
    private readonly ICameraService _camera;
    private readonly IImageStorageService _imageStorage;
    private readonly DispatcherTimer _statsTimer;
    private readonly Dispatcher _dispatcher;

    private IntPtr _previewHwnd = IntPtr.Zero;
    private bool _isNavigatedTo;

    private Dictionary<double, WeighingArchiveRecord> _receivePointMap = [];
    private Dictionary<double, DeliveryRecord> _deliveryPointMap = [];

    private bool _isCameraConnected;
    public bool IsCameraConnected
    {
        get => _isCameraConnected;
        private set => SetProperty(ref _isCameraConnected, value);
    }

    private static readonly SKColor[] _categoryColors =
    [
        new(0x42, 0xA5, 0xF5),
        new(0xFF, 0xB7, 0x4D),
        new(0xAB, 0x47, 0xBC),
        new(0x26, 0xC6, 0xDA),
        new(0xEF, 0x53, 0x50),
        new(0xFF, 0xEE, 0x58),
        new(0xEC, 0x40, 0x7A),
        new(0x9C, 0xCC, 0x65),
    ];

    // ── 实时重量 ──────────────────────────────────────────
    private double _currentWeight;
    public double CurrentWeight
    {
        get => _currentWeight;
        private set => SetProperty(ref _currentWeight, value);
    }

    private bool _isStable;
    public bool IsStable
    {
        get => _isStable;
        private set { SetProperty(ref _isStable, value); CaptureWeightCommand.RaiseCanExecuteChanged(); }
    }

    public bool IsSimulationMode => _serial.IsSimulationMode;

    // ── 品类单价 ──────────────────────────────────────────
    public bool IsAdmin => _userService.IsAdmin;
    public bool IsNotAdmin => !_userService.IsAdmin;

    // 入场表单中货物类别选定后显示的参考单价（只读提示）
    private string _entryPriceHint = string.Empty;
    public string EntryPriceHint
    {
        get => _entryPriceHint;
        private set => SetProperty(ref _entryPriceHint, value);
    }

    // ── 入场表单 ────────────────────────────────────────────
    private string _vehiclePlate = string.Empty;
    public string VehiclePlate
    {
        get => _vehiclePlate;
        set => SetProperty(ref _vehiclePlate, value.ToUpperInvariant());
    }

    private string _customerName = string.Empty;
    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    private GoodsCategory? _selectedCategory;
    public GoodsCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            SetProperty(ref _selectedCategory, value);
            ConfirmEntryCommand.RaiseCanExecuteChanged();
            EntryPriceHint = value?.PricePerUnit is double p && p > 0
                ? $"{p:F2} 元/kg"
                : "（未设置）";
        }
    }

    private string _remark = string.Empty;
    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value);
    }

    private double? _capturedWeight;
    public double? CapturedWeight
    {
        get => _capturedWeight;
        private set
        {
            SetProperty(ref _capturedWeight, value);
            RaisePropertyChanged(nameof(HasCapturedWeight));
            ConfirmEntryCommand.RaiseCanExecuteChanged();
        }
    }
    public bool HasCapturedWeight => _capturedWeight.HasValue;

    public ObservableCollection<GoodsCategory> GoodsCategories { get; } = [];

    // ── 等待队列 ────────────────────────────────────────────
    public ObservableCollection<WeighingQueue> QueueItems { get; } = [];

    // ── 今日统计 ────────────────────────────────────────────
    private string _todayTotalWeight = "0 kg";
    public string TodayTotalWeight
    {
        get => _todayTotalWeight;
        private set => SetProperty(ref _todayTotalWeight, value);
    }

    private int _todayVehicleCount;
    public int TodayVehicleCount
    {
        get => _todayVehicleCount;
        private set => SetProperty(ref _todayVehicleCount, value);
    }

    // ── LiveCharts 折线图（今日每小时净重，按品类动态） ───────
    public ObservableCollection<ISeries> HourlySeries { get; } = [];
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    // ── 对话框回调（由 View code-behind 赋值） ───────────────
    public Func<WeighingQueue, double, Task<bool>>? OpenSecondWeighDialog { get; set; }
    public Func<WeighingQueue, Task<bool>>? OpenEditQueueDialog { get; set; }

    internal ISerialPortService SerialPortService => _serial;
    internal ILogService LogService => _log;
    internal ICameraService CameraService => _camera;
    internal IImageStorageService ImageStorage => _imageStorage;
    internal int DefaultCaptureChannel { get; private set; } = 1;

    internal async Task ArchiveItemAsync(long queueId, double secondWeight, double? price,
        string? secondWeighImagePath = null)
        => await _weighingService.ArchiveAsync(queueId, secondWeight, price, secondWeighImagePath);

    internal async Task<bool> UpdateQueueAsync(long id, string vehiclePlate,
        string customerName, string goodsName, string? remark, double firstWeight)
    {
        try
        {
            await _weighingService.UpdateQueueAsync(id, vehiclePlate, customerName, goodsName, remark, firstWeight);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"修改磅单失败：{ex.Message}", "过磅");
            return false;
        }
    }

    // ── Commands ────────────────────────────────────────────
    public DelegateCommand CaptureWeightCommand { get; }
    public DelegateCommand ConfirmEntryCommand { get; }
    public DelegateCommand SaveCategoryPricesCommand { get; }
    public DelegateCommand<WeighingQueue> CardClickCommand { get; }
    public DelegateCommand<WeighingQueue> DeleteQueueItemCommand { get; }
    public DelegateCommand<WeighingQueue> EditQueueCommand { get; }
    public DelegateCommand<ChartPoint> ChartPointDownCommand { get; }

    public WeighingViewModel(
        ISerialPortService serial,
        IWeighingService weighingService,
        IArchiveQueryService archive,
        IDeliveryService delivery,
        IUserService userService,
        AwsDbContext db,
        ILogService log,
        ICameraService camera,
        IImageStorageService imageStorage)
    {
        _serial = serial;
        _weighingService = weighingService;
        _archive = archive;
        _delivery = delivery;
        _userService = userService;
        _db = db;
        _log = log;
        _camera = camera;
        _imageStorage = imageStorage;
        _dispatcher = Application.Current.Dispatcher;

        _camera.LoginStatusChanged += (_, _) =>
        {
            _dispatcher.BeginInvoke(() =>
            {
                IsCameraConnected = _camera.IsLoggedIn;
                TryStartPreview();
            });
        };

        XAxes =
        [
            new Axis
            {
                Labeler = v => $"{(int)v}时",
                MinStep = 1,
                MinLimit = 0,
                MaxLimit = 23,
                TextSize = 9,
                LabelsPaint = AWS.UI.Charts.ChartPaints.Text(),
            }
        ];

        YAxes =
        [
            new Axis
            {
                Name = "kg",
                NameTextSize = 10,
                TextSize = 9,
                LabelsPaint = AWS.UI.Charts.ChartPaints.Text(),
                NamePaint = AWS.UI.Charts.ChartPaints.Text(),
            }
        ];

        CaptureWeightCommand = new DelegateCommand(OnCaptureWeight, () => _isStable);
        ConfirmEntryCommand = new DelegateCommand(OnConfirmEntry,
            () => HasCapturedWeight && _selectedCategory != null);
        SaveCategoryPricesCommand = new DelegateCommand(async () => await OnSaveCategoryPricesAsync(), () => IsAdmin);
        CardClickCommand = new DelegateCommand<WeighingQueue>(OnCardClick);
        DeleteQueueItemCommand = new DelegateCommand<WeighingQueue>(async item => await OnDeleteQueueItemAsync(item));
        EditQueueCommand = new DelegateCommand<WeighingQueue>(async item => await OnEditQueueAsync(item));
        ChartPointDownCommand = new DelegateCommand<ChartPoint>(OnChartPointDown);

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _statsTimer.Tick += async (_, _) => await RefreshStatsAsync();
    }

    public async void OnNavigatedTo(NavigationContext ctx)
    {
        _serial.WeightReceived -= OnWeightReceived;
        _serial.WeightReceived += OnWeightReceived;
        RaisePropertyChanged(nameof(IsSimulationMode));
        RaisePropertyChanged(nameof(IsAdmin));
        RaisePropertyChanged(nameof(IsNotAdmin));
        SaveCategoryPricesCommand.RaiseCanExecuteChanged();

        var chStr = _db.SystemSettings.Find(SettingKeys.CaptureChannel)?.Value;
        if (int.TryParse(chStr, out var ch)) DefaultCaptureChannel = ch;
        IsCameraConnected = _camera.IsLoggedIn;

        _isNavigatedTo = true;
        TryStartPreview();

        _statsTimer.Start();
        await LoadInitialDataAsync();
    }

    public void OnNavigatedFrom(NavigationContext ctx)
    {
        _isNavigatedTo = false;
        _camera.StopPreview();
        _serial.WeightReceived -= OnWeightReceived;
        _statsTimer.Stop();
    }

    public void SetPreviewHandle(IntPtr hwnd)
    {
        _previewHwnd = hwnd;
        TryStartPreview();
    }

    public void ClearPreviewHandle()
    {
        _previewHwnd = IntPtr.Zero;
        _camera.StopPreview();
    }

    private void TryStartPreview()
    {
        if (_isNavigatedTo && _previewHwnd != IntPtr.Zero && _camera.IsLoggedIn)
            _camera.StartPreview(DefaultCaptureChannel, _previewHwnd);
    }

    public bool IsNavigationTarget(NavigationContext ctx) => false;

    private async Task LoadInitialDataAsync()
    {
        var cats = await _db.GoodsCategories.Where(c => c.IsActive).ToListAsync();
        _dispatcher.Invoke(() =>
        {
            GoodsCategories.Clear();
            foreach (var c in cats) GoodsCategories.Add(c);
        });

        await RefreshQueueAsync();
        await RefreshStatsAsync();
    }

    public async Task RefreshQueueAsync()
    {
        var items = await _weighingService.GetActiveQueueAsync();
        _dispatcher.Invoke(() =>
        {
            QueueItems.Clear();
            foreach (var i in items) QueueItems.Add(i);
        });
    }

    private async Task RefreshStatsAsync()
    {
        var today = DateTime.Today;
        var records         = await _archive.QueryAsync(today.Year, today, today.AddDays(1).AddSeconds(-1));
        var deliveryRecords = await _delivery.GetTodayRecordsAsync();
        var (total, count, _) = await _weighingService.GetTodayStatsAsync();

        // 品类柱：每笔收货一个点，X 含秒精度确保唯一，Y = 该笔净重
        static double ToX(DateTime t) => t.Hour + t.Minute / 60.0 + t.Second / 3600.0;

        var newReceiveMap  = records.ToDictionary(r => ToX(r.ArchivedAt), r => r);
        var newDeliveryMap = deliveryRecords.ToDictionary(r => ToX(r.DeliveryTime), r => r);

        var categoryPoints = records
            .GroupBy(r => r.GoodsName)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.ArchivedAt)
                       .Select(r => new ObservablePoint(ToX(r.ArchivedAt), r.NetWeight))
                       .ToArray());

        // 送货柱：每笔送货一个点，X 含秒精度，Y = 该笔送货重量
        var deliveryPoints = deliveryRecords
            .OrderBy(r => r.DeliveryTime)
            .Select(r => new ObservablePoint(ToX(r.DeliveryTime), r.TotalWeight))
            .ToArray();

        // 汇总阶梯线：收货 +，送货 −，按时间顺序逐笔累计
        double sRunning = 0;
        var summaryPoints = records.Select(r => (Time: r.ArchivedAt, Weight: r.NetWeight, IsDelivery: false))
            .Concat(deliveryRecords.Select(r => (Time: r.DeliveryTime, Weight: r.TotalWeight, IsDelivery: true)))
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

        _dispatcher.Invoke(() =>
        {
            _receivePointMap  = newReceiveMap;
            _deliveryPointMap = newDeliveryMap;
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

            TodayTotalWeight = $"{total:N0} kg";
            TodayVehicleCount = count;
        });
    }

    private void OnWeightReceived(object? sender, Core.Models.WeightReading reading)
    {
        // 仅响应首重设备或两者通用设备；二次称重专用设备的数据不在此面板显示
        if (reading.Source == Core.Enums.WeighMode.SecondWeigh) return;

        _dispatcher.Invoke(() =>
        {
            CurrentWeight = reading.Value;
            IsStable = reading.IsStable;
        });
    }

    private void OnCaptureWeight() => CapturedWeight = CurrentWeight;

    private async void OnConfirmEntry()
    {
        if (!_capturedWeight.HasValue || _selectedCategory == null) return;
        var user = _userService.CurrentUser;
        if (user == null) return;

        try
        {
            var logCategory = _selectedCategory.Name;
            var logWeight = _capturedWeight.Value;

            // 预生成磅单号以便确定图片路径
            var ticketNo = await _weighingService.GenerateTicketNoAsync();
            string? firstImagePath = null;
            if (_camera.IsLoggedIn)
            {
                var path = _imageStorage.BuildPath(ticketNo, "first");
                firstImagePath = await _camera.CaptureJpegAsync(DefaultCaptureChannel, path);
            }

            await _weighingService.CreateInitialEntryAsync(
                vehiclePlate: VehiclePlate,
                customerName: CustomerName,
                customerId: null,
                goodsName: _selectedCategory.Name,
                goodsCategoryId: _selectedCategory.Id,
                firstWeight: _capturedWeight.Value,
                operatorId: user.Id,
                operatorName: user.Username,
                remark: string.IsNullOrWhiteSpace(Remark) ? null : Remark,
                ticketNo: ticketNo,
                firstWeighImagePath: firstImagePath
            );

            VehiclePlate = string.Empty;
            CustomerName = string.Empty;
            SelectedCategory = null;
            Remark = string.Empty;
            CapturedWeight = null;

            await RefreshQueueAsync();
            _log.Info($"入场登记成功：{logCategory} {logWeight:F1}kg", "过磅");
        }
        catch (Exception ex)
        {
            _log.Error($"入场失败：{ex.Message}", "过磅");
        }
    }

    private async Task OnSaveCategoryPricesAsync()
    {
        try
        {
            // GoodsCategories 实体由 DbContext 跟踪，直接保存即可
            await _db.SaveChangesAsync();
            _log.Info("品类单价已保存", "过磅");
        }
        catch (Exception ex)
        {
            _log.Error($"保存单价失败：{ex.Message}", "过磅");
        }
    }

    private async void OnCardClick(WeighingQueue item)
    {
        if (OpenSecondWeighDialog == null) return;
        // 以当前品类的 PricePerUnit 作为二次称重弹窗的默认单价
        var price = GoodsCategories.FirstOrDefault(c => c.Id == item.GoodsCategoryId)?.PricePerUnit ?? 0;
        bool archived = await OpenSecondWeighDialog(item, price);
        if (archived)
        {
            await RefreshQueueAsync();
            await RefreshStatsAsync();
        }
    }

    private async Task OnDeleteQueueItemAsync(WeighingQueue? item)
    {
        if (item == null) return;
        var confirm = System.Windows.MessageBox.Show(
            $"确认删除磅单 {item.TicketNo}？", "确认删除",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            await _weighingService.DeleteQueueItemAsync(item.Id);
            _log.Warn($"已删除磅单：{item.TicketNo}", "过磅");
            await RefreshQueueAsync();
        }
        catch (Exception ex)
        {
            _log.Error($"删除磅单失败：{ex.Message}", "过磅");
        }
    }

    private async Task OnEditQueueAsync(WeighingQueue? item)
    {
        if (item == null || OpenEditQueueDialog == null) return;
        bool updated = await OpenEditQueueDialog(item);
        if (updated) await RefreshQueueAsync();
    }

    private void OnChartPointDown(ChartPoint pt)
    {
        if (pt is null) return;
        double x = pt.Coordinate.SecondaryValue;

        // 高亮被点击的柱（立即生效，对话框打开前可见）
        RoundedRectangleGeometry? geo = pt.Context.Visual as RoundedRectangleGeometry;
        if (geo is not null)
            geo.Fill = new SolidColorPaint(new SKColor(0xFF, 0xFF, 0xFF, 0x80));

        // 延迟到 Background 优先级，确保 Mouse Up 先被 Chart 处理，避免拖动残留
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            System.Windows.Input.Mouse.Capture(null);   // 释放 Chart 可能已捕获的鼠标

            Window? win = null;
            if (_receivePointMap.TryGetValue(x, out var rec))
                win = new ReceiveDetailWindow { DataContext = rec, Owner = Application.Current.MainWindow };
            else if (_deliveryPointMap.TryGetValue(x, out var del))
                win = new DeliveryDetailWindow { DataContext = del, Owner = Application.Current.MainWindow };

            win?.ShowDialog();

            // 对话框关闭后：取消高亮 + 确保鼠标释放
            if (geo is not null) geo.Fill = null;
            System.Windows.Input.Mouse.Capture(null);
        });
    }
}
