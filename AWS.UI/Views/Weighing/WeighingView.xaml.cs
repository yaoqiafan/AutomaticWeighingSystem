using AWS.Core.Entities;
using AWS.UI.ViewModels.Weighing;
using System.Windows.Controls;

namespace AWS.UI.Views.Weighing;

public partial class WeighingView : UserControl
{
    public WeighingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is WeighingViewModel vm)
        {
            vm.OpenSecondWeighDialog = OpenSecondWeighDialogAsync;
            vm.OpenEditQueueDialog = OpenEditQueueDialogAsync;
        }
    }

    private Task<bool> OpenSecondWeighDialogAsync(WeighingQueue item, double defaultPrice)
    {
        if (DataContext is not WeighingViewModel vm) return Task.FromResult(false);

        var dialogVm = new SecondWeighDialogViewModel(
            item,
            defaultPrice,
            vm.SerialPortService,
            (id, secondWeight, price) => vm.ArchiveItemAsync(id, secondWeight, price),
            vm.LogService);

        var dialog = new SecondWeighDialogWindow(dialogVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        return Task.FromResult(dialog.ShowDialog() == true);
    }

    private Task<bool> OpenEditQueueDialogAsync(WeighingQueue item)
    {
        if (DataContext is not WeighingViewModel vm) return Task.FromResult(false);

        var dialogVm = new EditQueueDialogViewModel(
            item,
            (id, plate, customer, goods, remark, weight) =>
                vm.UpdateQueueAsync(id, plate, customer, goods, remark, weight),
            vm.LogService);

        var dialog = new EditQueueDialogWindow(dialogVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        return Task.FromResult(dialog.ShowDialog() == true);
    }
}

