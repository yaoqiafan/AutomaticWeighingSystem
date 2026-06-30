using AWS.Core.Entities;
using AWS.Core.Enums;
using AWS.Core.Interfaces;
using AWS.Data;
using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;

namespace AWS.UI.ViewModels.Statistics;

// 单行货物明细（对话框内部使用）
public class DeliveryItemLine : BindableBase
{
    private GoodsCategory? _selectedGoods;
    public GoodsCategory? SelectedGoods
    {
        get => _selectedGoods;
        set
        {
            SetProperty(ref _selectedGoods, value);
            // 选择类别后不自动填充单价；单价由 重量÷金额 反算
            Recalc();
            Changed?.Invoke();
        }
    }

    private string _weightText = string.Empty;
    public string WeightText
    {
        get => _weightText;
        set { SetProperty(ref _weightText, value); Recalc(); Changed?.Invoke(); }
    }

    // 用户直接录入金额
    private string _amountText = string.Empty;
    public string AmountText
    {
        get => _amountText;
        set { SetProperty(ref _amountText, value); Recalc(); Changed?.Invoke(); }
    }

    // 由 金额 / 重量 反算的单价（只读显示）
    private double? _unitPrice;
    public double? UnitPrice { get => _unitPrice; private set => SetProperty(ref _unitPrice, value); }

    // 解析后的金额（供父 ViewModel 汇总使用）
    private double? _amount;
    public double? Amount { get => _amount; private set => SetProperty(ref _amount, value); }

    // 单位换算后的 kg 值（供父 ViewModel 保存时使用）
    public double WeightKg(string unit)
    {
        if (!double.TryParse(WeightText, out double w) || w <= 0) return 0;
        return unit == "ton" ? w * 1000 : w;
    }

    public bool IsValid => SelectedGoods != null
        && double.TryParse(WeightText, out double w) && w > 0;

    public Action? Changed { get; set; }

    private void Recalc()
    {
        bool hasWeight = double.TryParse(WeightText, out double w) && w > 0;
        bool hasAmount = double.TryParse(AmountText, out double a) && a > 0;

        Amount    = hasAmount ? a : null;
        UnitPrice = (hasWeight && hasAmount) ? Math.Round(a / w, 4) : null;

        RaisePropertyChanged(nameof(Amount));
        RaisePropertyChanged(nameof(UnitPrice));
        RaisePropertyChanged(nameof(IsValid));
    }
}

public class AddDeliveryDialogViewModel : BindableBase
{
    private readonly IDeliveryService _delivery;
    private readonly IUserService _user;
    private readonly AwsDbContext _db;

    public ObservableCollection<Customer> AvailableBuyers { get; } = [];
    public ObservableCollection<GoodsCategory> GoodsCategories { get; } = [];
    public ObservableCollection<DeliveryItemLine> ItemLines { get; } = [];

    private Customer? _selectedCustomer;
    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set { SetProperty(ref _selectedCustomer, value); RefreshConfirmCanExecute(); }
    }

    private DateTime _deliveryTime = DateTime.Now;
    public DateTime DeliveryTime { get => _deliveryTime; set => SetProperty(ref _deliveryTime, value); }

    private string _remark = string.Empty;
    public string Remark { get => _remark; set => SetProperty(ref _remark, value); }

    private string _weightUnit = "kg";
    public string WeightUnit { get => _weightUnit; private set => SetProperty(ref _weightUnit, value); }

    private double _totalAmount;
    public double TotalAmount { get => _totalAmount; private set => SetProperty(ref _totalAmount, value); }

    private double _totalWeight;
    public double TotalWeight { get => _totalWeight; private set => SetProperty(ref _totalWeight, value); }

    public bool Succeeded { get; private set; }
    public Action? CloseWindow { get; set; }

    public DelegateCommand ConfirmCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public DelegateCommand AddItemLineCommand { get; }
    public DelegateCommand<DeliveryItemLine> RemoveItemLineCommand { get; }

    public AddDeliveryDialogViewModel(IDeliveryService delivery, IUserService user, AwsDbContext db)
    {
        _delivery = delivery;
        _user = user;
        _db = db;

        ConfirmCommand = new DelegateCommand(async () => await ConfirmAsync(), CanConfirm);
        CancelCommand  = new DelegateCommand(() => CloseWindow?.Invoke());
        AddItemLineCommand = new DelegateCommand(AddItemLine);
        RemoveItemLineCommand = new DelegateCommand<DeliveryItemLine>(RemoveItemLine);

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var buyers = await _db.Customers
            .Where(c => c.IsActive && (c.Type == CustomerType.Buyer || c.Type == CustomerType.Both))
            .OrderBy(c => c.Name).ToListAsync();

        var goods = await _db.GoodsCategories
            .Where(g => g.IsActive).OrderBy(g => g.Name).ToListAsync();

        var unit = _db.SystemSettings.Find(SettingKeys.WeightUnit)?.Value ?? "kg";

        AvailableBuyers.Clear();
        foreach (var b in buyers) AvailableBuyers.Add(b);

        GoodsCategories.Clear();
        foreach (var g in goods) GoodsCategories.Add(g);

        WeightUnit = unit;

        // 默认添加一行
        AddItemLine();
    }

    private void AddItemLine()
    {
        var line = new DeliveryItemLine { Changed = OnItemLinesChanged };
        ItemLines.Add(line);
        RefreshConfirmCanExecute();
    }

    private void RemoveItemLine(DeliveryItemLine line)
    {
        line.Changed = null;
        ItemLines.Remove(line);
        OnItemLinesChanged();
    }

    private void OnItemLinesChanged()
    {
        // 重新计算合计
        double weight = 0, amount = 0;
        foreach (var line in ItemLines)
        {
            weight += line.WeightKg(WeightUnit);
            amount += line.Amount ?? 0;
        }
        TotalWeight = Math.Round(weight, 2);
        TotalAmount = Math.Round(amount, 2);
        RefreshConfirmCanExecute();
    }

    private bool CanConfirm()
        => SelectedCustomer != null
        && ItemLines.Count > 0
        && ItemLines.All(l => l.IsValid);

    private void RefreshConfirmCanExecute() => ConfirmCommand.RaiseCanExecuteChanged();

    private async Task ConfirmAsync()
    {
        if (!CanConfirm()) return;

        var record = new DeliveryRecord
        {
            CustomerId   = SelectedCustomer!.Id,
            CustomerName = SelectedCustomer.Name,
            OperatorId   = _user.CurrentUser?.Id ?? 0,
            OperatorName = _user.CurrentUser?.Username ?? string.Empty,
            DeliveryTime = DeliveryTime,
            Remark = string.IsNullOrWhiteSpace(Remark) ? null : Remark.Trim(),
            Items = ItemLines.Select(l =>
            {
                double wKg = l.WeightKg(WeightUnit);
                return new DeliveryItem
                {
                    GoodsCategoryId = l.SelectedGoods!.Id,
                    GoodsName       = l.SelectedGoods.Name,
                    Weight          = wKg,
                    PricePerUnit    = l.UnitPrice,
                    Amount          = l.Amount
                };
            }).ToList()
        };

        await _delivery.AddAsync(record);
        Succeeded = true;
        CloseWindow?.Invoke();
    }
}
