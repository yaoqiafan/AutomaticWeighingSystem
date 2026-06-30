namespace AWS.Core.Interfaces;

public record DiskStatus(double FreePercent, bool BelowThreshold, string DriveName);

public interface IImageStorageService
{
    string BuildPath(string ticketNo, string suffix);
    DiskStatus GetDiskStatus();
    void RunMaintenance();
}
