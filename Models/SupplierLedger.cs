using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Supplier Ledger Entry - tracks all financial transactions with a supplier
/// </summary>
public class SupplierLedgerEntry : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int SupplierId { get; set; }

    [Required, MaxLength(50)]
    public string TransactionType { get; set; } = string.Empty; // Purchase | Payment | Debit | Credit | Adjustment

    [Required, MaxLength(50)]
    public string ReferenceType { get; set; } = string.Empty; // Purchase | ManufacturingOrder | ManualPayment | Adjustment

    public int? ReferenceId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RunningBalance { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Party? Supplier { get; set; }
}

/// <summary>
/// Supplier Payment - records a payment made to a supplier
/// </summary>
public class SupplierPayment : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int SupplierId { get; set; }

    [MaxLength(100)]
    public string Reference { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Cash";

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Party? Supplier { get; set; }
}
