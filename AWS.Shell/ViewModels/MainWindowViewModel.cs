using AWS.Core.Entities;
using AWS.Core.Enums;
using AWS.Core.Interfaces;
using AWS.Core.Models;
using AWS.Data;
using AWS.Shell.Controls;
using PF.UI.Shared.Data;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;

namespace AWS.Shell.ViewModels;

public class DeviceWeightStatus : BindableBase
{
    public string PortName { get; init; } = "";
    public WeighMode Mode { get; init; }
    public bool IsSimulation { get; init; }

    public string ModeLabel
    {
        get
        {
            var mode = Mode switch
            {
                WeighMode.FirstWeigh  => "首重",
                WeighMode.SecondWeigh => "二次称重",
                _                     => "两者"
            };
            return IsSimulation ? $"{mode},模拟" : mode;
        }
    }

    private double _value;
    public double Value
    {
        get => _value;
        set { SetProperty(ref _value, value); RaisePropertyChanged(nameof(WeightText)); }
    }

    private bool _isStable;
    public bool IsStable
    {
        get => _isStable;
        set { SetProperty(ref _isStable, value); RaisePropertyChanged(nameof(StableBrush)); }
    }

    public string WeightText => $"{Value:N1} kg";

    public Brush StableBrush => IsStable
        ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
        : Brushes.Gray;
}

public class MainWindowViewModel : BindableBase
{
    private readonly IUserService _userService;
    private readonly ISerialPortService _serialPortService;
    private readonly ICameraService _cameraService;
    private readonly IRegionManager _regionManager;
    private readonly ILogService _logService;
    private readonly AwsDbContext _db;
    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;

    private string _systemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string SystemTime { get => _systemTime; private set => SetProperty(ref _systemTime, value); }

    private string _currentUserDisplay = string.Empty;
    public string CurrentUserDisplay { get => _currentUserDisplay; private set => SetProperty(ref _currentUserDisplay, value); }

    public bool IsAdmin => _userService.IsAdmin;

    private Brush _serialPortStatusBrush = Brushes.Gray;
    public Brush SerialPortStatusBrush { get => _serialPortStatusBrush; private set => SetProperty(ref _serialPortStatusBrush, value); }

    private string _serialPortStatusText = "串口未连接";
    public string SerialPortStatusText { get => _serialPortStatusText; private set => SetProperty(ref _serialPortStatusText, value); }

    private Brush _cameraStatusBrush = Brushes.Gray;
    public Brush CameraStatusBrush { get => _cameraStatusBrush; private set => SetProperty(ref _cameraStatusBrush, value); }

    private string _cameraStatusText = "摄像机未连接";
    public string CameraStatusText { get => _cameraStatusText; private set => SetProperty(ref _cameraStatusText, value); }

    private object? _selectedMenuItem;
    public object? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            if (SetProperty(ref _selectedMenuItem, value) && value is SidebarItem item
                && item.Tag is string target && !string.IsNullOrEmpty(target))
                _regionManager.RequestNavigate(RegionNames.Main, target);
        }
    }

    public ObservableCollection<LogEntry> LogEntries => _logService.Entries;

    private bool _isLogPanelOpen = true;
    public bool IsLogPanelOpen { get => _isLogPanelOpen; set => SetProperty(ref _isLogPanelOpen, value); }

    public ObservableCollection<DeviceWeightStatus> DeviceWeights { get; } = [];

    public DelegateCommand LoadedCommand { get; }
    public DelegateCommand ClosingCommand { get; }
    public DelegateCommand ToggleThemeCommand { get; }
    public DelegateCommand ToggleLogPanelCommand { get; }
    public DelegateCommand ClearLogCommand { get; }
    public DelegateCommand ReloadSerialPortCommand { get; }

    public MainWindowViewModel(IUserService userService,
        ISerialPortService serialPortService,
        ICameraService cameraService,
        IRegionManager regionManager,
        ILogService logService,
        AwsDbContext db)
    {
        _userService = userService;
        _serialPortService = serialPortService;
        _cameraService = cameraService;
        _regionManager = regionManager;
        _logService = logService;
        _db = db;
        _dispatcher = System.Windows.Application.Current.Dispatcher;

        _cameraService.LoginStatusChanged += (_, _) =>
            _dispatcher.BeginInvoke(UpdateCameraStatus);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            SystemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            UpdateSerialPortStatus();
        };

        _serialPortService.WeightReceived += OnWeightReceived;

        LoadedCommand = new DelegateCommand(OnLoaded);
        ClosingCommand = new DelegateCommand(OnClosing);
        ToggleThemeCommand = new DelegateCommand(OnToggleTheme);
        ToggleLogPanelCommand = new DelegateCommand(() => IsLogPanelOpen = !IsLogPanelOpen);
        ClearLogCommand = new DelegateCommand(_logService.Clear);
        ReloadSerialPortCommand = new DelegateCommand(ReloadSerialPort);
    }

    private void OnLoaded()
    {
        _timer.Start();
        UpdateUserDisplay();
        UpdateSerialPortStatus();
        UpdateCameraStatus();
    }

    private void OnClosing()
    {
        _timer.Stop();
        _serialPortService.Disconnect();
    }

    private void OnToggleTheme()
    {
        if (System.Windows.Application.Current is App app)
        {
            var source = app.Resources.MergedDictionaries[0]
                .MergedDictionaries.FirstOrDefault()?.Source?.ToString() ?? string.Empty;
            var newSkin = source.Contains(nameof(SkinType.Dark), StringComparison.OrdinalIgnoreCase)
                ? SkinType.Default : SkinType.Dark;
            app.UpdateSkin(newSkin);
        }
    }

    private void OnWeightReceived(object? sender, WeightReading reading)
    {
        _dispatcher.BeginInvoke(() =>
        {
            UpdateSerialPortStatus();

            var existing = DeviceWeights.FirstOrDefault(d => d.PortName == reading.PortName);
            if (existing != null)
            {
                existing.Value = reading.Value;
                existing.IsStable = reading.IsStable;
            }
            else
            {
                DeviceWeights.Add(new DeviceWeightStatus
                {
                    PortName = reading.PortName,
                    Mode = reading.Source,
                    IsSimulation = reading.IsSimulation,
                    Value = reading.Value,
                    IsStable = reading.IsStable
                });
            }
        });
    }

    private void ReloadSerialPort()
    {
        var configJson = _db.SystemSettings.Find(SettingKeys.SerialPortConfigs)?.Value ?? "[]";
        List<SerialPortConfig> allConfigs;
        try { allConfigs = JsonSerializer.Deserialize<List<SerialPortConfig>>(configJson) ?? []; }
        catch { allConfigs = []; }

        var activeConfigs = allConfigs.Where(c => c.IsEnabled).ToList();
        DeviceWeights.Clear();

        if (activeConfigs.Count == 0)
        {
            _serialPortService.StartSimulation();
            _logService.Info("串口热重载：无已启用设备，切换至模拟模式", "串口");
        }
        else
        {
            try
            {
                _serialPortService.ConnectAll(activeConfigs);
                _logService.Info($"串口热重载完成，已连接：{string.Join(", ", activeConfigs.Select(c => c.PortName))}", "串口");
            }
            catch (Exception ex)
            {
                _logService.Error($"串口热重载失败：{ex.Message}", "串口");
            }
        }

        UpdateSerialPortStatus();
    }

    private void UpdateUserDisplay()
    {
        var user = _userService.CurrentUser;
        CurrentUserDisplay = user == null ? "" :
            $"{user.Username} ({user.Role switch {
                Core.Enums.UserRole.SuperUser => "超级用户",
                Core.Enums.UserRole.Admin     => "管理员",
                _                             => "操作员"
            }})";
        RaisePropertyChanged(nameof(IsAdmin));
    }

    private void UpdateCameraStatus()
    {
        if (_cameraService.IsLoggedIn)
        {
            CameraStatusText  = "摄像机已连接";
            CameraStatusBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }
        else
        {
            CameraStatusText  = "摄像机未连接";
            CameraStatusBrush = Brushes.Gray;
        }
    }

    private void UpdateSerialPortStatus()
    {
        if (_serialPortService.IsSimulationMode)
        {
            SerialPortStatusText = "模拟数据运行中";
            SerialPortStatusBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
        }
        else if (_serialPortService.IsConnected)
        {
            var count = _serialPortService.ConnectedDevices.Count;
            SerialPortStatusText = $"已连接 {count} 个设备";
            SerialPortStatusBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }
        else
        {
            SerialPortStatusText = "串口未连接";
            SerialPortStatusBrush = Brushes.Gray;
        }
    }
}
