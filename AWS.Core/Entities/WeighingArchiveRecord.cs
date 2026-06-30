namespace AWS.Core.Entities;

public class WeighingArchiveRecord
{
    public long Id { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public string? VehiclePlate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string GoodsName { get; set; } = string.Empty;
    public DateTime FirstWeighTime { get; set; }
    public double FirstWeight { get; set; }
    public DateTime SecondWeighTime { get; set; }
    public double SecondWeight { get; set; }
    public double GrossWeight { get; set; }
    public double TareWeight { get; set; }
    public double NetWeight { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime ArchivedAt { get; set; }
    public double? PricePerUnit { get; set; }
    public double? TotalAmount { get; set; }
    public string? Remark { get; set; }
    public string? FirstWeighImagePath  { get; set; }
    public string? SecondWeighImagePath { get; set; }
}
