using PF.UI.Controls;
using System.Windows;

namespace AWS.UI.Controls;

/// <summary>
/// 在 PropertyGrid 中为 SerialPortConfigsJson 字符串属性提供"编辑..."按钮编辑器。
/// PropertyGrid 框架会将 SerialPortConfigsEditorControl.ConfigJsonProperty
/// 双向绑定到 SystemParameters.SerialPortConfigsJson。
/// </summary>
public class SerialPortConfigListEditor : PropertyEditorBase
{
    public override FrameworkElement CreateElement(PropertyItem propertyItem)
        => new SerialPortConfigsEditorControl { IsEnabled = !propertyItem.IsReadOnly };

    public override DependencyProperty GetDependencyProperty()
        => SerialPortConfigsEditorControl.ConfigJsonProperty;
}
