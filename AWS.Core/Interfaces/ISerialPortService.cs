using AWS.Core.Enums;
using AWS.Core.Models;

namespace AWS.Core.Interfaces;

public interface ISerialPortService
{
    event EventHandler<WeightReading>? WeightReceived;
    bool IsConnected { get; }
    bool IsSimulationMode { get; }
    IReadOnlyList<(string PortName, WeighMode Mode)> ConnectedDevices { get; }
    void ConnectAll(IEnumerable<SerialPortConfig> configs);
    void Disconnect();
    void StartSimulation();
    void StopSimulation();
    string[] GetAvailablePorts();
}
