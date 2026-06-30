using System.ComponentModel;
using AWS.UI.Controls;
using PF.UI.Shared.Data;

namespace AWS.UI.Models;

/// <summary>
/// 参数管理 PropertyGrid 绑定的 POCO。
/// 属性类型决定默认编辑器：string→TextBox、int/double→NumericUpDown、bool→Switch、Enum→ComboBox。
/// 串口设备列表使用自定义编辑器 SerialPortConfigListEditor。
/// </summary>
public class SystemParameters
{
    [Category("业务参数")]
    [DisplayName("公司名称")]
    [Description("显示在程序标题栏的公司名称")]
    public string CompanyName { get; set; } = string.Empty;

    [Category("界面参数")]
    [DisplayName("皮肤主题")]
    [Description("深色 / 浅色 / 紫色，保存后即时生效")]
    public SkinType SkinType { get; set; } = SkinType.Dark;

    [Category("串口参数")]
    [DisplayName("称重设备列表")]
    [Description("点击\"编辑\"可添加或删除串口设备，并设置每个设备的波特率和称重用途")]
    [Editor(typeof(SerialPortConfigListEditor), typeof(SerialPortConfigListEditor))]
    public string SerialPortConfigsJson { get; set; } = "[]";

    [Category("云同步")]
    [DisplayName("启用云同步")]
    [Description("开启后将过磅档案上传至云端服务器")]
    public bool CloudSyncEnabled { get; set; }

    [Category("云同步")]
    [DisplayName("同步地址")]
    [Description("云同步服务接口地址（URL）")]
    public string CloudSyncUrl { get; set; } = string.Empty;

    [Category("摄像参数")]
    [DisplayName("摄像机IP")]
    [Description("海康威视网络摄像机或NVR的IP地址")]
    public string CameraIp { get; set; } = string.Empty;

    [Category("摄像参数")]
    [DisplayName("端口号")]
    [Description("设备端口号，默认8000")]
    public int CameraPort { get; set; } = 8000;

    [Category("摄像参数")]
    [DisplayName("用户名")]
    [Description("登录摄像机的用户名")]
    public string CameraUser { get; set; } = "admin";

    [Category("摄像参数")]
    [DisplayName("密码")]
    [Description("登录摄像机的密码")]
    public string CameraPassword { get; set; } = string.Empty;

    [Category("摄像参数")]
    [DisplayName("默认抓图通道")]
    [Description("称重时自动抓图使用的通道号（从1开始）")]
    public int CaptureChannel { get; set; } = 1;

    [Category("存储参数")]
    [DisplayName("图片存储根目录")]
    [Description("过磅抓图的本地保存路径，子文件夹按 日期\\磅单号 自动创建")]
    public string ImageStoragePath { get; set; } = @"D:\WeighImages\";

    [Category("存储参数")]
    [DisplayName("磁盘预警阈值(%)")]
    [Description("存图磁盘剩余空间低于此百分比时状态栏告警，默认20")]
    public double DiskWarningPercent { get; set; } = 20;

    [Category("存储参数")]
    [DisplayName("自动删除保留天数")]
    [Description("超过此天数的图片文件夹自动删除；磁盘仍满时继续删最老的，默认90天")]
    public int AutoDeleteKeepDays { get; set; } = 90;
}
