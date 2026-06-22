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
    public const string DefaultPricePerKg = "DefaultPricePerKg";
    public const string SerialPortEnabled = "SerialPortEnabled";
}
