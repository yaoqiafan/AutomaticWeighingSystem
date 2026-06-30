using AWS.Core.Models;

namespace AWS.Core.Interfaces;

public interface ICameraService : IDisposable
{
    bool IsLoggedIn { get; }
    event EventHandler LoginStatusChanged;

    bool Login(string ip, int port, string user, string pwd);
    void Logout();

    IReadOnlyList<ChannelInfo> GetChannels();

    void StartPreview(int channel, IntPtr hwnd);
    void StopPreview();

    Task<string?> CaptureJpegAsync(int channel, string savePath);
}
