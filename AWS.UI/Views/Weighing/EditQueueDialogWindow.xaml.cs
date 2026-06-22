using AWS.UI.ViewModels.Weighing;

namespace AWS.UI.Views.Weighing;

public partial class EditQueueDialogWindow : PF.UI.Controls.Window
{
    public EditQueueDialogWindow(EditQueueDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseWindow = () =>
        {
            DialogResult = vm.Succeeded;
            Close();
        };
    }
}
