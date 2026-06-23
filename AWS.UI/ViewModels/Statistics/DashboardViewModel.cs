using AWS.Core.Entities;
using AWS.Core.Interfaces;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
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
    private readonly ILogService _log;
    private readonly DispatcherTimer _timer;

    // ── 统计卡片 ──────────────────────────────────────────────
    private string _todayTotalWeight = "0 kg";
    public string TodayTotalWeight { get => _todayTotalWeight; private set => SetProperty(ref _todayTotalWeight, value); }

    private string _todayVehicleCount = "0 次";
    public string TodayVehicleCount { get => _todayVehicleCount; private set => SetProperty(ref _todayVehicleCount, value); }

    private string _todayTotalAmount = "¥ --";
    public string TodayTotalAmount { get => _todayTotalAmount; private set => SetProperty(ref _todayTotalAmount, value); }

    private string _lastRefreshTime = "--:--:--";
    public string LastRefreshTime { get => _lastRefreshTime; private set => SetProperty(ref _lastRefreshTime, value); }

    // ── 折线图 ────────────────────────────────────────────────
    private readonly ObservableValue[] _hourlyValues =
        Enumerable.Range(0, 24).Select(_ => new ObservableValue(0)).ToArray();

    public ISeries[] HourlySeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    // ── 分页 ─────────────────────────────────────────────────
    private List<WeighingArchiveRecord> _allTodayRecords = [];

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

    public int TotalPageCount => Math.Max(1, (int)Math.Ceiling(_allTodayRecords.Count / (double)_pageSize));

    public ObservableCollection<WeighingArchiveRecord> TodayRecords { get; } = [];

    public DelegateCommand RefreshCommand { get; }

    public DashboardViewModel(IWeighingService weighing, IArchiveQueryService archive, ILogService log)
    {
        _weighing = weighing;
        _archive = archive;
        _log = log;

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

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public async void OnNavigatedTo(NavigationContext ctx)
    {
        await RefreshAsync();
        _timer.Start();
    }

    public void OnNavigatedFrom(NavigationContext ctx) => _timer.Stop();

    // 每次导航都创建新实例，避免旧数据残留
    public bool IsNavigationTarget(NavigationContext ctx) => false;

    private async Task RefreshAsync()
    {
        try
        {
            var hourly = await _weighing.GetTodayHourlyNetWeightAsync();
            for (int h = 0; h < 24; h++)
                _hourlyValues[h].Value = hourly[h];

            // 查今日全天（00:00:00 ~ 23:59:59），避免 DateTo=今日00:00 漏掉白天记录
            var today = DateTime.Today;
            var records = await _archive.QueryAsync(today.Year, today, today.AddDays(1).AddSeconds(-1));

            TodayTotalWeight = $"{records.Sum(r => r.NetWeight):N0} kg";
            TodayVehicleCount = $"{records.Count} 次";

            var amount = records.Sum(r => r.TotalAmount ?? 0);
            TodayTotalAmount = amount > 0 ? $"¥ {amount:N2}" : "¥ --";

            _allTodayRecords = records.OrderByDescending(r => r.ArchivedAt).ToList();
            _pageIndex = 1;
            RaisePropertyChanged(nameof(PageIndex));
            RaisePropertyChanged(nameof(TotalPageCount));
            UpdatePagedRecords();

            LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            _log.Error($"今日汇总刷新失败：{ex.Message}", "今日汇总");
        }
    }

    private void UpdatePagedRecords()
    {
        TodayRecords.Clear();
        foreach (var r in _allTodayRecords.Skip((_pageIndex - 1) * _pageSize).Take(_pageSize))
            TodayRecords.Add(r);
        RaisePropertyChanged(nameof(TotalPageCount));
    }
}
