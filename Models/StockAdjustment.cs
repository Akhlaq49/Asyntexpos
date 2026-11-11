using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class StockAdjustment : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

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

    /// <summary>Damage, Breakage, Theft, Count correction, Expired write-off, Other</summary>
    [MaxLength(50)]
    public string? Reason { get; set; }
    public int? BatchId { get; set; }
    public int? LocationId { get; set; }
    public int? VariantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
