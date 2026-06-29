using AWS.Core.Interfaces;
using AWS.Core.Models;
using System.IO.Ports;
using System.Timers;

namespace AWS.Services;

public class SerialPortService : ISerialPortService, IDisposable
{
    private Xk3190A9Reader? _reader;
    private bool _disposed;

    // 稳定性判断：最近 N 帧极差 < StableThreshold 则判定为稳定
    private const int StableWindowSize = 5;
    private const decimal StableThreshold = 1.0m;
    private readonly Queue<decimal> _recentValues = new();

    // 模拟模式
    private System.Timers.Timer? _simTimer;
    private double _simBaseWeight = 8000.0;
    private int _simTick;
    private readonly Random _rng = new();

    public event EventHandler<WeightReading>? WeightReceived;

    /// <summary>转发 Xk3190A9Reader 的帧错误，可选订阅用于日志。</summary>
    public event Action<string>? FrameError;

    public bool IsConnected => _reader != null || IsSimulationMode;
    public bool IsSimulationMode => _simTimer != null;

    public void Connect(string portName, int baudRate = 9600)
    {
        StopSimulation();
        Disconnect();

        _reader = new Xk3190A9Reader(portName, baudRate);
        _reader.WeightReceived += OnWeightReceived;
        _reader.FrameError += err => FrameError?.Invoke(err);
        _reader.Start();
        _recentValues.Clear();
    }

    public void Disconnect()
    {
        if (_reader is null) return;
        _reader.WeightReceived -= OnWeightReceived;
        _reader.Dispose();
        _reader = null;
        _recentValues.Clear();
    }

    private void OnWeightReceived(decimal value)
    {
        _recentValues.Enqueue(value);
        if (_recentValues.Count > StableWindowSize)
            _recentValues.Dequeue();

        bool isStable = _recentValues.Count == StableWindowSize
                        && _recentValues.Max() - _recentValues.Min() < StableThreshold;

        WeightReceived?.Invoke(this, new WeightReading((double)value, isStable, DateTime.Now));
    }

    public void StartSimulation()
    {
        Disconnect();
        if (_simTimer != null) return;

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

        if (_simTick % 40 == 0)
            _simBaseWeight = _rng.Next(5000, 20000);

        int phase = _simTick % 40;
        bool isStable;
        double value;

        if (phase < 10)
        {
            value = _simBaseWeight + (_rng.NextDouble() - 0.5) * 400;
            isStable = false;
        }
        else
        {
            value = _simBaseWeight + (_rng.NextDouble() - 0.5) * 6;
            value = Math.Round(value / 0.5) * 0.5;
            isStable = true;
        }

        value = Math.Max(0, value);
        WeightReceived?.Invoke(this, new WeightReading(value, isStable, DateTime.Now));
    }

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public void Dispose()
    {
        if (_disposed) return;
        StopSimulation();
        Disconnect();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
