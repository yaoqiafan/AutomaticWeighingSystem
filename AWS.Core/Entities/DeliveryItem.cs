namespace AWS.Core.Entities;

public class DeliveryItem
{
    public long Id { get; set; }
    public long DeliveryRecordId { get; set; }
    public int? GoodsCategoryId { get; set; }
    public string GoodsName { get; set; } = string.Empty;
    public double Weight { get; set; }        // kg
    public double? PricePerUnit { get; set; }
    public double? Amount { get; set; }

    public DeliveryRecord? DeliveryRecord { get; set; }
}
