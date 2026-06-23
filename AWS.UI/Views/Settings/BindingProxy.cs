using System.Windows;

namespace AWS.UI.Views.Settings;

// 用于在 DataTemplate 中绑定父级 DataContext（VM 层命令）
public class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    public object Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
}
