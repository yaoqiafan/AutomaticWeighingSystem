using AWS.Core.Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System.Windows.Media;
using System.Windows.Threading;

namespace AWS.Shell.ViewModels;

public class MainWindowViewModel : BindableBase
{
    private readonly IUserService _userService;
    private readonly ISerialPortService _serialPortService;
    private readonly IRegionManager _regionManager;
    private readonly DispatcherTimer _timer;

    private string _systemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string SystemTime
    {
        get => _systemTime;
        private set => SetProperty(ref _systemTime, value);
    }

    private string _currentUserDisplay = string.Empty;
    public string CurrentUserDisplay
    {
        get => _currentUserDisplay;
        private set => SetProperty(ref _currentUserDisplay, value);
    }

    public bool IsAdmin => _userService.IsAdmin;

    private Brush _serialPortStatusBrush = Brushes.Gray;
    public Brush SerialPortStatusBrush
    {
        get => _serialPortStatusBrush;
        private set => SetProperty(ref _serialPortStatusBrush, value);
    }

    private string _serialPortStatusText = "串口未连接";
    public string SerialPortStatusText
    {
        get => _serialPortStatusText;
        private set => SetProperty(ref _serialPortStatusText, value);
    }

    private object? _selectedMenuItem;
    public object? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set => SetProperty(ref _selectedMenuItem, value);
    }

    public DelegateCommand LoadedCommand { get; }
    public DelegateCommand ClosingCommand { get; }
    public DelegateCommand ToggleThemeCommand { get; }
    public DelegateCommand<object> NavigateCommand { get; }

    public MainWindowViewModel(IUserService userService,
        ISerialPortService serialPortService,
        IRegionManager regionManager)
    {
        _userService = userService;
        _serialPortService = serialPortService;
        _regionManager = regionManager;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            SystemTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            UpdateSerialPortStatus();
        };

        _serialPortService.WeightReceived += (_, _) => UpdateSerialPortStatus();

        LoadedCommand = new DelegateCommand(OnLoaded);
        ClosingCommand = new DelegateCommand(OnClosing);
        ToggleThemeCommand = new DelegateCommand(OnToggleTheme);
        NavigateCommand = new DelegateCommand<object>(OnNavigate);
    }

    private void OnLoaded()
    {
        _timer.Start();
        UpdateUserDisplay();
        UpdateSerialPortStatus();
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
            // 读当前皮肤，切换到另一种
            var current = app.Resources.MergedDictionaries[0]
                .MergedDictionaries.FirstOrDefault()?.Source?.ToString() ?? "";
            var newSkin = current.Contains("Dark") ? "Light" : "Dark";
            app.UpdateSkin(newSkin);
        }
    }

    private void OnNavigate(object args)
    {
        string? target = null;
        if (args is System.Windows.Controls.SelectionChangedEventArgs e
            && e.AddedItems.Count > 0
            && e.AddedItems[0] is PF.UI.Controls.SideMenuItem item)
        {
            target = item.Tag as string;
        }
        if (!string.IsNullOrEmpty(target))
            _regionManager.RequestNavigate(RegionNames.Main, target);
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

    private void UpdateSerialPortStatus()
    {
        if (_serialPortService.IsSimulationMode)
        {
            SerialPortStatusText = "模拟数据运行中";
            SerialPortStatusBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
        }
        else if (_serialPortService.IsConnected)
        {
            SerialPortStatusText = "串口已连接";
            SerialPortStatusBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }
        else
        {
            SerialPortStatusText = "串口未连接";
            SerialPortStatusBrush = Brushes.Gray;
        }
    }
}
