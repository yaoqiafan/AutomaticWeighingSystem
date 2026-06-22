using AWS.Shell.ViewModels;
using PF.UI.Controls;

namespace AWS.Shell.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.LoginSucceeded += () =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void PwdBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = PwdBox.Password;
    }
}
