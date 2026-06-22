namespace AWS.Core.Entities;

public class Vehicle
{
    public int Id { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public int? DefaultCustomerId { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
