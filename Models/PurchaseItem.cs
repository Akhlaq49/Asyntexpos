using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// PurchaseItem model representing line items in a purchase order
/// </summary>
public class PurchaseItem : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required]
    [ForeignKey("Purchase")]
    public int PurchaseId { get; set; }

    // Foreign key to Product for inventory sync
    public int? ProductId { get; set; }

    [MaxLength(100)]
    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxPercentage { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    // ── Pharmacy fields ──
    [MaxLength(100)]
    public string? BatchNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ManufacturedDate { get; set; }
    /// <summary>Free units from a distributor scheme (e.g. 10 + 1). Added to stock at zero cost.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BonusQuantity { get; set; } = 0;
    /// <summary>Unit the quantity was bought in: Unit, Strip or Box.</summary>
    [MaxLength(20)]
    public string? PurchaseUnit { get; set; } = "Unit";
    public int? VariantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Purchase? Purchase { get; set; }

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }
}
