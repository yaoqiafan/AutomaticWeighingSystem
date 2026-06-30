using AWS.Core.Enums;

namespace AWS.Core.Models;

public class SerialPortConfig
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public WeighMode WeighMode { get; set; } = WeighMode.Both;
    public bool IsEnabled { get; set; } = true;
    public bool IsSimulationMode { get; set; } = false;
}
