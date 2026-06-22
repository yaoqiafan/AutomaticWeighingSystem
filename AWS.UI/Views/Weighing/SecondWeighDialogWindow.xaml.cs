using AWS.UI.ViewModels.Weighing;

namespace AWS.UI.Views.Weighing;

public partial class SecondWeighDialogWindow : System.Windows.Window
{
    public SecondWeighDialogWindow(SecondWeighDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseWindow = () =>
        {
            DialogResult = vm.Succeeded;
            Close();
        };
        Closed += (_, _) => vm.Detach();
    }
}
