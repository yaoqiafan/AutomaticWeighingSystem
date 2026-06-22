using AWS.Core.Entities;
using AWS.Data;
using Microsoft.EntityFrameworkCore;
using PF.UI.Shared.Data;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;

namespace AWS.UI.ViewModels.Settings;

/// <summary>
/// 参数管理：集中展示并修改所有 SystemSettings。
/// 已知参数以表单形式编辑（含下拉/复选），其余参数以通用列表编辑。
/// </summary>
public class ParameterManageViewModel : BindableBase, INavigationAware
{
    private readonly AwsDbContext _db;

    // ── 通用参数（已知） ───────────────────────────────────
    private string _companyName = string.Empty;
    public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }

    private string _defaultPricePerKg = "0";
    public string DefaultPricePerKg { get => _defaultPricePerKg; set => SetProperty(ref _defaultPricePerKg, value); }

    private bool _serialPortEnabled;
    public bool SerialPortEnabled { get => _serialPortEnabled; set => SetProperty(ref _serialPortEnabled, value); }

    private string _serialPortName = "COM1";
    public string SerialPortName { get => _serialPortName; set => SetProperty(ref _serialPortName, value); }

    private string _baudRate = "9600";
    public string BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }

    private SkinType _skinType = SkinType.Dark;
    public SkinType SkinType { get => _skinType; set => SetProperty(ref _skinType, value); }

    private bool _cloudSyncEnabled;
    public bool CloudSyncEnabled { get => _cloudSyncEnabled; set => SetProperty(ref _cloudSyncEnabled, value); }

    private string _cloudSyncUrl = string.Empty;
    public string CloudSyncUrl { get => _cloudSyncUrl; set => SetProperty(ref _cloudSyncUrl, value); }

    // 皮肤下拉选项（枚举值 + 中文显示名）
    public SkinOption[] SkinOptions { get; } =
    [
        new(SkinType.Default, "浅色 (Default)"),
        new(SkinType.Dark, "深色 (Dark)"),
        new(SkinType.Violet, "紫色 (Violet)")
    ];
    public string[] BaudRateOptions { get; } = ["4800", "9600", "19200", "38400", "57600", "115200"];

    // ── 其它参数（未知/扩展） ─────────────────────────────
    public ObservableCollection<SystemSetting> OtherSettings { get; } = [];

    private static readonly HashSet<string> KnownKeys = new()
    {
        SettingKeys.CompanyName, SettingKeys.DefaultPricePerKg,
        SettingKeys.SerialPortEnabled, SettingKeys.SerialPortName, SettingKeys.BaudRate,
        SettingKeys.SkinType, SettingKeys.CloudSyncEnabled, SettingKeys.CloudSyncUrl
    };

    public DelegateCommand SaveCommand { get; }
    public DelegateCommand ReloadCommand { get; }

    public ParameterManageViewModel(AwsDbContext db)
    {
        _db = db;
        SaveCommand = new DelegateCommand(Save);
        ReloadCommand = new DelegateCommand(async () => await LoadAsync());
    }

    public async void OnNavigatedTo(NavigationContext ctx) => await LoadAsync();
    public void OnNavigatedFrom(NavigationContext ctx) { }
    public bool IsNavigationTarget(NavigationContext ctx) => true;

    private async Task LoadAsync()
    {
        var all = await _db.SystemSettings.ToDictionaryAsync(s => s.Key);

        CompanyName = Get(all, SettingKeys.CompanyName, "绿鑫资源");
        DefaultPricePerKg = Get(all, SettingKeys.DefaultPricePerKg, "0");
        SerialPortEnabled = Get(all, SettingKeys.SerialPortEnabled, "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        SerialPortName = Get(all, SettingKeys.SerialPortName, "COM1");
        BaudRate = Get(all, SettingKeys.BaudRate, "9600");
        var skinStr = Get(all, SettingKeys.SkinType, nameof(SkinType.Dark));
        SkinType = Enum.TryParse<SkinType>(skinStr, out var s) ? s : SkinType.Dark;
        CloudSyncEnabled = Get(all, SettingKeys.CloudSyncEnabled, "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        CloudSyncUrl = Get(all, SettingKeys.CloudSyncUrl, string.Empty);

        OtherSettings.Clear();
        foreach (var kv in all.Where(k => !KnownKeys.Contains(k.Key)))
            OtherSettings.Add(kv.Value);
    }

    private static string Get(Dictionary<string, SystemSetting> src, string key, string def)
        => src.TryGetValue(key, out var s) ? s.Value : def;

    private void Save()
    {
        try
        {
            Set(SettingKeys.CompanyName, CompanyName);
            Set(SettingKeys.DefaultPricePerKg, DefaultPricePerKg);
            Set(SettingKeys.SerialPortEnabled, SerialPortEnabled ? "true" : "false");
            Set(SettingKeys.SerialPortName, SerialPortName);
            Set(SettingKeys.BaudRate, BaudRate);
            Set(SettingKeys.SkinType, SkinType.ToString());
            Set(SettingKeys.CloudSyncEnabled, CloudSyncEnabled ? "true" : "false");
            Set(SettingKeys.CloudSyncUrl, CloudSyncUrl);

            _db.SaveChanges();

            // 皮肤即时生效（直接切换应用级资源字典，无需引用 Shell 层）
            ApplySkin(SkinType);

            System.Windows.MessageBox.Show("参数已保存", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void Set(string key, string value)
    {
        var setting = _db.SystemSettings.Find(key);
        if (setting != null) setting.Value = value;
        else _db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
    }

    /// <summary>
    /// 直接切换应用级合并字典中的皮肤资源，与 App.UpdateSkin 逻辑一致。
    /// AWS.UI 不引用 AWS.Shell，故在此内联实现以保持皮肤即时切换。
    /// </summary>
    private static void ApplySkin(SkinType skin)
    {
        var app = System.Windows.Application.Current;
        if (app?.Resources.MergedDictionaries is not { Count: >= 2 } dicts) return;

        var skins0 = dicts[0];
        skins0.MergedDictionaries.Clear();
        skins0.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/PF.UI.Resources;component/Colors/{skin}.xaml")
        });

        var skins1 = dicts[1];
        skins1.MergedDictionaries.Clear();
        skins1.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/PF.UI.Resources;component/Themes/Default.xaml")
        });
    }
}

/// <summary>皮肤下拉项：枚举值 + 中文显示名。</summary>
public record SkinOption(SkinType Value, string Display);
