using AWS.Core.Entities;

namespace AWS.Core.Interfaces;

public interface IDeliveryService
{
    Task<string> AddAsync(DeliveryRecord record);
    Task DeleteAsync(long id);

    Task<List<DeliveryRecord>> GetTodayRecordsAsync();
    Task<double> GetTodayTotalWeightAsync();
    Task<double> GetTodayTotalAmountAsync();

    Task<List<DeliveryRecord>> QueryAsync(DateTime? from, DateTime? to,
        string? customerName = null, string? goodsName = null);

    Task<(double TotalWeight, double TotalAmount, int Count)> GetQuerySummaryAsync(
        DateTime? from, DateTime? to,
        string? customerName = null, string? goodsName = null);

    /// <summary>返回各货物品类每天库存快照（收货净重 - 送货重量），用于库存趋势折线图。</summary>
    Task<Dictionary<string, List<(DateTime Date, double Stock)>>> GetInventoryTrendAsync(
        DateTime from, DateTime to, IEnumerable<string>? categories = null);

    /// <summary>返回各货物品类当前库存（全部历史）。</summary>
    Task<Dictionary<string, double>> GetCurrentInventoryAsync();

    /// <summary>返回今日每小时送货重量（24 元素数组）。</summary>
    Task<double[]> GetTodayHourlyWeightAsync();
}
