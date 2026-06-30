using System.Windows;

namespace AWS.UI.Views.Charts;

public partial class DeliveryDetailWindow : PF.UI.Controls.Window
{
    public DeliveryDetailWindow() => InitializeComponent();

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
