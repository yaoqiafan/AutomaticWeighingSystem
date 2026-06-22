using AWS.Core.Models;

namespace AWS.Core.Interfaces;

public interface ISerialPortService
{
    event EventHandler<WeightReading>? WeightReceived;
    bool IsConnected { get; }
    bool IsSimulationMode { get; }
    void Connect(string portName, int baudRate = 9600);
    void Disconnect();
    void StartSimulation();
    void StopSimulation();
    string[] GetAvailablePorts();
}
