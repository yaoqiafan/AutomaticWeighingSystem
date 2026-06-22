using AWS.Core.Models;
using AWS.Shell.ViewModels;
using PF.UI.Controls;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace AWS.Shell.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.LogEntries.CollectionChanged += OnLogEntriesChanged;

        // 默认展开第一个菜单组
        if (SideNav.Items.Count > 0 && SideNav.Items[0] is PF.UI.Controls.SideMenuItem firstGroup)
            firstGroup.SwitchPanelArea(true);
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            LogScrollViewer.ScrollToBottom();
    }
}
