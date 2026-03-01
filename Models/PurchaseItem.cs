using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// PurchaseItem model representing line items in a purchase order
/// </summary>
public class PurchaseItem
{
    [Key]
    public int Id { get; set; }

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Purchase? Purchase { get; set; }

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; }
}
