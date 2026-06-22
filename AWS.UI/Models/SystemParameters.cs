using System.ComponentModel;
using PF.UI.Shared.Data;

namespace AWS.UI.Models;

/// <summary>
/// 参数管理 PropertyGrid 绑定的 POCO。
/// 通过 Category / DisplayName / Description 特性驱动 PropertyGrid 自动反射生成编辑器。
/// 属性类型决定编辑器：string→TextBox、int/double→NumericUpDown、bool→Switch、Enum→ComboBox。
/// </summary>
public class SystemParameters
{
    [Category("业务参数")]
    [DisplayName("公司名称")]
    [Description("显示在程序标题栏的公司名称")]
    public string CompanyName { get; set; } = string.Empty;

    [Category("业务参数")]
    [DisplayName("默认单价")]
    [Description("过磅默认单价（元/kg），可被单次操作覆盖")]
    public double DefaultPricePerKg { get; set; }

    [Category("界面参数")]
    [DisplayName("皮肤主题")]
    [Description("深色 / 浅色 / 紫色，保存后即时生效")]
    public SkinType SkinType { get; set; } = SkinType.Dark;

    [Category("串口参数")]
    [DisplayName("启用串口")]
    [Description("关闭后使用模拟数据，便于无设备调试")]
    public bool SerialPortEnabled { get; set; }

    [Category("串口参数")]
    [DisplayName("串口名称")]
    [Description("实际称重仪表连接的串口，如 COM1")]
    public string SerialPortName { get; set; } = "COM1";

    [Category("串口参数")]
    [DisplayName("波特率")]
    [Description("与称重仪表通讯的波特率，常用 9600")]
    public int BaudRate { get; set; } = 9600;

    [Category("云同步")]
    [DisplayName("启用云同步")]
    [Description("开启后将过磅档案上传至云端服务器")]
    public bool CloudSyncEnabled { get; set; }

    [Category("云同步")]
    [DisplayName("同步地址")]
    [Description("云同步服务接口地址（URL）")]
    public string CloudSyncUrl { get; set; } = string.Empty;
}
