using AWS.Core.Entities;
using AWS.Core.Interfaces;

namespace AWS.Services;

public class CloudSyncService : ICloudSyncService
{
    public bool IsEnabled => false;

    public Task SyncAsync(WeighingArchiveRecord record)
        => Task.CompletedTask;
}
