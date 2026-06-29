using AWS.Core.Entities;
using AWS.Core.Interfaces;
using AWS.Data;
using Microsoft.EntityFrameworkCore;

namespace AWS.Services;

public class DeliveryService : IDeliveryService
{
    private readonly AwsDbContext _db;
    private readonly DynamicArchiveManager _archive;

    public DeliveryService(AwsDbContext db, DynamicArchiveManager archive)
    {
        _db = db;
        _archive = archive;
    }

    public async Task<string> AddAsync(DeliveryRecord record)
    {
        record.TicketNo = await GenerateTicketNoAsync();
        record.DeliveryTime = record.DeliveryTime == default ? DateTime.Now : record.DeliveryTime;
        record.TotalWeight = record.Items.Sum(i => i.Weight);
        record.TotalAmount = record.Items.Sum(i => i.Amount ?? 0);
        if (record.TotalAmount == 0) record.TotalAmount = null;
        _db.DeliveryRecords.Add(record);
        await _db.SaveChangesAsync();
        return record.TicketNo;
    }

    public async Task DeleteAsync(long id)
    {
        var items = await _db.DeliveryItems.Where(i => i.DeliveryRecordId == id).ToListAsync();
        _db.DeliveryItems.RemoveRange(items);

        var r = await _db.DeliveryRecords.FindAsync(id);
        if (r is not null) _db.DeliveryRecords.Remove(r);

        await _db.SaveChangesAsync();
    }

    public Task<List<DeliveryRecord>> GetTodayRecordsAsync()
    {
        var today = DateTime.Today;
        return _db.DeliveryRecords
            .Include(r => r.Items)
            .Where(r => r.DeliveryTime >= today && r.DeliveryTime < today.AddDays(1))
            .OrderByDescending(r => r.DeliveryTime)
            .ToListAsync();
    }

    public async Task<double> GetTodayTotalWeightAsync()
    {
        var today = DateTime.Today;
        return await _db.DeliveryRecords
            .Where(r => r.DeliveryTime >= today && r.DeliveryTime < today.AddDays(1))
            .SumAsync(r => (double?)r.TotalWeight) ?? 0;
    }

    public async Task<double> GetTodayTotalAmountAsync()
    {
        var today = DateTime.Today;
        return await _db.DeliveryRecords
            .Where(r => r.DeliveryTime >= today && r.DeliveryTime < today.AddDays(1))
            .SumAsync(r => r.TotalAmount) ?? 0;
    }

    public Task<List<DeliveryRecord>> QueryAsync(DateTime? from, DateTime? to,
        string? customerName = null, string? goodsName = null)
    {
        var q = _db.DeliveryRecords.Include(r => r.Items).AsQueryable();
        if (from.HasValue) q = q.Where(r => r.DeliveryTime >= from.Value);
        if (to.HasValue)   q = q.Where(r => r.DeliveryTime <= to.Value);
        if (!string.IsNullOrWhiteSpace(customerName))
            q = q.Where(r => r.CustomerName.Contains(customerName.Trim()));
        if (!string.IsNullOrWhiteSpace(goodsName))
            q = q.Where(r => r.Items.Any(i => i.GoodsName.Contains(goodsName.Trim())));
        return q.OrderByDescending(r => r.DeliveryTime).ToListAsync();
    }

    public async Task<(double TotalWeight, double TotalAmount, int Count)> GetQuerySummaryAsync(
        DateTime? from, DateTime? to, string? customerName = null, string? goodsName = null)
    {
        var records = await QueryAsync(from, to, customerName, goodsName);
        return (
            records.Sum(r => r.TotalWeight),
            records.Sum(r => r.TotalAmount ?? 0),
            records.Count
        );
    }

    public async Task<double[]> GetTodayHourlyWeightAsync()
    {
        var today = DateTime.Today;
        var result = new double[24];
        var records = await _db.DeliveryRecords
            .Where(r => r.DeliveryTime >= today && r.DeliveryTime < today.AddDays(1))
            .ToListAsync();
        foreach (var r in records)
            result[r.DeliveryTime.Hour] += r.TotalWeight;
        return result;
    }

    public async Task<Dictionary<string, double>> GetCurrentInventoryAsync()
    {
        // 各品类库存 = 全部收货净重 - 全部送货重量（按品类分别核算）
        var availableYears = await _archive.GetAvailableYearsAsync();
        var incoming = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var year in availableYears)
        {
            var records = await _archive.QueryAsync(year);
            foreach (var r in records)
            {
                incoming.TryAdd(r.GoodsName, 0);
                incoming[r.GoodsName] += r.NetWeight;
            }
        }

        // 按品类从 DeliveryItems 汇总送出量
        var outgoing = await _db.DeliveryItems
            .GroupBy(i => i.GoodsName)
            .Select(g => new { GoodsName = g.Key, TotalWeight = g.Sum(i => i.Weight) })
            .ToListAsync();

        foreach (var o in outgoing)
        {
            incoming.TryAdd(o.GoodsName, 0);
            incoming[o.GoodsName] -= o.TotalWeight;
        }

        return incoming;
    }

    public async Task<Dictionary<string, List<(DateTime Date, double Stock)>>> GetInventoryTrendAsync(
        DateTime from, DateTime to, IEnumerable<string>? categories = null)
    {
        var catList = categories?.ToList();
        var availableYears = await _archive.GetAvailableYearsAsync();
        var baseStock = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // 期初库存（from 之前的收货 - 送货）
        foreach (var year in availableYears)
        {
            var records = await _archive.QueryAsync(year, null, from.AddSeconds(-1));
            foreach (var r in records)
            {
                if (catList != null && !catList.Contains(r.GoodsName, StringComparer.OrdinalIgnoreCase)) continue;
                baseStock.TryAdd(r.GoodsName, 0);
                baseStock[r.GoodsName] += r.NetWeight;
            }
        }

        var baseOutItems = await _db.DeliveryItems
            .Where(i => i.DeliveryRecord!.DeliveryTime < from)
            .ToListAsync();
        foreach (var i in baseOutItems)
        {
            if (catList != null && !catList.Contains(i.GoodsName, StringComparer.OrdinalIgnoreCase)) continue;
            baseStock.TryAdd(i.GoodsName, 0);
            baseStock[i.GoodsName] -= i.Weight;
        }

        // 区间内收货
        var rangeIncoming = new List<(string GoodsName, DateTime Date, double Weight)>();
        foreach (var year in availableYears.Where(y => y >= from.Year && y <= to.Year))
        {
            var recs = await _archive.QueryAsync(year, from, to);
            rangeIncoming.AddRange(recs
                .Where(r => catList == null || catList.Contains(r.GoodsName, StringComparer.OrdinalIgnoreCase))
                .Select(r => (r.GoodsName, r.ArchivedAt.Date, r.NetWeight)));
        }

        // 区间内送货（从 DeliveryItems）
        var rangeOutItems = await _db.DeliveryItems
            .Where(i => i.DeliveryRecord!.DeliveryTime >= from && i.DeliveryRecord!.DeliveryTime <= to)
            .Select(i => new { i.GoodsName, Date = i.DeliveryRecord!.DeliveryTime.Date, i.Weight })
            .ToListAsync();

        var rangeOutgoing = rangeOutItems
            .Where(x => catList == null || catList.Contains(x.GoodsName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var allGoods = rangeIncoming.Select(x => x.GoodsName)
            .Union(rangeOutgoing.Select(x => x.GoodsName))
            .Union(baseStock.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, List<(DateTime, double)>>();
        var days = (int)(to.Date - from.Date).TotalDays + 1;

        foreach (var goods in allGoods)
        {
            double running = baseStock.TryGetValue(goods, out var b) ? b : 0;
            var trend = new List<(DateTime, double)>();
            for (int d = 0; d < days; d++)
            {
                var date = from.Date.AddDays(d);
                running += rangeIncoming
                    .Where(x => x.GoodsName.Equals(goods, StringComparison.OrdinalIgnoreCase) && x.Date == date)
                    .Sum(x => x.Weight);
                running -= rangeOutgoing
                    .Where(x => x.GoodsName.Equals(goods, StringComparison.OrdinalIgnoreCase) && x.Date == date)
                    .Sum(x => x.Weight);
                trend.Add((date, Math.Max(0, running)));
            }
            result[goods] = trend;
        }

        return result;
    }

    private async Task<string> GenerateTicketNoAsync()
    {
        var today = DateTime.Today;
        var prefix = $"SD-{today:yyyyMMdd}-";
        var last = await _db.DeliveryRecords
            .Where(r => r.TicketNo.StartsWith(prefix))
            .OrderByDescending(r => r.TicketNo)
            .Select(r => r.TicketNo)
            .FirstOrDefaultAsync();

        int maxSeq = 0;
        if (last != null && int.TryParse(last.Replace(prefix, ""), out int seq))
            maxSeq = seq;

        return $"{prefix}{(maxSeq + 1):D3}";
    }
}
