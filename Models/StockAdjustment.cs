using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class StockAdjustment
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Warehouse { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Store { get; set; } = string.Empty;

    public int ProductId { get; set; }

    [MaxLength(100)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Person { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? Notes { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
