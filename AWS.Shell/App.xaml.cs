using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Core.Models;
using AWS.Data;
using AWS.Services;
using AWS.Services.HCNetSDK;
using AWS.Shell.Views;
using AWS.UI;
using Microsoft.EntityFrameworkCore;
using PF.UI.Resources;
using PF.UI.Shared.Data;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using System.IO;
using System.Text.Json;
using System.Timers;
using System.Windows;
using System.Windows.Threading;

namespace AWS.Shell;

public partial class App : PrismApplication
{
    public App() : base() { this.ShutdownMode = ShutdownMode.OnExplicitShutdown; }

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
        if (!RunningInstance())
        {
            MessageBox.Show("程序已在运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // ── 启动屏：在 Prism 框架启动前完成耗时初始化 ──────────────────
        Splash? splash = null;
        splash = new Splash
        {
            WelcomeText       = "绿鑫资源称重系统",
            VersionNumber     = "v1.0",
            WelcomeText_small = "正在启动，请稍候...",
            LoadingAction     = async () =>
            {
                // 1. 数据库预初始化（建表、Schema 迁移、默认数据）
                Dispatcher.Invoke(() => splash!.UpdateMessage("正在初始化数据库...", MsgType.Info));
                await Task.Delay(500);
                try
                {
                    await Task.Run(PreInitDatabase);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => splash!.UpdateMessage($"数据库初始化失败：{ex.Message}", MsgType.Error));
                    await Task.Delay(2000);
                    return false;
                }
                await Task.Delay(1000);
                // 2. 海康 SDK 初始化（P/Invoke 加载 DLL）
                Dispatcher.Invoke(() => splash!.UpdateMessage("正在初始化摄像服务...", MsgType.Info));
               
                await Task.Run(() =>
                {
                    HCNetSDKApi.NET_DVR_Init();
                    HCNetSDKApi.NET_DVR_SetLogToFile(3, @"D:\SWLog\HCNetSDK\", true);
                });
                await Task.Delay(2000);
                Dispatcher.Invoke(() => splash!.UpdateMessage("正在启动应用框架...", MsgType.Info));
                await Task.Delay(500);

                Dispatcher.Invoke(() => splash!.UpdateMessage("系统初始化成功！", MsgType.Success));
                await Task.Delay(1000);
                return true;
            }
        };

        if (splash.ShowDialog() != true)
        {
            Shutdown(1);
            return;
        }

        // Splash 关闭后启动 Prism（DB 已存在，EnsureCreated 极快）
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _maintenanceTimer?.Stop();
        _maintenanceTimer?.Dispose();
        try { Container.Resolve<ICameraService>().Dispose(); } catch { }
        HCNetSDKApi.NET_DVR_Cleanup();
        base.OnExit(e);
        if (_isNewInstance)
        {
            _appMutex?.ReleaseMutex();
            _appMutex?.Dispose();
        }
    }

    private System.Timers.Timer? _maintenanceTimer;

    private static bool RunningInstance()
    {
        try
        {
            _appMutex = new Mutex(true, MutexName, out _isNewInstance);
        }
        catch (UnauthorizedAccessException)
        {
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
        ApplySkin();

        if (DevAutoLogin)
        {
            Container.Resolve<IUserService>()
                .LoginAsync("superuser", DateTime.Now.ToString("yyyyMMddHH00"))
                .GetAwaiter().GetResult();
        }
#pragma warning disable CS0162 // DevAutoLogin is a dev-time constant; else branch is dead until set to false
        else
        {
            var login = Container.Resolve<LoginWindow>();
            if (login.ShowDialog() != true)
                Environment.Exit(0);
        }
#pragma warning restore CS0162

        return Container.Resolve<MainWindow>();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Container.Resolve<ILogService>().Info("系统启动", "App");

        // 串口：本地操作，速度快，同步执行
        InitializeSerialPort();

        // 摄像机登录：网络操作，放后台线程，不阻塞 UI
        _ = Task.Run(InitializeCameraLogin);

        StartDiskMaintenanceTimer();

        Container.Resolve<IRegionManager>()
            .RequestNavigate(RegionNames.Main, nameof(AWS.UI.Views.Weighing.WeighingView));
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // DI 注册（不做耗时的 DB 操作，已在 Splash 预完成）
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
        containerRegistry.RegisterSingleton<IDeliveryService, DeliveryService>();
        containerRegistry.RegisterSingleton<ICameraService, HikvisionCameraService>();
        containerRegistry.RegisterSingleton<IImageStorageService, ImageStorageService>();

        containerRegistry.RegisterForNavigation<LoginWindow>();
        containerRegistry.RegisterForNavigation<MainWindow>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<WeighingUIModule>();
    }

    #endregion

    #region 数据库预初始化（Splash 阶段，独立 DbContext）

    /// <summary>
    /// 在 Splash 阶段、Prism DI 容器建立之前运行。使用独立 DbContext 完成所有耗时的
    /// Schema 创建、迁移和默认数据填充，保证 Prism 启动时 EnsureCreated 几乎瞬间返回。
    /// </summary>
    private void PreInitDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

        var options = new DbContextOptionsBuilder<AwsDbContext>()
            .UseSqlite($"Data Source={DbPath}").Options;

        using var db = new AwsDbContext(options);
        EnsureDatabase(db);
    }

    private static void EnsureDatabase(AwsDbContext db)
    {
        db.Database.EnsureCreated();

        try { db.Database.ExecuteSqlRaw("ALTER TABLE GoodsCategories ADD COLUMN PricePerUnit REAL"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Customers ADD COLUMN Type INTEGER NOT NULL DEFAULT 0"); } catch { }

        // 检测旧 Schema（含 GoodsName 列），存在则删除旧表并重建
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('DeliveryRecords') WHERE name='GoodsName'";
                var hasOld = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) > 0;
                if (hasOld)
                {
                    db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS DeliveryItems");
                    db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS DeliveryRecords");
                }
            }
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS DeliveryRecords (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    TicketNo     TEXT    NOT NULL,
                    CustomerId   INTEGER,
                    CustomerName TEXT    NOT NULL DEFAULT '',
                    OperatorId   INTEGER NOT NULL DEFAULT 0,
                    OperatorName TEXT    NOT NULL DEFAULT '',
                    DeliveryTime TEXT    NOT NULL,
                    TotalWeight  REAL    NOT NULL DEFAULT 0,
                    TotalAmount  REAL,
                    Remark       TEXT
                )
                """);
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS DeliveryItems (
                    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeliveryRecordId INTEGER NOT NULL,
                    GoodsCategoryId  INTEGER,
                    GoodsName        TEXT    NOT NULL DEFAULT '',
                    Weight           REAL    NOT NULL DEFAULT 0,
                    PricePerUnit     REAL,
                    Amount           REAL
                )
                """);
        }
        catch { }

        // 默认参数（幂等）
        var defaults = new[]
        {
            ("WeightUnit",         "kg"),
            ("CameraIp",           ""),
            ("CameraPort",         "8000"),
            ("CameraUser",         "admin"),
            ("CameraPassword",     ""),
            ("CaptureChannel",     "1"),
            ("ImageStoragePath",   @"D:\WeighImages\"),
            ("DiskWarningPercent", "20"),
            ("AutoDeleteKeepDays", "90"),
        };
        foreach (var (key, val) in defaults)
        {
            try
            {
                db.Database.ExecuteSql(
                    $"INSERT OR IGNORE INTO SystemSettings (Key, Value) VALUES ({key}, {val})");
            }
            catch { }
        }

        // 旧归档表补图片列
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'WeighingArchive_%'";
            using var reader = cmd.ExecuteReader();
            var tables = new List<string>();
            while (reader.Read()) tables.Add(reader.GetString(0));
            reader.Close();
            foreach (var tbl in tables)
            {
#pragma warning disable EF1002 // tbl comes from sqlite_master, not user input
                try { db.Database.ExecuteSqlRaw($"ALTER TABLE {tbl} ADD COLUMN FirstWeighImagePath TEXT"); } catch { }
                try { db.Database.ExecuteSqlRaw($"ALTER TABLE {tbl} ADD COLUMN SecondWeighImagePath TEXT"); } catch { }
#pragma warning restore EF1002
            }
        }
        catch { }
    }

    #endregion

    #region 摄像机 & 串口初始化

    private void InitializeCameraLogin()
    {
        var db     = Container.Resolve<AwsDbContext>();
        var camera = Container.Resolve<ICameraService>();
        var log    = Container.Resolve<ILogService>();

        var ip   = db.SystemSettings.Find(SettingKeys.CameraIp)?.Value ?? string.Empty;
        var port = int.TryParse(db.SystemSettings.Find(SettingKeys.CameraPort)?.Value, out var p) ? p : 8000;
        var user = db.SystemSettings.Find(SettingKeys.CameraUser)?.Value ?? "admin";
        var pwd  = db.SystemSettings.Find(SettingKeys.CameraPassword)?.Value ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(ip))
        {
            if (!camera.Login(ip, port, user, pwd))
                log.Warn($"摄像机登录失败 {ip}:{port}，{HCNetSDKError.GetLastError()}", "摄像");
            else
                log.Info($"摄像机已连接 {ip}:{port}", "摄像");
        }
    }

    private void StartDiskMaintenanceTimer()
    {
        _maintenanceTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds);
        _maintenanceTimer.Elapsed += (_, _) =>
        {
            try { Container.Resolve<IImageStorageService>().RunMaintenance(); } catch { }
        };
        _maintenanceTimer.AutoReset = true;
        _maintenanceTimer.Start();
    }

    private void InitializeSerialPort()
    {
        var db = Container.Resolve<AwsDbContext>();
        var serial = Container.Resolve<ISerialPortService>();

        var configJson = db.SystemSettings.Find(SettingKeys.SerialPortConfigs)?.Value ?? "[]";
        List<SerialPortConfig> allConfigs;
        try { allConfigs = JsonSerializer.Deserialize<List<SerialPortConfig>>(configJson) ?? []; }
        catch { allConfigs = []; }

        var activeConfigs = allConfigs.Where(c => c.IsEnabled).ToList();
        if (activeConfigs.Count == 0)
        {
            serial.StartSimulation();
            return;
        }

        try { serial.ConnectAll(activeConfigs); }
        catch { }
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
