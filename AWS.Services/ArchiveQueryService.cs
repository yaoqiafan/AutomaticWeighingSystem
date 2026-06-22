using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;

namespace AWS.Services;

public class ArchiveQueryService : IArchiveQueryService
{
    private readonly DynamicArchiveManager _archive;

    public ArchiveQueryService(DynamicArchiveManager archive)
    {
        _archive = archive;
    }

    public Task<List<WeighingArchiveRecord>> QueryAsync(int year, DateTime? from = null,
        DateTime? to = null, string? vehiclePlate = null,
        string? customerName = null, string? goodsName = null)
        => _archive.QueryAsync(year, from, to, vehiclePlate, customerName, goodsName);

    public Task DeleteAsync(int year, long id)
        => _archive.DeleteAsync(year, id);

    public Task<List<int>> GetAvailableYearsAsync()
        => _archive.GetAvailableYearsAsync();
}
