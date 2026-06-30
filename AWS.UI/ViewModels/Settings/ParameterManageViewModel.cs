using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using AWS.UI.Models;
using Microsoft.EntityFrameworkCore;
using PF.UI.Shared.Data;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace AWS.UI.ViewModels.Settings;

public class ParameterManageViewModel : BindableBase, INavigationAware
{
    private readonly AwsDbContext _db;
    private readonly ILogService _log;

    private SystemParameters _parameters = new();
    public SystemParameters Parameters
    {
        get => _parameters;
        private set => SetProperty(ref _parameters, value);
    }

    public DelegateCommand SaveCommand { get; }
    public DelegateCommand ReloadCommand { get; }

    public ParameterManageViewModel(AwsDbContext db, ILogService log)
    {
        _db = db;
        _log = log;
        SaveCommand = new DelegateCommand(Save);
        ReloadCommand = new DelegateCommand(async () => await LoadAsync());
    }

    public async void OnNavigatedTo(NavigationContext ctx) => await LoadAsync();
    public void OnNavigatedFrom(NavigationContext ctx) { }
    public bool IsNavigationTarget(NavigationContext ctx) => false;

    private async Task LoadAsync()
    {
        var all = await _db.SystemSettings.ToDictionaryAsync(s => s.Key);

        Parameters = new SystemParameters
        {
            CompanyName           = Get(all, SettingKeys.CompanyName, "绿鑫资源"),
            SerialPortConfigsJson = Get(all, SettingKeys.SerialPortConfigs, "[]"),
            SkinType              = Enum.TryParse<SkinType>(Get(all, SettingKeys.SkinType, nameof(SkinType.Dark)), out var s) ? s : SkinType.Dark,
            CloudSyncEnabled      = Get(all, SettingKeys.CloudSyncEnabled, "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            CloudSyncUrl          = Get(all, SettingKeys.CloudSyncUrl, string.Empty),
            CameraIp              = Get(all, SettingKeys.CameraIp, string.Empty),
            CameraPort            = int.TryParse(Get(all, SettingKeys.CameraPort, "8000"), out var cp) ? cp : 8000,
            CameraUser            = Get(all, SettingKeys.CameraUser, "admin"),
            CameraPassword        = Get(all, SettingKeys.CameraPassword, string.Empty),
            CaptureChannel        = int.TryParse(Get(all, SettingKeys.CaptureChannel, "1"), out var ch) ? ch : 1,
            ImageStoragePath      = Get(all, SettingKeys.ImageStoragePath, @"D:\WeighImages\"),
            DiskWarningPercent    = double.TryParse(Get(all, SettingKeys.DiskWarningPercent, "20"), out var dw) ? dw : 20,
            AutoDeleteKeepDays    = int.TryParse(Get(all, SettingKeys.AutoDeleteKeepDays, "90"), out var ad) ? ad : 90,
        };
    }

    private void Save()
    {
        var p = Parameters;
        try
        {
            Set(SettingKeys.CompanyName,        p.CompanyName);
            Set(SettingKeys.SerialPortConfigs,   p.SerialPortConfigsJson);
            Set(SettingKeys.SkinType,            p.SkinType.ToString());
            Set(SettingKeys.CloudSyncEnabled,    p.CloudSyncEnabled ? "true" : "false");
            Set(SettingKeys.CloudSyncUrl,        p.CloudSyncUrl);
            Set(SettingKeys.CameraIp,            p.CameraIp);
            Set(SettingKeys.CameraPort,          p.CameraPort.ToString());
            Set(SettingKeys.CameraUser,          p.CameraUser);
            Set(SettingKeys.CameraPassword,      p.CameraPassword);
            Set(SettingKeys.CaptureChannel,      p.CaptureChannel.ToString());
            Set(SettingKeys.ImageStoragePath,    p.ImageStoragePath);
            Set(SettingKeys.DiskWarningPercent,  p.DiskWarningPercent.ToString("F1"));
            Set(SettingKeys.AutoDeleteKeepDays,  p.AutoDeleteKeepDays.ToString());

            _db.SaveChanges();
            ApplySkin(p.SkinType);

            _log.Info($"参数已保存（皮肤：{p.SkinType}）", "参数管理");
        }
        catch (Exception ex)
        {
            _log.Error($"保存失败：{ex.Message}", "参数管理");
        }
    }

    private static string Get(Dictionary<string, SystemSetting> src, string key, string def)
        => src.TryGetValue(key, out var s) ? s.Value : def;

    private void Set(string key, string value)
    {
        var setting = _db.SystemSettings.Find(key);
        if (setting != null) setting.Value = value;
        else _db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
    }

    private static void ApplySkin(SkinType skin)
    {
        var app = System.Windows.Application.Current;
        if (app?.Resources.MergedDictionaries is not { Count: >= 1 } dicts) return;

        var skins0 = dicts[0];
        skins0.MergedDictionaries.Clear();
        skins0.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/PF.UI.Resources;component/Colors/{skin}.xaml")
        });
    }
}
