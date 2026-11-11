using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// One batch of a product with its own expiry. Product.Quantity is kept equal to the
/// sum of its batches (plus any legacy stock that has no batch yet).
/// Quantities are in base units (tablet, ml bottle, piece).
/// </summary>
public class ProductBatch : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [MaxLength(100)]
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ManufacturedDate { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PurchasePrice { get; set; }

    [MaxLength(200)]
    public string? Supplier { get; set; }
    public int? PurchaseId { get; set; }
    public int? PurchaseItemId { get; set; }

    [MaxLength(100)]
    public string? Store { get; set; }

    public int? LocationId { get; set; }
    public int? VariantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Which batches a sale line was taken from, so returns and edits restore the right batch.</summary>
public class SaleItemBatch : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int SaleItemId { get; set; }
    public int BatchId { get; set; }
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }
}

/// <summary>A bill parked at the counter.</summary>
public class HeldBill : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [MaxLength(100)]
    public string Label { get; set; } = string.Empty;
    public int? CustomerId { get; set; }

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    /// <summary>Cart lines as JSON, owned by the POS screen.</summary>
    public string CartJson { get; set; } = "[]";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [MaxLength(100)]
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PurchaseReturn : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [MaxLength(50)]
    public string Reference { get; set; } = string.Empty;
    public int? PurchaseId { get; set; }

    [MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public int? LocationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PurchaseReturnItem> Items { get; set; } = new();
}

public class PurchaseReturnItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }
    public int ProductId { get; set; }

    [MaxLength(300)]
    public string ProductName { get; set; } = string.Empty;
    public int? VariantId { get; set; }
    public int? BatchId { get; set; }

    [MaxLength(100)]
    public string? BatchNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    /// <summary>Expired, Near expiry, Damaged, Wrong item, Other</summary>
    [MaxLength(50)]
    public string Reason { get; set; } = "Expired";
}

/// <summary>
/// Maps onto the existing SupplierPayments table (also used by the manufacturing module):
/// SupplierId/Reference/Amount/PaymentMethod/Description are its original columns;
/// SupplierName, PurchaseId and CreatedBy were added for pharmacy purchasing.
/// </summary>
public class SupplierPayment : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Supplier party id (0 when the supplier is only known by name).</summary>
    public int SupplierId { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }
    public int? PurchaseId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    [Column("PaymentMethod")]
    public string PaymentType { get; set; } = "Cash";

    [MaxLength(100)]
    public string Reference { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("Description")]
    public string? Notes { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A cashier's till session, opened at shift start and closed with a cash count.</summary>
public class TillSession : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(100)]
    public int? LocationId { get; set; }
    public string? Store { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashSales { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CardSales { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditSales { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashRefunds { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? CountedCash { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>Open, Closed</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Open";
}

public class AuditLog : ITenantEntity
{
    public long Id { get; set; }
    public int TenantId { get; set; }

    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Created, Updated, Deleted, PriceChanged, Refund, StockAdjusted, ...</summary>
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One row per customer + product + sale, so a refill reminder is sent only once.</summary>
public class RefillReminder : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public int SaleId { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string Channel { get; set; } = "SMS";
}

/// <summary>Per-tenant pharmacy settings: receipt header, tax, alerts, discount limits and FBR.</summary>
public class PharmacySettings : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [MaxLength(200)]
    public string PharmacyName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? DrugLicenceNo { get; set; }

    [MaxLength(50)]
    public string? Ntn { get; set; }

    [MaxLength(50)]
    public string? Strn { get; set; }

    [MaxLength(500)]
    public string? ReceiptFooter { get; set; }

    /// <summary>Default sales tax % for items that have no tax of their own.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal DefaultTaxPercent { get; set; }

    /// <summary>Highest discount % a non-admin user can give on a bill.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal MaxCashierDiscountPercent { get; set; } = 10;

    public int ExpiryAlertDays1 { get; set; } = 90;
    public int ExpiryAlertDays2 { get; set; } = 60;
    public int ExpiryAlertDays3 { get; set; } = 30;

    /// <summary>Require doctor name / prescription on Rx-only items.</summary>
    public bool RequirePrescription { get; set; } = true;

    public bool RefillRemindersEnabled { get; set; }

    // FBR POS integration
    public bool FbrEnabled { get; set; }

    [MaxLength(50)]
    public string? FbrPosId { get; set; }

    [MaxLength(500)]
    public string? FbrToken { get; set; }

    /// <summary>Sandbox or Production</summary>
    [MaxLength(20)]
    public string FbrEnvironment { get; set; } = "Sandbox";

    /// <summary>en | ur | ar | en+ur</summary>
    [MaxLength(10)]
    public string ReceiptLanguage { get; set; } = "en";

    /// <summary>Block credit sales over the limit (true) or only warn (false).</summary>
    public bool BlockOverCreditLimit { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
