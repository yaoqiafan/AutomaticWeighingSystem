using AWS.Core.Entities;

namespace AWS.Core.Interfaces;

public interface IWeighingService
{
    Task<string> GenerateTicketNoAsync();
    Task<WeighingQueue> CreateInitialEntryAsync(string vehiclePlate, string customerName,
        int? customerId, string goodsName, int? goodsCategoryId,
        double firstWeight, int operatorId, string operatorName, string? remark = null);
    Task SetWaitingAsync(long queueId);
    Task<WeighingArchiveRecord> ArchiveAsync(long queueId, double secondWeight,
        double? pricePerUnit = null);
    Task<List<WeighingQueue>> GetActiveQueueAsync();
    Task DeleteQueueItemAsync(long queueId);
    Task<double[]> GetTodayHourlyNetWeightAsync();
    Task<(double TotalWeight, int Count)> GetTodayStatsAsync();
}
