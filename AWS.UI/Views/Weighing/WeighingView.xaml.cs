using AWS.Core.Entities;
using AWS.UI.ViewModels.Weighing;
using System.Windows;
using System.Windows.Controls;

namespace AWS.UI.Views.Weighing;

public partial class WeighingView : UserControl
{
    public WeighingView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WeighingViewModel vm)
        {
            vm.OpenSecondWeighDialog = OpenSecondWeighDialogAsync;
            vm.OpenEditQueueDialog   = OpenEditQueueDialogAsync;
            vm.SetPreviewHandle(VideoPanel.Handle);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WeighingViewModel vm)
            vm.ClearPreviewHandle();
    }

    private Task<bool> OpenSecondWeighDialogAsync(WeighingQueue item, double defaultPrice)
    {
        if (DataContext is not WeighingViewModel vm) return Task.FromResult(false);

        var dialogVm = new SecondWeighDialogViewModel(
            item,
            defaultPrice,
            vm.SerialPortService,
            vm.CameraService,
            vm.ImageStorage,
            vm.DefaultCaptureChannel,
            (id, secondWeight, price, imgPath) => vm.ArchiveItemAsync(id, secondWeight, price, imgPath),
            vm.LogService);

        var dialog = new SecondWeighDialogWindow(dialogVm)
        {
            Owner = Application.Current.MainWindow
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
            Owner = Application.Current.MainWindow
        };
        return Task.FromResult(dialog.ShowDialog() == true);
    }
}
