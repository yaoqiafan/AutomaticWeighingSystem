using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;

namespace AWS.UI.ViewModels.BasicData;

public class BasicDataViewModel : BindableBase, INavigationAware
{
    private readonly AwsDbContext _db;
    private readonly ILogService _log;

    public ObservableCollection<Customer> Customers { get; } = [];
    public ObservableCollection<GoodsCategory> GoodsCategories { get; } = [];
    public ObservableCollection<Vehicle> Vehicles { get; } = [];

    private Customer? _selectedCustomer;
    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set { SetProperty(ref _selectedCustomer, value); DeleteCustomerCommand.RaiseCanExecuteChanged(); }
    }

    private GoodsCategory? _selectedGoods;
    public GoodsCategory? SelectedGoods
    {
        get => _selectedGoods;
        set { SetProperty(ref _selectedGoods, value); DeleteGoodsCommand.RaiseCanExecuteChanged(); }
    }

    private Vehicle? _selectedVehicle;
    public Vehicle? SelectedVehicle
    {
        get => _selectedVehicle;
        set { SetProperty(ref _selectedVehicle, value); DeleteVehicleCommand.RaiseCanExecuteChanged(); }
    }

    public DelegateCommand AddCustomerCommand { get; }
    public DelegateCommand DeleteCustomerCommand { get; }
    public DelegateCommand AddGoodsCommand { get; }
    public DelegateCommand DeleteGoodsCommand { get; }
    public DelegateCommand AddVehicleCommand { get; }
    public DelegateCommand DeleteVehicleCommand { get; }
    public DelegateCommand SaveAllCommand { get; }

    public BasicDataViewModel(AwsDbContext db, ILogService log)
    {
        _db = db;
        _log = log;

        AddCustomerCommand = new DelegateCommand(() => AddCustomer());
        DeleteCustomerCommand = new DelegateCommand(DeleteCustomer, () => SelectedCustomer != null);
        AddGoodsCommand = new DelegateCommand(() => AddGoods());
        DeleteGoodsCommand = new DelegateCommand(DeleteGoods, () => SelectedGoods != null);
        AddVehicleCommand = new DelegateCommand(() => AddVehicle());
        DeleteVehicleCommand = new DelegateCommand(DeleteVehicle, () => SelectedVehicle != null);
        SaveAllCommand = new DelegateCommand(SaveAll);
    }

    public async void OnNavigatedTo(NavigationContext ctx)
    {
        await LoadAllAsync();
    }

    public void OnNavigatedFrom(NavigationContext ctx) { }
    public bool IsNavigationTarget(NavigationContext ctx) => false;

    private async Task LoadAllAsync()
    {
        var customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
        var goods = await _db.GoodsCategories.OrderBy(g => g.Name).ToListAsync();
        var vehicles = await _db.Vehicles.OrderBy(v => v.PlateNumber).ToListAsync();

        Customers.Clear();
        foreach (var c in customers) Customers.Add(c);
        GoodsCategories.Clear();
        foreach (var g in goods) GoodsCategories.Add(g);
        Vehicles.Clear();
        foreach (var v in vehicles) Vehicles.Add(v);
    }

    private void AddCustomer()
    {
        var c = new Customer { Name = "新客户", IsActive = true, CreatedAt = DateTime.Now };
        _db.Customers.Add(c);
        Customers.Add(c);
        SelectedCustomer = c;
    }

    private void DeleteCustomer()
    {
        if (SelectedCustomer == null) return;
        _db.Customers.Remove(SelectedCustomer);
        Customers.Remove(SelectedCustomer);
    }

    private void AddGoods()
    {
        var g = new GoodsCategory { Name = "新货物", IsActive = true, CreatedAt = DateTime.Now };
        _db.GoodsCategories.Add(g);
        GoodsCategories.Add(g);
        SelectedGoods = g;
    }

    private void DeleteGoods()
    {
        if (SelectedGoods == null) return;
        _db.GoodsCategories.Remove(SelectedGoods);
        GoodsCategories.Remove(SelectedGoods);
    }

    private void AddVehicle()
    {
        var v = new Vehicle { PlateNumber = "新车牌", IsActive = true, CreatedAt = DateTime.Now };
        _db.Vehicles.Add(v);
        Vehicles.Add(v);
        SelectedVehicle = v;
    }

    private void DeleteVehicle()
    {
        if (SelectedVehicle == null) return;
        _db.Vehicles.Remove(SelectedVehicle);
        Vehicles.Remove(SelectedVehicle);
    }

    private void SaveAll()
    {
        try
        {
            _db.SaveChanges();
            _log.Info($"基础数据已保存（客户{Customers.Count}/货物{GoodsCategories.Count}/车辆{Vehicles.Count}）", "基础数据");
        }
        catch (Exception ex)
        {
            _log.Error($"保存失败：{ex.Message}", "基础数据");
        }
    }
}
