using System.IO;
using System.IO.Ports;
using System.Text;

namespace AWS.Services;

/// <summary>
/// 上海耀华 XK3190-A9+ 称重显示器串口驱动（连续发送方式 tF=0）。
/// 帧格式（共12字节）：
///   [0]02(STX) [1]符号(+/-) [2..7]6位ASCII数字 [8]小数点位置(0~4,ASCII)
///   [9]校验高4位 [10]校验低4位 [11]03(ETX)
/// 校验 = byte[1]^byte[2]^...^byte[8]，按 nibble 转 ASCII 后与 [9][10] 比对。
/// 通讯参数需与仪表"打印设置→密码98"中设置一致：8数据位、1停止位、无校验。
/// </summary>
public sealed class Xk3190A9Reader : IDisposable
{
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;
    private const int FrameLength = 12;

    private readonly SerialPort _port;
    private readonly List<byte> _buffer = new(FrameLength);
    private readonly CancellationTokenSource _cts = new();
    private Task? _readTask;

    /// <summary>解析成功时触发，参数为净/毛重当前显示值（已按小数点位置换算）。</summary>
    public event Action<decimal>? WeightReceived;

    /// <summary>校验失败、帧长异常或串口IO异常时触发，便于记录日志。</summary>
    public event Action<string>? FrameError;

    public Xk3190A9Reader(string portName, int baudRate = 9600)
    {
        // 数据位组成：1起始位+8数据位(ASCII)+1停止位=10位，无奇偶校验
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };
    }

    public void Start()
    {
        if (_readTask is not null)
            throw new InvalidOperationException("已经启动，请勿重复调用 Start。");

        _port.Open();
        _readTask = Task.Run(() => ReadLoop(_cts.Token), _cts.Token);
    }

    private void ReadLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // 直接用 SerialPort.ReadByte()：阻塞到 ReadTimeout，超时抛 TimeoutException。
                // 波特率低(≤9600)、帧短(12字节)，逐字节阻塞读开销可忽略。
                int data = _port.ReadByte();
                if (data == -1)
                    continue; // 理论上 ReadByte 不会返回-1（超时是抛异常），仅做防御

                byte b = (byte)data;

                if (b == Stx)
                {
                    // 收到帧头：无论之前缓冲区状态如何，都重新开始一帧
                    _buffer.Clear();
                    _buffer.Add(b);
                    continue;
                }

                if (_buffer.Count == 0)
                    continue; // 尚未收到帧头之前的杂散字节直接丢弃

                _buffer.Add(b);

                if (b == Etx)
                {
                    TryParseFrame(_buffer.ToArray());
                    _buffer.Clear();
                }
                else if (_buffer.Count > FrameLength)
                {
                    // 超长仍未收到ETX，视为脏数据，丢弃重新等待STX
                    FrameError?.Invoke("帧超长未收到结束符，已丢弃");
                    _buffer.Clear();
                }
            }
            catch (TimeoutException)
            {
                // 正常空闲超时，继续等待下一字节
            }
            catch (IOException ex) when (!token.IsCancellationRequested)
            {
                FrameError?.Invoke($"串口IO异常: {ex.Message}");
            }
            catch (InvalidOperationException) when (token.IsCancellationRequested)
            {
                // 端口在 Dispose 过程中被关闭，正常退出
                break;
            }
        }
    }

    private void TryParseFrame(byte[] frame)
    {
        if (frame.Length != FrameLength)
        {
            FrameError?.Invoke($"帧长度异常: 期望{FrameLength}，实际{frame.Length}");
            return;
        }

        byte xorHigh = frame[9];
        byte xorLow = frame[10];

        byte computed = 0;
        for (int i = 1; i <= 8; i++) // 符号位+6位数字+小数点位 共8字节参与校验
            computed ^= frame[i];

        if (!VerifyChecksum(computed, xorHigh, xorLow))
        {
            FrameError?.Invoke("校验失败，数据可能被干扰");
            return;
        }

        char sign = (char)frame[1];
        string digits = Encoding.ASCII.GetString(frame, 2, 6);
        int dp = frame[8] - '0'; // 小数点位置：从右往左数第几位（0~4）

        if (!int.TryParse(digits, out int raw))
        {
            FrameError?.Invoke($"数据位非数字: {digits}");
            return;
        }

        decimal value = raw / (decimal)Math.Pow(10, dp);
        if (sign == '-') value = -value;

        WeightReceived?.Invoke(value);
    }

    private static bool VerifyChecksum(byte computed, byte high, byte low)
    {
        byte expectedHigh = NibbleToAscii((byte)(computed >> 4));
        byte expectedLow = NibbleToAscii((byte)(computed & 0x0F));
        return expectedHigh == high && expectedLow == low;
    }

    // 异或结果按手册规则编码：<=9 加0x30变数字字符，>9 加0x37变A~F字母字符
    private static byte NibbleToAscii(byte nibble) =>
        (byte)(nibble <= 9 ? nibble + 0x30 : nibble + 0x37);

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // 任务取消产生的异常，关闭流程中可忽略
        }

        if (_port.IsOpen)
            _port.Close();

        _port.Dispose();
        _cts.Dispose();
    }
}
