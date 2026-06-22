using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AWS.Shell.Controls;

/// <summary>
/// 侧边栏导航项：平铺、无展开/折叠。点击即选中并上抛 Selected 事件。
/// 仅承载 Header（文本）、Icon（图标内容）、Tag（导航目标）。
/// </summary>
public class SidebarItem : Control
{
    static SidebarItem()
    {
        // 默认可聚焦，便于键盘选中
        FocusableProperty.OverrideMetadata(typeof(SidebarItem), new FrameworkPropertyMetadata(true));
    }

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SidebarItem), new PropertyMetadata(string.Empty));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(SidebarItem), new PropertyMetadata(null));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(SidebarItem), new PropertyMetadata(false));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly RoutedEvent SelectedEvent = EventManager.RegisterRoutedEvent(
        "Selected", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SidebarItem));

    public event RoutedEventHandler Selected
    {
        add => AddHandler(SelectedEvent, value);
        remove => RemoveHandler(SelectedEvent, value);
    }

    private bool _pressed;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _pressed = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _pressed = false;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_pressed) return;
        _pressed = false;
        IsSelected = true;
        RaiseEvent(new RoutedEventArgs(SelectedEvent, this));
    }
}

/// <summary>
/// 平铺侧边栏：纵向列表 + 单选。监听子项 Selected 事件，维护唯一选中项并更新 SelectedItem。
/// 完全没有展开/折叠语义——这正是对原 SideMenu 最外层"展开事件拦截"的彻底实现。
/// </summary>
public class Sidebar : ItemsControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(Sidebar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public object SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
        "SelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Sidebar));

    public event RoutedEventHandler SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    private SidebarItem? _selected;

    public Sidebar()
    {
        AddHandler(SidebarItem.SelectedEvent, new RoutedEventHandler(OnItemSelected));
    }

    protected override DependencyObject GetContainerForItemOverride() => new SidebarItem();
    protected override bool IsItemItsOwnContainerOverride(object item) => item is SidebarItem;

    private void OnItemSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not SidebarItem item) return;

        if (_selected != null && !ReferenceEquals(_selected, item))
            _selected.IsSelected = false;

        _selected = item;
        item.IsSelected = true;
        SelectedItem = item;
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Sidebar sidebar) return;
        if (e.NewValue is SidebarItem newItem)
        {
            if (sidebar._selected != null && !ReferenceEquals(sidebar._selected, newItem))
                sidebar._selected.IsSelected = false;
            sidebar._selected = newItem;
            newItem.IsSelected = true;
        }
    }
}
