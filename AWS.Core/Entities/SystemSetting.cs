namespace AWS.Core.Entities;

public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public static class SettingKeys
{
    public const string SerialPortName = "SerialPortName";
    public const string BaudRate = "BaudRate";
    public const string CompanyName = "CompanyName";
    public const string SkinType = "SkinType";
    public const string CloudSyncEnabled = "CloudSyncEnabled";
    public const string CloudSyncUrl = "CloudSyncUrl";
    public const string SerialPortEnabled = "SerialPortEnabled";
    public const string SerialPortConfigs = "SerialPortConfigs";  // JSON array of SerialPortConfig
    public const string WeightUnit        = "WeightUnit";  // "kg" or "ton"

    // 摄像参数
    public const string CameraIp           = "CameraIp";
    public const string CameraPort         = "CameraPort";
    public const string CameraUser         = "CameraUser";
    public const string CameraPassword     = "CameraPassword";
    public const string CaptureChannel     = "CaptureChannel";

    // 存储参数
    public const string ImageStoragePath   = "ImageStoragePath";
    public const string DiskWarningPercent = "DiskWarningPercent";
    public const string AutoDeleteKeepDays = "AutoDeleteKeepDays";
}
