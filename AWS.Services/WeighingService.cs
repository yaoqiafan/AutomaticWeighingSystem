using AWS.Core.Entities;
using AWS.Core.Enums;
using AWS.Core.Interfaces;
using AWS.Data;
using Microsoft.EntityFrameworkCore;

namespace AWS.Services;

public class WeighingService : IWeighingService
{
    private readonly AwsDbContext _context;
    private readonly DynamicArchiveManager _archive;

    public WeighingService(AwsDbContext context, DynamicArchiveManager archive)
    {
        _context = context;
        _archive = archive;
    }

    public async Task<string> GenerateTicketNoAsync()
    {
        var today = DateTime.Today;
        var prefix = $"PS-{today:yyyyMMdd}-";
        var lastTicket = await _context.WeighingQueues
            .Where(q => q.TicketNo.StartsWith(prefix))
            .OrderByDescending(q => q.TicketNo)
            .Select(q => q.TicketNo)
            .FirstOrDefaultAsync();

        // 同时查询今日存档中的最大序号（当天已存档的单据序号也要计入）
        int maxSeq = 0;
        if (lastTicket != null)
        {
            var seqStr = lastTicket.Replace(prefix, "");
            if (int.TryParse(seqStr, out int seq)) maxSeq = seq;
        }

        var archiveYears = await _archive.GetAvailableYearsAsync();
        if (archiveYears.Contains(today.Year))
        {
            var todayArchives = await _archive.QueryAsync(today.Year, today, today.AddDays(1));
            foreach (var r in todayArchives)
            {
                var seqStr = r.TicketNo.Replace(prefix, "");
                if (int.TryParse(seqStr, out int seq) && seq > maxSeq)
                    maxSeq = seq;
            }
        }

        return $"{prefix}{(maxSeq + 1):D3}";
    }

    public async Task<WeighingQueue> CreateInitialEntryAsync(string vehiclePlate,
        string customerName, int? customerId, string goodsName, int? goodsCategoryId,
        double firstWeight, int operatorId, string operatorName, string? remark = null)
    {
        var ticketNo = await GenerateTicketNoAsync();
        var entry = new WeighingQueue
        {
            TicketNo = ticketNo,
            VehiclePlate = string.IsNullOrWhiteSpace(vehiclePlate) ? null : vehiclePlate.Trim().ToUpper(),
            CustomerName = customerName,
            CustomerId = customerId,
            GoodsName = goodsName,
            GoodsCategoryId = goodsCategoryId,
            Status = WeighingStatus.InitialEntry,
            FirstWeighTime = DateTime.Now,
            FirstWeight = firstWeight,
            OperatorId = operatorId,
            OperatorName = operatorName,
            CreatedAt = DateTime.Now,
            Remark = remark
        };
        _context.WeighingQueues.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task SetWaitingAsync(long queueId)
    {
        var item = await _context.WeighingQueues.FindAsync(queueId)
            ?? throw new InvalidOperationException($"磅单 {queueId} 不存在");
        item.Status = WeighingStatus.Waiting;
        await _context.SaveChangesAsync();
    }

    public async Task<WeighingArchiveRecord> ArchiveAsync(long queueId, double secondWeight,
        double? pricePerUnit = null)
    {
        var item = await _context.WeighingQueues.FindAsync(queueId)
            ?? throw new InvalidOperationException($"磅单 {queueId} 不存在");

        double gross = Math.Max(item.FirstWeight, secondWeight);
        double tare = Math.Min(item.FirstWeight, secondWeight);
        double net = gross - tare;

        var record = new WeighingArchiveRecord
        {
            TicketNo = item.TicketNo,
            VehiclePlate = item.VehiclePlate,
            CustomerName = item.CustomerName,
            GoodsName = item.GoodsName,
            FirstWeighTime = item.FirstWeighTime,
            FirstWeight = item.FirstWeight,
            SecondWeighTime = DateTime.Now,
            SecondWeight = secondWeight,
            GrossWeight = gross,
            TareWeight = tare,
            NetWeight = net,
            OperatorName = item.OperatorName,
            ArchivedAt = DateTime.Now,
            PricePerUnit = pricePerUnit,
            TotalAmount = pricePerUnit.HasValue ? Math.Round(net * pricePerUnit.Value, 2) : null,
            Remark = item.Remark
        };

        record.Id = await _archive.InsertAsync(record);

        _context.WeighingQueues.Remove(item);
        await _context.SaveChangesAsync();

        return record;
    }

    public async Task<List<WeighingQueue>> GetActiveQueueAsync()
        => await _context.WeighingQueues
            .OrderBy(q => q.CreatedAt)
            .ToListAsync();

    public async Task DeleteQueueItemAsync(long queueId)
    {
        var item = await _context.WeighingQueues.FindAsync(queueId);
        if (item != null)
        {
            _context.WeighingQueues.Remove(item);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateQueueAsync(long id, string vehiclePlate, string customerName,
        string goodsName, string? remark, double firstWeight)
    {
        var item = await _context.WeighingQueues.FindAsync(id)
            ?? throw new InvalidOperationException($"磅单 {id} 不存在");
        item.VehiclePlate = string.IsNullOrWhiteSpace(vehiclePlate) ? null : vehiclePlate.Trim().ToUpper();
        item.CustomerName = customerName;
        item.GoodsName = goodsName;
        item.Remark = remark;
        item.FirstWeight = firstWeight;
        await _context.SaveChangesAsync();
    }

    public async Task<double[]> GetTodayHourlyNetWeightAsync()
    {
        var result = new double[24];
        var today = DateTime.Today;
        var years = await _archive.GetAvailableYearsAsync();
        if (!years.Contains(today.Year)) return result;
        var records = await _archive.QueryAsync(today.Year, today, today.AddDays(1));
        foreach (var r in records)
            result[r.ArchivedAt.Hour] += r.NetWeight;
        return result;
    }

    public async Task<(double TotalWeight, int Count)> GetTodayStatsAsync()
    {
        var today = DateTime.Today;
        var years = await _archive.GetAvailableYearsAsync();
        if (!years.Contains(today.Year)) return (0, 0);
        var records = await _archive.QueryAsync(today.Year, today, today.AddDays(1));
        return (records.Sum(r => r.NetWeight), records.Count);
    }
}
