using PF.UI.Shared.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AWS.Shell.Views;

public partial class SplashWindow : Window
{
    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(nameof(StatusMessage), typeof(string), typeof(SplashWindow),
            new PropertyMetadata("正在启动，请稍候..."));

    public static readonly DependencyProperty StatusTypeProperty =
        DependencyProperty.Register(nameof(StatusType), typeof(MsgType), typeof(SplashWindow),
            new PropertyMetadata(MsgType.Info));

    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    public MsgType StatusType
    {
        get => (MsgType)GetValue(StatusTypeProperty);
        set => SetValue(StatusTypeProperty, value);
    }

    public Func<Task<bool>> LoadingAction { get; set; } = () => Task.FromResult(true);

    public SplashWindow() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        bool result = await (LoadingAction?.Invoke() ?? Task.FromResult(true));
        Dispatcher.Invoke(() => DialogResult = result);
    }

    public void UpdateMessage(string message, MsgType type = MsgType.Info)
    {
        StatusMessage = message;
        StatusType = type;
    }
}

internal class SplashMsgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MsgType type)
            return type switch
            {
                MsgType.Success => new SolidColorBrush(Color.FromRgb(0x10, 0x89, 0x3E)), // SuccessColor
                MsgType.Error   => new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23)), // DangerColor
                MsgType.Fatal   => new SolidColorBrush(Color.FromRgb(0xA8, 0x00, 0x00)), // DarkDangerColor
                _               => new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)), // SecondaryTextColor
            };
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
