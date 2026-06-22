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

/// <summary>
/// 参数管理：通过 PropertyGrid 绑定 SystemParameters POCO，集中展示并修改所有系统参数。
/// 编辑器由控件库按属性类型自动生成；保存时将 POCO 值回写 SystemSettings。
/// </summary>
public class ParameterManageViewModel : BindableBase, INavigationAware
{
    private readonly AwsDbContext _db;
    private readonly ILogService _log;

    private SystemParameters _parameters = new();
    /// <summary>PropertyGrid 绑定的参数对象。</summary>
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
    public bool IsNavigationTarget(NavigationContext ctx) => true;

    private async Task LoadAsync()
    {
        var all = await _db.SystemSettings.ToDictionaryAsync(s => s.Key);

        // 用新实例承载已加载值：引用变化 → PropertyGrid 自动刷新
        Parameters = new SystemParameters
        {
            CompanyName = Get(all, SettingKeys.CompanyName, "绿鑫资源"),
            DefaultPricePerKg = double.TryParse(Get(all, SettingKeys.DefaultPricePerKg, "0"), out var price) ? price : 0,
            SerialPortEnabled = Get(all, SettingKeys.SerialPortEnabled, "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            SerialPortName = Get(all, SettingKeys.SerialPortName, "COM1"),
            BaudRate = int.TryParse(Get(all, SettingKeys.BaudRate, "9600"), out var baud) ? baud : 9600,
            SkinType = Enum.TryParse<SkinType>(Get(all, SettingKeys.SkinType, nameof(SkinType.Dark)), out var s) ? s : SkinType.Dark,
            CloudSyncEnabled = Get(all, SettingKeys.CloudSyncEnabled, "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            CloudSyncUrl = Get(all, SettingKeys.CloudSyncUrl, string.Empty),
        };
    }

    private void Save()
    {
        var p = Parameters;
        try
        {
            Set(SettingKeys.CompanyName, p.CompanyName);
            Set(SettingKeys.DefaultPricePerKg, p.DefaultPricePerKg.ToString("F4"));
            Set(SettingKeys.SerialPortEnabled, p.SerialPortEnabled ? "true" : "false");
            Set(SettingKeys.SerialPortName, p.SerialPortName);
            Set(SettingKeys.BaudRate, p.BaudRate.ToString());
            Set(SettingKeys.SkinType, p.SkinType.ToString());
            Set(SettingKeys.CloudSyncEnabled, p.CloudSyncEnabled ? "true" : "false");
            Set(SettingKeys.CloudSyncUrl, p.CloudSyncUrl);

            _db.SaveChanges();

            // 皮肤即时生效（直接切换应用级资源字典，无需引用 Shell 层）
            ApplySkin(p.SkinType);

            _log.Info($"参数已保存（皮肤：{p.SkinType}，单价：{p.DefaultPricePerKg:F2}，串口：{(p.SerialPortEnabled ? p.SerialPortName : "关闭")}）", "参数管理");
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

    /// <summary>
    /// 直接切换应用级合并字典中的皮肤色彩（slot[0]）。
    /// 仅替换 Colors 字典，控件主题（slot[1] = Themes/Default.xaml）保持不动，
    /// 避免触发控件模板重新应用而重置 SideMenu 等运行时状态。
    /// </summary>
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
