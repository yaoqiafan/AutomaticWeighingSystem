using AWS.Core.Entities;
using AWS.Core.Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using System.Windows.Threading;

namespace AWS.UI.ViewModels.Weighing;

/// <summary>
/// 查看 / 编辑队列项对话框：修改车牌、客户、货物、备注、首次重量。
/// 与二次称重对话框（SecondWeighDialogViewModel）功能区分：本对话框仅修改已录入信息，不进行二次称重。
/// </summary>
public class EditQueueDialogViewModel : BindableBase
{
    private readonly Func<long, string, string, string, string?, double, Task<bool>> _updateFunc;
    private readonly Dispatcher _dispatcher;
    private readonly ILogService _log;

    public WeighingQueue QueueItem { get; }

    private string _vehiclePlate = string.Empty;
    public string VehiclePlate { get => _vehiclePlate; set => SetProperty(ref _vehiclePlate, value); }

    private string _customerName = string.Empty;
    public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }

    private string _goodsName = string.Empty;
    public string GoodsName { get => _goodsName; set => SetProperty(ref _goodsName, value); }

    private string _firstWeightText = "0";
    public string FirstWeightText { get => _firstWeightText; set => SetProperty(ref _firstWeightText, value); }

    private string _remark = string.Empty;
    public string Remark { get => _remark; set => SetProperty(ref _remark, value); }

    public bool Succeeded { get; private set; }
    public Action? CloseWindow { get; set; }

    public DelegateCommand SaveCommand { get; }
    public DelegateCommand CancelCommand { get; }

    public EditQueueDialogViewModel(
        WeighingQueue item,
        Func<long, string, string, string, string?, double, Task<bool>> updateFunc,
        ILogService log)
    {
        QueueItem = item;
        _updateFunc = updateFunc;
        _log = log;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _vehiclePlate = item.VehiclePlate ?? string.Empty;
        _customerName = item.CustomerName;
        _goodsName = item.GoodsName;
        _firstWeightText = item.FirstWeight.ToString("F1");
        _remark = item.Remark ?? string.Empty;

        SaveCommand = new DelegateCommand(async () => await SaveAsync());
        CancelCommand = new DelegateCommand(() => CloseWindow?.Invoke());
    }

    private async Task SaveAsync()
    {
        if (!double.TryParse(FirstWeightText, out double w) || w < 0)
        {
            _log.Warn($"首次重量无效，已忽略：{FirstWeightText}", "编辑磅单");
            return;
        }

        try
        {
            Succeeded = await _updateFunc(QueueItem.Id, VehiclePlate, CustomerName, GoodsName, Remark, w);
            if (Succeeded)
            {
                _log.Info($"已修改磅单 {QueueItem.TicketNo} 信息", "编辑磅单");
                CloseWindow?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _log.Error($"修改失败：{ex.Message}", "编辑磅单");
        }
    }
}
