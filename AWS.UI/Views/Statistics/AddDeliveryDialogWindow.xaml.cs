using AWS.UI.ViewModels.Statistics;

namespace AWS.UI.Views.Statistics;

public partial class AddDeliveryDialogWindow
{
    public AddDeliveryDialogWindow(AddDeliveryDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseWindow = () => { DialogResult = vm.Succeeded; Close(); };
    }
}
