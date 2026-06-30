using AWS.Core.Interfaces;
using AWS.Core.Models;
using AWS.Services.HCNetSDK;

namespace AWS.Services;

public class HikvisionCameraService : ICameraService
{
    private int _userId = -1;
    private int _realHandle = -1;
    private NET_DVR_DEVICEINFO_V30 _deviceInfo;
    private readonly object _lock = new();
    private bool _disposed;

    public bool IsLoggedIn => _userId >= 0;
    public event EventHandler? LoginStatusChanged;

    public bool Login(string ip, int port, string user, string pwd)
    {
        lock (_lock)
        {
            if (_userId >= 0) Logout();
            _deviceInfo = new NET_DVR_DEVICEINFO_V30();
            _userId = HCNetSDKApi.NET_DVR_Login_V30(ip, (ushort)port, user, pwd, ref _deviceInfo);
            LoginStatusChanged?.Invoke(this, EventArgs.Empty);
            return _userId >= 0;
        }
    }

    public void Logout()
    {
        lock (_lock)
        {
            StopPreview();
            if (_userId >= 0)
            {
                HCNetSDKApi.NET_DVR_Logout(_userId);
                _userId = -1;
            }
            LoginStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<ChannelInfo> GetChannels()
    {
        if (!IsLoggedIn) return [];
        var list = new List<ChannelInfo>();

        // 模拟通道
        int analogCount = _deviceInfo.byChanNum;
        int analogStart = _deviceInfo.byStartChan == 0 ? 1 : _deviceInfo.byStartChan;
        for (int i = 0; i < analogCount; i++)
            list.Add(new ChannelInfo(analogStart + i, $"模拟通道{analogStart + i:D2}", false));

        // IP通道
        int ipCount    = _deviceInfo.byIPChanNum + _deviceInfo.byHighDChanNum * 256;
        int ipStart    = _deviceInfo.byStartDChan == 0 ? analogStart + analogCount : _deviceInfo.byStartDChan;
        for (int i = 0; i < ipCount; i++)
            list.Add(new ChannelInfo(ipStart + i, $"IP通道{i + 1:D2}", true));

        return list;
    }

    public void StartPreview(int channel, IntPtr hwnd)
    {
        lock (_lock)
        {
            StopPreview();
            if (!IsLoggedIn || hwnd == IntPtr.Zero) return;

            var info = new NET_DVR_PREVIEWINFO
            {
                lChannel     = channel,
                dwStreamType = 0,
                dwLinkMode   = 0,
                hPlayWnd     = hwnd,
                bBlocked     = true,
                bPassbackRecord = false,
                byPreviewMode = 0,
                byProtoType   = 0,
                byRes         = new byte[2]
            };
            _realHandle = HCNetSDKApi.NET_DVR_RealPlay_V40(_userId, ref info, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void StopPreview()
    {
        lock (_lock)
        {
            if (_realHandle >= 0)
            {
                HCNetSDKApi.NET_DVR_StopRealPlay(_realHandle);
                _realHandle = -1;
            }
        }
    }

    public async Task<string?> CaptureJpegAsync(int channel, string savePath)
    {
        if (!IsLoggedIn) return null;

        return await Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                var para = new NET_DVR_JPEGPARA { wPicSize = 0xFF, wPicQuality = 0 };
                var buf = new byte[400_000];
                uint actualSize = 0;

                bool ok = HCNetSDKApi.NET_DVR_CaptureJPEGPicture_NEW(
                    _userId, channel, ref para, buf, (uint)buf.Length, ref actualSize);

                if (!ok || actualSize == 0) return null;

                File.WriteAllBytes(savePath, buf[..(int)actualSize]);
                return savePath;
            }
            catch { return null; }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Logout();
        GC.SuppressFinalize(this);
    }
}
