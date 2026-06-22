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
            vm.OpenSecondWeighDialog = OpenSecondWeighDialogAsync;
    }

    private Task<bool> OpenSecondWeighDialogAsync(WeighingQueue item, double defaultPrice)
    {
        if (DataContext is not WeighingViewModel vm) return Task.FromResult(false);

        var dialogVm = new SecondWeighDialogViewModel(
            item,
            defaultPrice,
            vm.SerialPortService,
            (id, secondWeight, price) => vm.ArchiveItemAsync(id, secondWeight, price));

        var dialog = new SecondWeighDialogWindow(dialogVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        return Task.FromResult(dialog.ShowDialog() == true);
    }
}
