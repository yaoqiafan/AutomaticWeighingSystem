using System.ComponentModel.DataAnnotations.Schema;

namespace AWS.Core.Entities;

public class DeliveryRecord
{
    public long Id { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime DeliveryTime { get; set; }
    public double TotalWeight { get; set; }    // kg, sum of Items.Weight
    public double? TotalAmount { get; set; }
    public string? Remark { get; set; }

    public List<DeliveryItem> Items { get; set; } = [];

    [NotMapped]
    public string GoodsSummary => Items.Count == 0
        ? "—"
        : string.Join("、", Items.Select(i => i.GoodsName).Distinct());
}
