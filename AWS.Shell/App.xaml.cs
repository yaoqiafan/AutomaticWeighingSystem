using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using AWS.Services;
using AWS.Shell.Views;
using AWS.UI;
using Microsoft.EntityFrameworkCore;
using PF.UI.Shared.Data;
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

    #region 程序启动与自检

    private static readonly string MutexName =
        "Global\\LxAwsWeighingSystem-7E4F2A1B-C3D9-4E8F-A012-B56789CDEF01";
    private static Mutex? _appMutex;
    private static bool _isNewInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (RunningInstance())
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }
        else
        {
            MessageBox.Show("程序已在运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        if (_isNewInstance)
        {
            _appMutex?.ReleaseMutex();
            _appMutex?.Dispose();
        }
    }

    private static bool RunningInstance()
    {
        try
        {
            _appMutex = new Mutex(true, MutexName, out _isNewInstance);
        }
        catch (UnauthorizedAccessException)
        {
            // 无权限访问全局互斥体（非管理员），降级为本地互斥体
            _appMutex = new Mutex(true, MutexName.Replace("Global\\", ""), out _isNewInstance);
        }
        return _isNewInstance;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var ex = e.Exception;
        var str = $"出现未处理异常\n异常类型：{ex.GetType().Name}\n异常消息：{ex.Message}\n堆栈信息：{ex.StackTrace}";

        try { Container.Resolve<ILogService>().Error(str, "App"); } catch { }

        MessageBox.Show(str, "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    #endregion

    #region Prism 框架核心

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
        var db = containerRegistry.GetContainer().Resolve<AwsDbContext>();
        db.Database.EnsureCreated();
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

    #endregion

    #region 界面资源

    private void ApplySkin()
    {
        var db = Container.Resolve<AwsDbContext>();
        var skinStr = db.SystemSettings.Find(SettingKeys.SkinType)?.Value ?? nameof(SkinType.Dark);
        var skin = Enum.TryParse<SkinType>(skinStr, out var s) ? s : SkinType.Dark;
        UpdateSkin(skin);
    }

    internal void UpdateSkin(SkinType skin = SkinType.Dark)
    {
        var skins0 = Resources.MergedDictionaries[0];
        skins0.MergedDictionaries.Clear();
        skins0.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/PF.UI.Resources;component/Colors/{skin}.xaml")
        });

        var skins1 = Resources.MergedDictionaries[1];
        skins1.MergedDictionaries.Clear();
        skins1.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/PF.UI.Resources;component/Themes/Default.xaml")
        });
    }

    #endregion
}

public static class RegionNames
{
    public const string Main = "MainRegion";
}
