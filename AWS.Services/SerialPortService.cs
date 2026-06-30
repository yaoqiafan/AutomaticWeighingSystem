using AWS.Core.Enums;
using AWS.Core.Interfaces;
using AWS.Core.Models;
using System.IO.Ports;
using System.Timers;

namespace AWS.Services;

public class SerialPortService : ISerialPortService, IDisposable
{
    private const int StableWindowSize = 5;
    private const decimal StableThreshold = 1.0m;

    private readonly List<ReaderEntry> _readers = [];
    private System.Timers.Timer? _simTimer;
    private double _simBaseWeight = 8000.0;
    private int _simTick;
    private bool _disposed;

    public event EventHandler<WeightReading>? WeightReceived;

    public bool IsConnected => _readers.Count > 0 || IsSimulationMode;
    public bool IsSimulationMode => _simTimer != null;

    public IReadOnlyList<(string PortName, WeighMode Mode)> ConnectedDevices
        => _readers.Select(r => (r.PortName, r.Mode)).ToList();

    public void ConnectAll(IEnumerable<SerialPortConfig> configs)
    {
        StopSimulation();
        DisconnectAll();

        foreach (var cfg in configs)
        {
            IWeightSource source = cfg.IsSimulationMode
                ? new SimulatedWeightSource()
                : new RealWeightSource(cfg.PortName, cfg.BaudRate);

            var entry = new ReaderEntry(cfg.PortName, cfg.WeighMode, source);
            source.WeightReceived += (v, stable) => entry.OnRawValue(v, stable, FireWeightReceived);
            source.Start();
            _readers.Add(entry);
        }
    }

    public void Disconnect()
    {
        StopSimulation();
        DisconnectAll();
    }

    private void DisconnectAll()
    {
        foreach (var e in _readers)
            e.Dispose();
        _readers.Clear();
    }

    private void FireWeightReceived(double value, bool isStable, WeighMode mode, string portName, bool isSimulation)
        => WeightReceived?.Invoke(this,
            new WeightReading(value, isStable, DateTime.Now, mode, portName, isSimulation));

    public void StartSimulation()
    {
        DisconnectAll();
        if (_simTimer != null) return;

        _simBaseWeight = Random.Shared.Next(5000, 20000);
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
            _simBaseWeight = Random.Shared.Next(5000, 20000);

        int phase = _simTick % 40;
        double value;
        bool isStable;

        if (phase < 10)
        {
            value = _simBaseWeight + (Random.Shared.NextDouble() - 0.5) * 400;
            isStable = false;
        }
        else
        {
            value = _simBaseWeight + (Random.Shared.NextDouble() - 0.5) * 6;
            value = Math.Round(value / 0.5) * 0.5;
            isStable = true;
        }

        value = Math.Max(0, value);
        WeightReceived?.Invoke(this, new WeightReading(value, isStable, DateTime.Now, WeighMode.Both, "模拟", true));
    }

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public void Dispose()
    {
        if (_disposed) return;
        StopSimulation();
        DisconnectAll();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    // ── 权重数据源抽象（真实串口 / 软件模拟） ─────────────────────────
    // isStable: null = 由调用方通过滑窗计算；非null = 数据源直接给出结论（模拟模式）
    private interface IWeightSource : IDisposable
    {
        event Action<decimal, bool?>? WeightReceived;
        bool IsSimulation { get; }
        void Start();
    }

    private sealed class RealWeightSource(string portName, int baudRate) : IWeightSource
    {
        private readonly Xk3190A9Reader _reader = new(portName, baudRate);

        public bool IsSimulation => false;
        public event Action<decimal, bool?>? WeightReceived;

        public void Start()
        {
            _reader.WeightReceived += v => WeightReceived?.Invoke(v, null);
            _reader.Start();
        }

        public void Dispose() => _reader.Dispose();
    }

    private sealed class SimulatedWeightSource : IWeightSource
    {
        private System.Timers.Timer? _timer;
        private double _baseWeight = Random.Shared.Next(5000, 20000);
        private int _tick;

        public bool IsSimulation => true;
        public event Action<decimal, bool?>? WeightReceived;

        public void Start()
        {
            _timer = new System.Timers.Timer(500);
            _timer.Elapsed += OnTick;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private void OnTick(object? sender, ElapsedEventArgs e)
        {
            _tick++;
            if (_tick % 40 == 0) _baseWeight = Random.Shared.Next(5000, 20000);

            int phase = _tick % 40;
            double value;
            bool isStable;

            if (phase < 10)
            {
                value = _baseWeight + (Random.Shared.NextDouble() - 0.5) * 400;
                isStable = false;
            }
            else
            {
                value = _baseWeight + (Random.Shared.NextDouble() - 0.5) * 6;
                value = Math.Round(value / 0.5) * 0.5;
                isStable = true;
            }

            WeightReceived?.Invoke((decimal)Math.Max(0, value), isStable);
        }

        public void Dispose()
        {
            if (_timer is null) return;
            _timer.Stop();
            _timer.Elapsed -= OnTick;
            _timer.Dispose();
            _timer = null;
        }
    }

    // ── 封装单个数据源及其稳定性滑窗 ─────────────────────────────────
    private sealed class ReaderEntry : IDisposable
    {
        private readonly Queue<decimal> _buf = new();
        private readonly IWeightSource _source;

        public string PortName { get; }
        public WeighMode Mode { get; }
        public bool IsSimulation => _source.IsSimulation;

        public ReaderEntry(string portName, WeighMode mode, IWeightSource source)
        {
            PortName = portName;
            Mode = mode;
            _source = source;
        }

        // providedStable != null  → 模拟源直接给出稳定标志，跳过滑窗
        // providedStable == null  → 真实串口，走滑窗计算
        public void OnRawValue(decimal value, bool? providedStable,
            Action<double, bool, WeighMode, string, bool> fire)
        {
            bool isStable;
            if (providedStable.HasValue)
            {
                isStable = providedStable.Value;
            }
            else
            {
                _buf.Enqueue(value);
                if (_buf.Count > StableWindowSize) _buf.Dequeue();
                isStable = _buf.Count == StableWindowSize
                           && _buf.Max() - _buf.Min() < StableThreshold;
            }

            fire((double)value, isStable, Mode, PortName, IsSimulation);
        }

        public void Dispose() => _source.Dispose();
    }
}
