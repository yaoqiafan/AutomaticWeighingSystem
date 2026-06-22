using AWS.Core.Entities;

namespace AWS.Core.Interfaces;

public interface IArchiveQueryService
{
    Task<List<WeighingArchiveRecord>> QueryAsync(int year, DateTime? from = null,
        DateTime? to = null, string? vehiclePlate = null,
        string? customerName = null, string? goodsName = null);
    Task DeleteAsync(int year, long id);
    Task<List<int>> GetAvailableYearsAsync();
}
