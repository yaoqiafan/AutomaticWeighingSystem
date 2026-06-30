using AWS.UI.ViewModels.Camera;
using System.Windows;
using System.Windows.Controls;

namespace AWS.UI.Views.Camera;

public partial class CameraManageView : UserControl
{
    public CameraManageView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CameraManageViewModel vm)
            vm.SetPreviewHandle(VideoPanel.Handle);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CameraManageViewModel vm)
            vm.ClearPreviewHandle();
    }
}
