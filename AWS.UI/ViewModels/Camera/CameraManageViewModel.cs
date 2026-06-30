using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Core.Models;
using AWS.Data;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;

namespace AWS.UI.ViewModels.Camera;

public class CameraManageViewModel : BindableBase, INavigationAware
{
    private readonly ICameraService _camera;
    private readonly AwsDbContext _db;
    private readonly ILogService _log;
    private readonly Dispatcher _dispatcher;

    private IntPtr _previewHwnd = IntPtr.Zero;
    private bool _isNavigatedTo;

    // ── 连接参数 ──────────────────────────────────────────────
    private string _ip   = string.Empty;
    private int    _port = 8000;
    private string _user = "admin";
    private string _pwd  = string.Empty;

    public string Ip   { get => _ip;   set => SetProperty(ref _ip, value); }
    public int    Port { get => _port; set => SetProperty(ref _port, value); }
    public string User { get => _user; set => SetProperty(ref _user, value); }
    public string Pwd  { get => _pwd;  set => SetProperty(ref _pwd, value); }

    // ── 状态 ─────────────────────────────────────────────────
    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set { SetProperty(ref _isConnected, value); RaisePropertyChanged(nameof(StatusText)); }
    }
    public string StatusText => IsConnected ? $"已连接 {Ip}:{Port}" : "未连接";

    // ── 通道列表 ──────────────────────────────────────────────
    public ObservableCollection<ChannelInfo> Channels { get; } = [];

    private ChannelInfo? _selectedChannel;
    public ChannelInfo? SelectedChannel { get => _selectedChannel; set => SetProperty(ref _selectedChannel, value); }

    // ── Commands ──────────────────────────────────────────────
    public DelegateCommand ConnectCommand    { get; }
    public DelegateCommand DisconnectCommand { get; }
    public DelegateCommand StartPreviewCommand { get; }
    public DelegateCommand StopPreviewCommand  { get; }
    public DelegateCommand CaptureCommand      { get; }

    public CameraManageViewModel(ICameraService camera, AwsDbContext db, ILogService log)
    {
        _camera = camera;
        _db = db;
        _log = log;
        _dispatcher = Dispatcher.CurrentDispatcher;

        ConnectCommand    = new DelegateCommand(OnConnect);
        DisconnectCommand = new DelegateCommand(OnDisconnect);
        StartPreviewCommand = new DelegateCommand(OnStartPreview);
        StopPreviewCommand  = new DelegateCommand(_camera.StopPreview);
        CaptureCommand      = new DelegateCommand(async () => await OnCaptureAsync());
    }

    // ── INavigationAware ──────────────────────────────────────
    public void OnNavigatedTo(NavigationContext ctx)
    {
        LoadParams();
        IsConnected = _camera.IsLoggedIn;
        RefreshChannels();
        _isNavigatedTo = true;
        TryStartPreview();
    }

    public void OnNavigatedFrom(NavigationContext ctx)
    {
        _isNavigatedTo = false;
        _camera.StopPreview();
    }

    public bool IsNavigationTarget(NavigationContext ctx) => true;

    // ── HWND 传递（由 code-behind 调用） ─────────────────────
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
        if (_isNavigatedTo && _previewHwnd != IntPtr.Zero && _camera.IsLoggedIn && _selectedChannel != null)
            _camera.StartPreview(_selectedChannel.ChannelNo, _previewHwnd);
    }

    private void OnStartPreview()
    {
        if (_selectedChannel == null || _previewHwnd == IntPtr.Zero) return;
        _camera.StartPreview(_selectedChannel.ChannelNo, _previewHwnd);
    }

    private void OnConnect()
    {
        if (!_camera.Login(Ip, Port, User, Pwd))
        {
            _log.Warn($"摄像机登录失败 {Ip}:{Port}", "摄像");
            IsConnected = false;
            return;
        }
        IsConnected = true;
        RefreshChannels();
        _log.Info($"摄像机已连接 {Ip}:{Port}", "摄像");
        TryStartPreview();
    }

    private void OnDisconnect()
    {
        _camera.Logout();
        IsConnected = false;
        Channels.Clear();
    }

    private async Task OnCaptureAsync()
    {
        if (!_camera.IsLoggedIn || _selectedChannel == null) return;
        var path = Path.Combine(
            _db.SystemSettings.Find(SettingKeys.ImageStoragePath)?.Value ?? @"D:\WeighImages\",
            "manual",
            $"{DateTime.Now:yyyyMMdd_HHmmss}_ch{_selectedChannel.ChannelNo}.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var result = await _camera.CaptureJpegAsync(_selectedChannel.ChannelNo, path);
        if (result != null) _log.Info($"手动抓图成功：{path}", "摄像");
        else                _log.Warn("手动抓图失败", "摄像");
    }

    private void LoadParams()
    {
        Ip   = _db.SystemSettings.Find(SettingKeys.CameraIp)?.Value   ?? string.Empty;
        Port = int.TryParse(_db.SystemSettings.Find(SettingKeys.CameraPort)?.Value, out var p) ? p : 8000;
        User = _db.SystemSettings.Find(SettingKeys.CameraUser)?.Value  ?? "admin";
        Pwd  = _db.SystemSettings.Find(SettingKeys.CameraPassword)?.Value ?? string.Empty;
    }

    private void RefreshChannels()
    {
        Channels.Clear();
        foreach (var ch in _camera.GetChannels())
            Channels.Add(ch);
        if (Channels.Count > 0) SelectedChannel = Channels[0];
    }
}
