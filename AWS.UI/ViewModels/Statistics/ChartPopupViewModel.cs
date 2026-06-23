using AWS.Core.Entities;
using AWS.UI.Charts;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Mvvm;
using SkiaSharp;
using System.Windows;

namespace AWS.UI.ViewModels.Statistics;

public class ChartPopupViewModel : BindableBase
{
    private List<WeighingArchiveRecord> _records = [];
    private bool _isDark;

    public string[] ChartTypeNames { get; } =
        ["各客户净重排名", "货物类型净重占比", "净重日趋势", "各车辆净重排名"];

    private string _windowTitle = "数据图表";
    public string WindowTitle { get => _windowTitle; private set => SetProperty(ref _windowTitle, value); }

    private int _selectedChartTypeIndex;
    public int SelectedChartTypeIndex
    {
        get => _selectedChartTypeIndex;
        set { SetProperty(ref _selectedChartTypeIndex, value); BuildChart(); }
    }

    private IEnumerable<ISeries> _cartesianSeries = [];
    public IEnumerable<ISeries> CartesianSeries
    {
        get => _cartesianSeries;
        private set => SetProperty(ref _cartesianSeries, value);
    }

    private IEnumerable<ISeries> _pieSeries = [];
    public IEnumerable<ISeries> PieSeries
    {
        get => _pieSeries;
        private set => SetProperty(ref _pieSeries, value);
    }

    private Axis[] _xAxes = [];
    public Axis[] XAxes { get => _xAxes; private set => SetProperty(ref _xAxes, value); }

    private Axis[] _yAxes = [];
    public Axis[] YAxes { get => _yAxes; private set => SetProperty(ref _yAxes, value); }

    private Visibility _cartesianVisibility = Visibility.Visible;
    public Visibility CartesianVisibility
    {
        get => _cartesianVisibility;
        private set => SetProperty(ref _cartesianVisibility, value);
    }

    private Visibility _pieVisibility = Visibility.Collapsed;
    public Visibility PieVisibility
    {
        get => _pieVisibility;
        private set => SetProperty(ref _pieVisibility, value);
    }

    // 供 XAML 绑定（替代 x:Static），随主题更新
    public SolidColorPaint TooltipTextPaint { get; private set; } = ChartPaints.ThemedTooltip(false);
    public SolidColorPaint LegendTextPaint  { get; private set; } = ChartPaints.ThemedTooltip(false);

    public void Initialize(List<WeighingArchiveRecord> records, int chartTypeIndex, bool isDark = false)
    {
        _records = records;
        _isDark  = isDark;
        TooltipTextPaint = ChartPaints.ThemedTooltip(isDark);
        LegendTextPaint  = ChartPaints.ThemedTooltip(isDark);
        RaisePropertyChanged(nameof(TooltipTextPaint));
        RaisePropertyChanged(nameof(LegendTextPaint));
        _selectedChartTypeIndex = chartTypeIndex;
        RaisePropertyChanged(nameof(SelectedChartTypeIndex));
        BuildChart();
    }

    private void BuildChart()
    {
        var isPie = SelectedChartTypeIndex == 1;
        CartesianVisibility = isPie ? Visibility.Collapsed : Visibility.Visible;
        PieVisibility       = isPie ? Visibility.Visible   : Visibility.Collapsed;

        WindowTitle = $"数据图表  —  {ChartTypeNames[SelectedChartTypeIndex]}（{_records.Count} 条记录）";

        switch (SelectedChartTypeIndex)
        {
            case 0: BuildCustomerChart();   break;
            case 1: BuildGoodsPieChart();   break;
            case 2: BuildDailyTrendChart(); break;
            case 3: BuildVehicleChart();    break;
        }
    }

    private void BuildCustomerChart()
    {
        var groups = _records
            .GroupBy(r => r.CustomerName)
            .Select(g => (Name: g.Key, Weight: Math.Round(g.Sum(r => r.NetWeight), 1)))
            .OrderByDescending(x => x.Weight)
            .ToList();

        CartesianSeries =
        [
            new RowSeries<double>
            {
                Values              = groups.Select(g => g.Weight).ToArray(),
                Name                = "净重(kg)",
                Fill                = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5)),
                DataLabelsPaint     = ChartPaints.ThemedDataLabel(_isDark),
                DataLabelsSize      = 10,
                DataLabelsPosition  = LiveChartsCore.Measure.DataLabelsPosition.End,
                MaxBarWidth         = 28,
            }
        ];
        XAxes =
        [
            new Axis
            {
                Name         = "净重 kg",
                NameTextSize = 10,
                TextSize     = 9,
                LabelsPaint  = ChartPaints.ThemedText(_isDark),
                NamePaint    = ChartPaints.ThemedText(_isDark),
            }
        ];
        YAxes =
        [
            new Axis
            {
                Labels      = groups.Select(g => g.Name).ToArray(),
                TextSize    = 10,
                LabelsPaint = ChartPaints.ThemedText(_isDark),
            }
        ];
    }

    private void BuildGoodsPieChart()
    {
        var groups = _records
            .GroupBy(r => r.GoodsName)
            .Select(g => (Name: g.Key, Weight: Math.Round(g.Sum(r => r.NetWeight), 1)))
            .OrderByDescending(x => x.Weight)
            .ToList();

        SKColor[] palette =
        [
            new(0x42, 0xA5, 0xF5), new(0x66, 0xBB, 0x6A), new(0xFF, 0xCA, 0x28),
            new(0xEF, 0x53, 0x50), new(0xAB, 0x47, 0xBC), new(0x26, 0xC6, 0xDA),
            new(0xFF, 0x70, 0x43), new(0x8D, 0x6E, 0x63),
        ];

        PieSeries = groups.Select((g, i) => (ISeries)new PieSeries<double>
        {
            Values             = [g.Weight],
            Name               = g.Name,
            Fill               = new SolidColorPaint(palette[i % palette.Length]),
            DataLabelsPaint    = ChartPaints.ThemedDataLabel(_isDark),
            DataLabelsSize     = 10,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
        }).ToArray();
    }

    private void BuildDailyTrendChart()
    {
        var groups = _records
            .GroupBy(r => r.ArchivedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => (Date: g.Key, Weight: Math.Round(g.Sum(r => r.NetWeight), 1)))
            .ToList();

        CartesianSeries =
        [
            new LineSeries<double>
            {
                Values         = groups.Select(g => g.Weight).ToArray(),
                Name           = "净重 kg",
                Fill           = null,
                GeometrySize   = 6,
                Stroke         = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5), 2),
                GeometryFill   = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5)),
                GeometryStroke = null,
            }
        ];
        XAxes =
        [
            new Axis
            {
                Labels      = groups.Select(g => g.Date.ToString("MM/dd")).ToArray(),
                TextSize    = 9,
                LabelsPaint = ChartPaints.ThemedText(_isDark),
            }
        ];
        YAxes =
        [
            new Axis
            {
                Name         = "kg",
                NameTextSize = 10,
                TextSize     = 9,
                LabelsPaint  = ChartPaints.ThemedText(_isDark),
                NamePaint    = ChartPaints.ThemedText(_isDark),
            }
        ];
    }

    private void BuildVehicleChart()
    {
        var groups = _records
            .GroupBy(r => r.VehiclePlate ?? "未知")
            .Select(g => (Name: g.Key, Weight: Math.Round(g.Sum(r => r.NetWeight), 1)))
            .OrderByDescending(x => x.Weight)
            .ToList();

        CartesianSeries =
        [
            new RowSeries<double>
            {
                Values             = groups.Select(g => g.Weight).ToArray(),
                Name               = "净重(kg)",
                Fill               = new SolidColorPaint(new SKColor(0x66, 0xBB, 0x6A)),
                DataLabelsPaint    = ChartPaints.ThemedDataLabel(_isDark),
                DataLabelsSize     = 10,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                MaxBarWidth        = 28,
            }
        ];
        XAxes =
        [
            new Axis
            {
                Name         = "净重 kg",
                NameTextSize = 10,
                TextSize     = 9,
                LabelsPaint  = ChartPaints.ThemedText(_isDark),
                NamePaint    = ChartPaints.ThemedText(_isDark),
            }
        ];
        YAxes =
        [
            new Axis
            {
                Labels      = groups.Select(g => g.Name).ToArray(),
                TextSize    = 10,
                LabelsPaint = ChartPaints.ThemedText(_isDark),
            }
        ];
    }
}
