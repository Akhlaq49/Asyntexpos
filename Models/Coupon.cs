namespace ReactPosApi.Models;

public class Coupon
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Percentage";        // Percentage | Fixed
    public decimal Discount { get; set; }
    public int Limit { get; set; }                           // 0 = unlimited
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool OncePerCustomer { get; set; }
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string Status { get; set; } = "Active";           // Active | Inactive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
