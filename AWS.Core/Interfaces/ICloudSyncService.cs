using AWS.Core.Entities;

namespace AWS.Core.Interfaces;

public interface ICloudSyncService
{
    bool IsEnabled { get; }
    Task SyncAsync(WeighingArchiveRecord record);
}
