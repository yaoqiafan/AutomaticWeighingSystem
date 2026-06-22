using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace AWS.UI.ViewModels.Weighing;

public class WeighingViewModel : BindableBase, INavigationAware
{
    private readonly ISerialPortService _serial;
    private readonly IWeighingService _weighingService;
    private readonly IUserService _userService;
    private readonly AwsDbContext _db;
    private readonly ILogService _log;
    private readonly DispatcherTimer _statsTimer;
    private readonly Dispatcher _dispatcher;

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

    // ── 单价快捷修改（Admin） ───────────────────────────────
    private double _currentPricePerKg;
    public double CurrentPricePerKg
    {
        get => _currentPricePerKg;
        private set => SetProperty(ref _currentPricePerKg, value);
    }

    private bool _isPriceEditing;
    public bool IsPriceEditing
    {
        get => _isPriceEditing;
        set => SetProperty(ref _isPriceEditing, value);
    }

    private string _priceEditText = "0";
    public string PriceEditText
    {
        get => _priceEditText;
        set => SetProperty(ref _priceEditText, value);
    }

    public bool IsAdmin => _userService.IsAdmin;

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
        set { SetProperty(ref _selectedCategory, value); ConfirmEntryCommand.RaiseCanExecuteChanged(); }
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

    // ── LiveCharts 折线图（今日每小时净重） ──────────────────
    private readonly ObservableValue[] _hourlyValues =
        Enumerable.Range(0, 24).Select(_ => new ObservableValue(0)).ToArray();

    public ISeries[] HourlySeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    // ── 对话框回调（由 View code-behind 赋值） ───────────────
    public Func<WeighingQueue, double, Task<bool>>? OpenSecondWeighDialog { get; set; }
    public Func<WeighingQueue, Task<bool>>? OpenEditQueueDialog { get; set; }

    internal ISerialPortService SerialPortService => _serial;
    internal ILogService LogService => _log;

    internal async Task ArchiveItemAsync(long queueId, double secondWeight, double? price)
        => await _weighingService.ArchiveAsync(queueId, secondWeight, price);

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
    public DelegateCommand StartPriceEditCommand { get; }
    public DelegateCommand SavePriceCommand { get; }
    public DelegateCommand CancelPriceEditCommand { get; }
    public DelegateCommand<WeighingQueue> CardClickCommand { get; }
    public DelegateCommand<WeighingQueue> DeleteQueueItemCommand { get; }
    public DelegateCommand<WeighingQueue> EditQueueCommand { get; }

    public WeighingViewModel(
        ISerialPortService serial,
        IWeighingService weighingService,
        IUserService userService,
        AwsDbContext db,
        ILogService log)
    {
        _serial = serial;
        _weighingService = weighingService;
        _userService = userService;
        _db = db;
        _log = log;
        _dispatcher = Application.Current.Dispatcher;

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
            new Axis
            {
                Labels = Enumerable.Range(0, 24).Select(h => $"{h}时").ToArray(),
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
        StartPriceEditCommand = new DelegateCommand(
            () => { PriceEditText = _currentPricePerKg.ToString("F2"); IsPriceEditing = true; },
            () => IsAdmin);
        SavePriceCommand = new DelegateCommand(OnSavePrice);
        CancelPriceEditCommand = new DelegateCommand(() => IsPriceEditing = false);
        CardClickCommand = new DelegateCommand<WeighingQueue>(OnCardClick);
        DeleteQueueItemCommand = new DelegateCommand<WeighingQueue>(async item => await OnDeleteQueueItemAsync(item));
        EditQueueCommand = new DelegateCommand<WeighingQueue>(async item => await OnEditQueueAsync(item));

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _statsTimer.Tick += async (_, _) => await RefreshStatsAsync();
    }

    public async void OnNavigatedTo(NavigationContext ctx)
    {
        _serial.WeightReceived -= OnWeightReceived;
        _serial.WeightReceived += OnWeightReceived;
        RaisePropertyChanged(nameof(IsSimulationMode));
        RaisePropertyChanged(nameof(IsAdmin));
        StartPriceEditCommand.RaiseCanExecuteChanged();
        _statsTimer.Start();
        await LoadInitialDataAsync();
    }

    public void OnNavigatedFrom(NavigationContext ctx)
    {
        _serial.WeightReceived -= OnWeightReceived;
        _statsTimer.Stop();
    }

    public bool IsNavigationTarget(NavigationContext ctx) => true;

    private async Task LoadInitialDataAsync()
    {
        var cats = await _db.GoodsCategories.Where(c => c.IsActive).ToListAsync();
        _dispatcher.Invoke(() =>
        {
            GoodsCategories.Clear();
            foreach (var c in cats) GoodsCategories.Add(c);
        });

        var priceStr = _db.SystemSettings.Find(SettingKeys.DefaultPricePerKg)?.Value ?? "0";
        CurrentPricePerKg = double.TryParse(priceStr, out double p) ? p : 0;

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
        var hourly = await _weighingService.GetTodayHourlyNetWeightAsync();
        var (total, count) = await _weighingService.GetTodayStatsAsync();
        _dispatcher.Invoke(() =>
        {
            for (int h = 0; h < 24; h++) _hourlyValues[h].Value = hourly[h];
            TodayTotalWeight = $"{total:N0} kg";
            TodayVehicleCount = count;
        });
    }

    private void OnWeightReceived(object? sender, Core.Models.WeightReading reading)
    {
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

            await _weighingService.CreateInitialEntryAsync(
                vehiclePlate: VehiclePlate,
                customerName: CustomerName,
                customerId: null,
                goodsName: _selectedCategory.Name,
                goodsCategoryId: _selectedCategory.Id,
                firstWeight: _capturedWeight.Value,
                operatorId: user.Id,
                operatorName: user.Username,
                remark: string.IsNullOrWhiteSpace(Remark) ? null : Remark
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

    private void OnSavePrice()
    {
        if (!double.TryParse(PriceEditText, out double newPrice) || newPrice < 0)
        {
            _log.Warn($"单价无效，已忽略：{PriceEditText}", "过磅");
            return;
        }
        var setting = _db.SystemSettings.Find(SettingKeys.DefaultPricePerKg);
        if (setting != null)
        {
            setting.Value = newPrice.ToString("F4");
            _db.SaveChanges();
        }
        CurrentPricePerKg = newPrice;
        IsPriceEditing = false;
        _log.Info($"默认单价已更新为 {newPrice:F2} 元/kg", "过磅");
    }

    private async void OnCardClick(WeighingQueue item)
    {
        if (OpenSecondWeighDialog == null) return;
        bool archived = await OpenSecondWeighDialog(item, CurrentPricePerKg);
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
}
