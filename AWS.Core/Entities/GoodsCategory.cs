namespace AWS.Core.Entities;

public class GoodsCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
