using AWS.Core.Models;
using AWS.UI.Views.Settings;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace AWS.UI.Controls;

public partial class SerialPortConfigsEditorControl : UserControl
{
    public static readonly DependencyProperty ConfigJsonProperty =
        DependencyProperty.Register(
            nameof(ConfigJson),
            typeof(string),
            typeof(SerialPortConfigsEditorControl),
            new FrameworkPropertyMetadata(
                "[]",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnConfigJsonChanged));

    public string ConfigJson
    {
        get => (string)GetValue(ConfigJsonProperty);
        set => SetValue(ConfigJsonProperty, value);
    }

    public SerialPortConfigsEditorControl()
    {
        InitializeComponent();
        UpdateSummary("[]");
    }

    private static void OnConfigJsonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SerialPortConfigsEditorControl ctrl)
            ctrl.UpdateSummary(e.NewValue as string ?? "[]");
    }

    private void UpdateSummary(string json)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<SerialPortConfig>>(json);
            int count = list?.Count ?? 0;
            SummaryText.Text = count == 0 ? "未配置任何设备" : $"{count} 个设备已配置";
        }
        catch
        {
            SummaryText.Text = "配置格式异常";
        }
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SerialPortConfigsDialogWindow(ConfigJson)
        {
            Owner = Window.GetWindow(this)
        };

        if (dlg.ShowDialog() == true)
            ConfigJson = dlg.ResultJson;
    }
}
