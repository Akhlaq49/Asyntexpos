using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Purchase order sent to a supplier. Goods are received against it (fully or in parts);
/// every receipt adds stock and creates a Received <see cref="Purchase"/>.
/// Status flow: Draft -> Ordered -> PartiallyReceived -> Received (or Cancelled).
/// </summary>
public class PurchaseOrder : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(30)]
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>Supplier party (Role = "Supplier"); name/phone are copied so the PO keeps them.</summary>
    public int? SupplierId { get; set; }

    [Required, MaxLength(150)]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? SupplierPhone { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public DateTime? ExpectedDate { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = PurchaseOrderStatus.Draft;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OrderTax { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Shipping { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(150)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int PurchaseOrderId { get; set; }

    public int ProductId { get; set; }

    [Required, MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Sku { get; set; }

    public int Quantity { get; set; }

    public int ReceivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    /// <summary>Line discount (amount, for the whole line)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    /// <summary>Quantity x UnitCost - Discount + TaxAmount</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }

    public Product? Product { get; set; }
}

public static class PurchaseOrderStatus
{
    public const string Draft = "Draft";
    public const string Ordered = "Ordered";
    public const string PartiallyReceived = "PartiallyReceived";
    public const string Received = "Received";
    public const string Cancelled = "Cancelled";
}
