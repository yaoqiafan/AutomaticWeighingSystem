using AWS.Core.Entities;
using AWS.Core.Models;
using AWS.Data;
using AWS.Shell.ViewModels;
using PF.UI.Controls;
using PF.UI.Shared.Data;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace AWS.Shell.Views;

public partial class MainWindow : PF.UI.Controls.Window
{
    private bool _isAnimating;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.LogEntries.CollectionChanged += OnLogEntriesChanged;

        // 菜单展开状态完全由 XAML 中 ExpandMode="ShowAll" 控制：
        // 所有分组默认展开，且任何点击/导航都不会折叠。
        // 皮肤切换只替换 Colors 字典（不触碰主题模板），故不会重置展开状态。

        // 同步主题图标
        SyncThemeIcon();
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            LogScrollViewer.ScrollToBottom();
    }

    // ─── 主题切换（圆形扩散动画）────────────────────────────────────────────
    private void ToggleThemeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isAnimating) return;
        _isAnimating = true;

        var w = (int)Math.Max(ActualWidth, 1);
        var h = (int)Math.Max(ActualHeight, 1);

        // 1. 截取旧主题快照
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(this);

        // 2. 圆心 = 按钮中心
        var btnPos = ThemeBtn.TransformToAncestor(this).Transform(new Point(0, 0));
        var cx = btnPos.X + ThemeBtn.ActualWidth / 2;
        var cy = btnPos.Y + ThemeBtn.ActualHeight / 2;

        // 3. 覆盖层窗口（遮蔽主题切换瞬间）
        var overlayImg = new System.Windows.Controls.Image
        {
            Source = rtb,
            Width = w,
            Height = h,
            Stretch = Stretch.None
        };
        var fullRect = new RectangleGeometry(new Rect(0, 0, w, h));
        var hole = new EllipseGeometry { Center = new Point(cx, cy), RadiusX = 0, RadiusY = 0 };
        overlayImg.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, hole);

        var overlayWin = new System.Windows.Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Width = w,
            Height = h,
            Left = Left,
            Top = Top,
            Content = overlayImg,
            Focusable = false
        };
        overlayWin.Show();

        // 4. 切换主题（覆盖层遮蔽，用户不可见）
        if (Application.Current is App app)
        {
            var newSkin = GetCurrentSkin() == SkinType.Dark ? SkinType.Default : SkinType.Dark;
            app.UpdateSkin(newSkin);
            ThemeIcon.Kind = newSkin == SkinType.Dark ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;
            PersistSkin(newSkin);
        }

        // 5. 用 DoubleAnimation 驱动镂空圆扩大（ease in-out cubic）
        var maxR = Math.Sqrt(w * w + h * h) + 20;
        var duration = new Duration(TimeSpan.FromMilliseconds(600));
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var animR = new DoubleAnimation(0, maxR, duration) { EasingFunction = easing };
        animR.Completed += (_, _) =>
        {
            overlayWin.Close();
            _isAnimating = false;
        };

        hole.BeginAnimation(EllipseGeometry.RadiusXProperty, animR);
        hole.BeginAnimation(EllipseGeometry.RadiusYProperty,
            new DoubleAnimation(0, maxR, duration) { EasingFunction = easing });
    }

    private void SyncThemeIcon()
    {
        ThemeIcon.Kind = GetCurrentSkin() == SkinType.Dark
            ? PackIconKind.WeatherSunny
            : PackIconKind.WeatherNight;
    }

    private static SkinType GetCurrentSkin()
    {
        var source = Application.Current?.Resources.MergedDictionaries[0]
            .MergedDictionaries.FirstOrDefault()?.Source?.ToString() ?? string.Empty;
        foreach (SkinType skin in Enum.GetValues<SkinType>())
        {
            if (source.Contains(skin.ToString(), StringComparison.OrdinalIgnoreCase))
                return skin;
        }
        return SkinType.Dark;
    }

    private static void PersistSkin(SkinType skin)
    {
        var db = Prism.Ioc.ContainerLocator.Container.Resolve<AwsDbContext>();
        var setting = db.SystemSettings.Find(SettingKeys.SkinType);
        if (setting != null)
        {
            setting.Value = skin.ToString();
            db.SaveChanges();
        }
    }
}
