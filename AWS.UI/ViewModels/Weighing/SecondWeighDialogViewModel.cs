using AWS.Core.Entities;
using AWS.Core.Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using System.Windows;

namespace AWS.UI.ViewModels.Weighing;

public class SecondWeighDialogViewModel : BindableBase
{
    private readonly ISerialPortService _serial;
    private readonly Func<long, double, double?, Task> _archiveFunc;

    public WeighingQueue QueueItem { get; }

    // ── 实时重量 ────────────────────────────────────────────
    private double _currentWeight;
    public double CurrentWeight
    {
        get => _currentWeight;
        private set => SetProperty(ref _currentWeight, value);
    }

    private bool _isStable;
    public bool IsStable
    {
        get => _isStable;
        private set { SetProperty(ref _isStable, value); CaptureCommand.RaiseCanExecuteChanged(); }
    }

    // ── 二次采集 ────────────────────────────────────────────
    private double? _capturedSecondWeight;
    public double? CapturedSecondWeight
    {
        get => _capturedSecondWeight;
        private set
        {
            SetProperty(ref _capturedSecondWeight, value);
            RaisePropertyChanged(nameof(HasCapturedWeight));
            CalculateWeights();
            ConfirmCommand.RaiseCanExecuteChanged();
        }
    }
    public bool HasCapturedWeight => _capturedSecondWeight.HasValue;

    // ── 计算结果 ────────────────────────────────────────────
    private double _grossWeight;
    public double GrossWeight { get => _grossWeight; private set => SetProperty(ref _grossWeight, value); }

    private double _tareWeight;
    public double TareWeight { get => _tareWeight; private set => SetProperty(ref _tareWeight, value); }

    private double _netWeight;
    public double NetWeight { get => _netWeight; private set => SetProperty(ref _netWeight, value); }

    // ── 价格 ────────────────────────────────────────────────
    private string _priceText = "0";
    public string PriceText
    {
        get => _priceText;
        set { SetProperty(ref _priceText, value); CalculateTotal(); }
    }

    private double _totalAmount;
    public double TotalAmount { get => _totalAmount; private set => SetProperty(ref _totalAmount, value); }

    // ── 备注 ────────────────────────────────────────────────
    private string _remark = string.Empty;
    public string Remark { get => _remark; set => SetProperty(ref _remark, value); }

    public bool Succeeded { get; private set; }

    public DelegateCommand CaptureCommand { get; }
    public DelegateCommand ConfirmCommand { get; }
    public DelegateCommand CancelCommand { get; }

    // View 设置此 Action 以关闭窗口
    public Action? CloseWindow { get; set; }

    public SecondWeighDialogViewModel(
        WeighingQueue item,
        double defaultPrice,
        ISerialPortService serial,
        Func<long, double, double?, Task> archiveFunc)
    {
        QueueItem = item;
        _serial = serial;
        _archiveFunc = archiveFunc;
        _priceText = defaultPrice.ToString("F2");

        CaptureCommand = new DelegateCommand(
            () => CapturedSecondWeight = CurrentWeight,
            () => IsStable);
        ConfirmCommand = new DelegateCommand(OnConfirm, () => HasCapturedWeight);
        CancelCommand = new DelegateCommand(() => CloseWindow?.Invoke());

        _serial.WeightReceived += OnWeightReceived;
    }

    public void Detach() => _serial.WeightReceived -= OnWeightReceived;

    private void OnWeightReceived(object? sender, Core.Models.WeightReading reading)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentWeight = reading.Value;
            IsStable = reading.IsStable;
        });
    }

    private void CalculateWeights()
    {
        if (!_capturedSecondWeight.HasValue) return;
        GrossWeight = Math.Max(QueueItem.FirstWeight, _capturedSecondWeight.Value);
        TareWeight = Math.Min(QueueItem.FirstWeight, _capturedSecondWeight.Value);
        NetWeight = GrossWeight - TareWeight;
        CalculateTotal();
    }

    private void CalculateTotal()
    {
        if (double.TryParse(PriceText, out double price) && price > 0)
            TotalAmount = Math.Round(NetWeight * price, 2);
        else
            TotalAmount = 0;
    }

    private async void OnConfirm()
    {
        if (!_capturedSecondWeight.HasValue) return;
        double? price = double.TryParse(PriceText, out double p) && p > 0 ? p : null;
        try
        {
            await _archiveFunc(QueueItem.Id, _capturedSecondWeight.Value, price);
            Succeeded = true;
            Detach();
            CloseWindow?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"存档失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
