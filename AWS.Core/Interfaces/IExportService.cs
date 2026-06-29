using AWS.Core.Entities;

namespace AWS.Core.Interfaces;

public interface IExportService
{
    Task ExportToExcelAsync(IEnumerable<WeighingArchiveRecord> records, string filePath);
    Task ExportDeliveryToExcelAsync(IEnumerable<DeliveryRecord> records, string filePath);
}
