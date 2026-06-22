using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using AWS.Services;
using AWS.Shell.Views;
using AWS.UI;
using Microsoft.EntityFrameworkCore;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace AWS.Shell;

public partial class App : PrismApplication
{
    private static readonly string DbPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LxAws", "weighing.db");

    // TODO: 打包前将 DevAutoLogin 改为 false
    private const bool DevAutoLogin = true;

    protected override Window CreateShell()
    {
        var userService = Container.Resolve<IUserService>();

        if (DevAutoLogin)
        {
            userService.LoginAsync("superuser", DateTime.Now.ToString("yyyyMMddHH00"))
                .GetAwaiter().GetResult();
        }
        else
        {
            var login = Container.Resolve<LoginWindow>();
            if (login.ShowDialog() != true)
            {
                Environment.Exit(0);
                return null!;
            }
        }

        ApplySkin();
        return Container.Resolve<MainWindow>();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        var log = Container.Resolve<ILogService>();
        log.Info("系统启动", "App");
        InitializeSerialPort();
        Container.Resolve<IRegionManager>()
            .RequestNavigate(RegionNames.Main, nameof(AWS.UI.Views.Weighing.WeighingView));
    }

    private void InitializeSerialPort()
    {
        var db = Container.Resolve<AwsDbContext>();
        var serial = Container.Resolve<ISerialPortService>();
        var enabled = db.SystemSettings.Find(SettingKeys.SerialPortEnabled)?.Value == "true";
        if (!enabled)
        {
            serial.StartSimulation();
            return;
        }
        var portName = db.SystemSettings.Find(SettingKeys.SerialPortName)?.Value ?? "COM1";
        var baudStr = db.SystemSettings.Find(SettingKeys.BaudRate)?.Value ?? "9600";
        int baud = int.TryParse(baudStr, out int b) ? b : 9600;
        try { serial.Connect(portName, baud); }
        catch { /* 串口连接失败，界面状态栏会提示 */ }
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

        var options = new DbContextOptionsBuilder<AwsDbContext>()
            .UseSqlite($"Data Source={DbPath}")
            .Options;

        containerRegistry.RegisterInstance(options);
        containerRegistry.RegisterSingleton<AwsDbContext>();
        containerRegistry.RegisterSingleton<DynamicArchiveManager>();

        containerRegistry.RegisterSingleton<ILogService, LogService>();
        containerRegistry.RegisterSingleton<IUserService, UserService>();
        containerRegistry.RegisterSingleton<ISerialPortService, SerialPortService>();
        containerRegistry.RegisterSingleton<IWeighingService, WeighingService>();
        containerRegistry.RegisterSingleton<IArchiveQueryService, ArchiveQueryService>();
        containerRegistry.RegisterSingleton<IExportService, ExportService>();
        containerRegistry.RegisterSingleton<ICloudSyncService, CloudSyncService>();

        containerRegistry.RegisterForNavigation<LoginWindow>();
        containerRegistry.RegisterForNavigation<MainWindow>();

        EnsureDatabase(containerRegistry);
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<WeighingUIModule>();
    }

    private void EnsureDatabase(IContainerRegistry containerRegistry)
    {
        var container = containerRegistry.GetContainer();
        var db = container.Resolve<AwsDbContext>();
        db.Database.EnsureCreated();
    }

    private void ApplySkin()
    {
        var db = Container.Resolve<AwsDbContext>();
        var skin = db.SystemSettings.Find(SettingKeys.SkinType)?.Value ?? "Dark";
        UpdateSkin(skin);
    }

    internal void UpdateSkin(string skinName)
    {
        var validSkin = skinName == "Light" ? "Light" : "Dark";

        var skins0 = Resources.MergedDictionaries[0];
        skins0.MergedDictionaries.Clear();
        skins0.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/PF.UI.Resources;component/Colors/{validSkin}.xaml")
        });

        var skins1 = Resources.MergedDictionaries[1];
        skins1.MergedDictionaries.Clear();
        skins1.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/PF.UI.Resources;component/Themes/Default.xaml")
        });
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"发生未处理错误：{e.Exception.Message}", "系统错误",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}

public static class RegionNames
{
    public const string Main = "MainRegion";
}
