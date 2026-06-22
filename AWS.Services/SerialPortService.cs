using AWS.Core.Interfaces;
using AWS.Core.Models;
using System.IO.Ports;
using System.Timers;

namespace AWS.Services;

public class SerialPortService : ISerialPortService, IDisposable
{
    private SerialPort? _port;
    private readonly List<byte> _buffer = [];
    private bool _disposed;

    // 模拟模式
    private System.Timers.Timer? _simTimer;
    private double _simBaseWeight = 8000.0;
    private int _simTick;
    private readonly Random _rng = new();

    public event EventHandler<WeightReading>? WeightReceived;
    public bool IsConnected => _port?.IsOpen ?? IsSimulationMode;
    public bool IsSimulationMode => _simTimer != null;

    public void Connect(string portName, int baudRate = 9600)
    {
        StopSimulation();
        Disconnect();
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 500
        };
        _port.DataReceived += OnDataReceived;
        _port.Open();
        _buffer.Clear();
    }

    public void Disconnect()
    {
        if (_port is null) return;
        _port.DataReceived -= OnDataReceived;
        if (_port.IsOpen) _port.Close();
        _port.Dispose();
        _port = null;
        _buffer.Clear();
    }

    public void StartSimulation()
    {
        Disconnect();
        if (_simTimer != null) return;

        // 随机初始基准重量 5000~20000 kg
        _simBaseWeight = _rng.Next(5000, 20000);
        _simTick = 0;

        _simTimer = new System.Timers.Timer(500);
        _simTimer.Elapsed += OnSimTimerElapsed;
        _simTimer.AutoReset = true;
        _simTimer.Start();
    }

    public void StopSimulation()
    {
        if (_simTimer is null) return;
        _simTimer.Stop();
        _simTimer.Elapsed -= OnSimTimerElapsed;
        _simTimer.Dispose();
        _simTimer = null;
    }

    private void OnSimTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _simTick++;

        // 每 40 tick（20 秒）随机换一个基准重量，模拟新车辆
        if (_simTick % 40 == 0)
            _simBaseWeight = _rng.Next(5000, 20000);

        // tick 0~10 剧烈波动（不稳定），tick 11~35 小幅波动（稳定）
        int phase = _simTick % 40;
        bool isStable;
        double value;

        if (phase < 10)
        {
            // 不稳定阶段：±200 kg 随机扰动
            value = _simBaseWeight + (_rng.NextDouble() - 0.5) * 400;
            isStable = false;
        }
        else
        {
            // 稳定阶段：±3 kg 小幅抖动，四舍五入到 0.5 kg
            value = _simBaseWeight + (_rng.NextDouble() - 0.5) * 6;
            value = Math.Round(value / 0.5) * 0.5;
            isStable = true;
        }

        value = Math.Max(0, value);
        WeightReceived?.Invoke(this, new WeightReading(value, isStable, DateTime.Now));
    }

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is null || !_port.IsOpen) return;
        try
        {
            int count = _port.BytesToRead;
            var bytes = new byte[count];
            _port.Read(bytes, 0, count);
            _buffer.AddRange(bytes);
            TryParseFrames();
        }
        catch { /* 单帧读取异常时忽略，等待下一帧 */ }
    }

    private void TryParseFrames()
    {
        // XK3190-A9+ 帧: STX(0x02) + 状态字节 + 6字节ASCII重量 + ETX(0x03) + CR + LF = 10字节
        while (_buffer.Count >= 10)
        {
            int stxIdx = _buffer.IndexOf(0x02);
            if (stxIdx < 0) { _buffer.Clear(); return; }
            if (stxIdx > 0) { _buffer.RemoveRange(0, stxIdx); }
            if (_buffer.Count < 10) return;

            if (_buffer[8] != 0x03)
            {
                _buffer.RemoveAt(0);
                continue;
            }

            var frame = _buffer.Take(10).ToArray();
            _buffer.RemoveRange(0, 10);

            var reading = ParseFrame(frame);
            if (reading != null)
                WeightReceived?.Invoke(this, reading);
        }
    }

    private static WeightReading? ParseFrame(byte[] frame)
    {
        // 状态字节: Bit7=极性(0=正), Bit6=稳定(1=稳定)
        byte status = frame[1];
        bool isNegative = (status & 0x80) != 0;
        bool isStable = (status & 0x40) != 0;

        if (isNegative) return null;

        var weightStr = System.Text.Encoding.ASCII.GetString(frame, 2, 6).Trim();
        if (!double.TryParse(weightStr, out double value)) return null;

        return new WeightReading(value, isStable, DateTime.Now);
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopSimulation();
        Disconnect();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
