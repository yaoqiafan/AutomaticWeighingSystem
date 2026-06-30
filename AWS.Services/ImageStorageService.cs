using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;

namespace AWS.Services;

public class ImageStorageService : IImageStorageService
{
    private readonly AwsDbContext _db;

    public ImageStorageService(AwsDbContext db) => _db = db;

    private string GetBasePath()
        => _db.SystemSettings.Find(SettingKeys.ImageStoragePath)?.Value
           ?? @"D:\WeighImages\";

    private double GetWarningPercent()
        => double.TryParse(_db.SystemSettings.Find(SettingKeys.DiskWarningPercent)?.Value, out var v) ? v : 20;

    private int GetKeepDays()
        => int.TryParse(_db.SystemSettings.Find(SettingKeys.AutoDeleteKeepDays)?.Value, out var v) ? v : 90;

    public string BuildPath(string ticketNo, string suffix)
    {
        var dir = Path.Combine(GetBasePath(), DateTime.Today.ToString("yyyyMMdd"), ticketNo);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{suffix}.jpg");
    }

    public DiskStatus GetDiskStatus()
    {
        try
        {
            var root = Path.GetPathRoot(GetBasePath()) ?? "C:\\";
            var drive = new DriveInfo(root);
            double pct = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
            return new DiskStatus(pct, pct < GetWarningPercent(), drive.Name);
        }
        catch
        {
            return new DiskStatus(100, false, "?");
        }
    }

    public void RunMaintenance()
    {
        var basePath   = GetBasePath();
        var keepDays   = GetKeepDays();
        var threshold  = GetWarningPercent();
        var cutoff     = DateTime.Today.AddDays(-keepDays);

        if (!Directory.Exists(basePath)) return;

        // ① 按天数删除
        foreach (var dir in Directory.GetDirectories(basePath))
        {
            var name = Path.GetFileName(dir);
            if (DateTime.TryParseExact(name, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date)
                && date < cutoff)
            {
                TryDeleteDirectory(dir);
            }
        }

        // ② 磁盘兜底删除
        while (true)
        {
            var status = GetDiskStatus();
            if (!status.BelowThreshold) break;

            var oldest = Directory.GetDirectories(basePath)
                .Where(d => DateTime.TryParseExact(Path.GetFileName(d), "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
                .OrderBy(d => d)
                .FirstOrDefault();

            if (oldest == null) break; // 已无可删目录
            TryDeleteDirectory(oldest);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }
}
