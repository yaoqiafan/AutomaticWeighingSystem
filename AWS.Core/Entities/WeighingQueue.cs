using AWS.Core.Enums;

namespace AWS.Core.Entities;

public class WeighingQueue
{
    public long Id { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string? VehiclePlate { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? GoodsCategoryId { get; set; }
    public string GoodsName { get; set; } = string.Empty;
    public WeighingStatus Status { get; set; }
    public DateTime FirstWeighTime { get; set; }
    public double FirstWeight { get; set; }
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Remark { get; set; }
    public string? FirstWeighImagePath { get; set; }
}
