using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Purchase model for managing purchase orders and supplier/vendor purchases
/// </summary>
public class Purchase
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    [Required]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? SupplierRef { get; set; }

    [MaxLength(50)]
    [Required]
    public string Reference { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [MaxLength(30)]
    public string Status { get; set; } = "Received"; // Received, Pending, Ordered, Cancelled

    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "Unpaid"; // Paid, Unpaid, Overdue, Partial

    [Column(TypeName = "decimal(18,2)")]
    public decimal OrderTax { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Shipping { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Paid { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation property for line items
    public ICollection<PurchaseItem>? Items { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
