using System.Windows;

namespace AWS.UI.Views.Charts;

public partial class ReceiveDetailWindow : PF.UI.Controls.Window
{
    public ReceiveDetailWindow() => InitializeComponent();

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
