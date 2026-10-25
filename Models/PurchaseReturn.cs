using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Goods sent back to the supplier from one received purchase (a direct purchase or a purchase order receipt).
/// Its value is settled by deducting it from what is still owed on that purchase (Credited) and/or by
/// money the supplier pays back (Refunded); whatever is left the supplier still owes (Due).
/// </summary>
public class PurchaseReturn : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    [MaxLength(30)]
    public string Reference { get; set; } = string.Empty;   // PR-0001

    public int PurchaseId { get; set; }
    [MaxLength(50)]
    public string PurchaseReference { get; set; } = string.Empty;
    public int? PurchaseOrderId { get; set; }

    [MaxLength(150)]
    public string SupplierName { get; set; } = string.Empty;

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    /// <summary>Value of the goods returned (their cost + share of the purchase's tax and discount)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    /// <summary>Deducted from what was still owed on the purchase</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Credited { get; set; }

    /// <summary>Money the supplier has paid back</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Refunded { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(150)]
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<PurchaseReturnItem> Items { get; set; } = new();
    public List<PurchaseReturnRefund> Refunds { get; set; } = new();
}

public class PurchaseReturnItem : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }

    public int PurchaseItemId { get; set; }
    public int? ProductId { get; set; }

    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }
}

/// <summary>Money received back from the supplier for a return (when the return is saved or later)</summary>
public class PurchaseReturnRefund : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(30)]
    public string Method { get; set; } = "Cash";

    [MaxLength(60)]
    public string? Reference { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(150)]
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
