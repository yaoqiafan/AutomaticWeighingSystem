using AWS.Core.Enums;
using AWS.Core.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace AWS.UI.Views.Settings;

public partial class SerialPortConfigsDialogWindow : PF.UI.Controls.Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string ResultJson { get; private set; } = "[]";

    public ObservableCollection<SerialPortConfigItem> Devices { get; } = [];
    public string[] AvailablePorts { get; } = SerialPort.GetPortNames();
    public string[] WeighModeOptions { get; } = ["首重", "二次称重", "两者"];

    public bool CanAddDevice => Devices.Count < 2;

    private readonly RelayCommand _addCmd;
    public ICommand AddCommand => _addCmd;
    public ICommand RemoveCommand { get; }

    public SerialPortConfigsDialogWindow(string configJson)
    {
        InitializeComponent();
        DataContext = this;

        _addCmd = new RelayCommand(
            _ => Devices.Add(new SerialPortConfigItem()),
            _ => Devices.Count < 2);

        RemoveCommand = new RelayCommand(p =>
        {
            if (p is SerialPortConfigItem item) Devices.Remove(item);
        });

        Devices.CollectionChanged += OnDevicesChanged;

        try
        {
            var configs = JsonSerializer.Deserialize<List<SerialPortConfig>>(configJson) ?? [];
            foreach (var c in configs)
                Devices.Add(new SerialPortConfigItem(c));
        }
        catch { }
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _addCmd.RaiseCanExecuteChanged();
        Notify(nameof(CanAddDevice));
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DeviceGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        // 验证：称重用途为[两者]的已启用设备只能有一个，且不能与其他已启用设备共存
        var enabledDevices = Devices.Where(d => d.IsEnabled).ToList();
        var bothCount = enabledDevices.Count(d => d.WeighModeDisplay == "两者");

        if (bothCount > 0 && enabledDevices.Count > 1)
        {
            MessageBox.Show(
                "称重用途为[两者]的设备启用时，不能同时启用其他设备。\n\n请确保只有一个设备处于启用状态，或将该设备的称重用途改为[首重]或[二次称重]。",
                "配置验证",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var configs = Devices.Select(d => d.ToConfig()).ToList();
        ResultJson = JsonSerializer.Serialize(configs);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => canExecute?.Invoke(p) ?? true;
        public void Execute(object? p) => execute(p);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>DataGrid 绑定用的行模型；WeighModeDisplay 负责中文↔枚举互转。</summary>
public class SerialPortConfigItem
{
    public bool IsEnabled { get; set; } = true;
    public bool IsSimulationMode { get; set; } = false;
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;

    private WeighMode _weighMode = WeighMode.Both;
    public string WeighModeDisplay
    {
        get => _weighMode switch
        {
            WeighMode.FirstWeigh => "首重",
            WeighMode.SecondWeigh => "二次称重",
            _ => "两者"
        };
        set => _weighMode = value switch
        {
            "首重" => WeighMode.FirstWeigh,
            "二次称重" => WeighMode.SecondWeigh,
            _ => WeighMode.Both
        };
    }

    public SerialPortConfigItem() { }

    public SerialPortConfigItem(SerialPortConfig cfg)
    {
        IsEnabled = cfg.IsEnabled;
        IsSimulationMode = cfg.IsSimulationMode;
        PortName = cfg.PortName;
        BaudRate = cfg.BaudRate;
        _weighMode = cfg.WeighMode;
    }

    public SerialPortConfig ToConfig() => new()
    {
        IsEnabled = IsEnabled,
        IsSimulationMode = IsSimulationMode,
        PortName = PortName,
        BaudRate = BaudRate,
        WeighMode = _weighMode
    };
}
