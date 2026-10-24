using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Money paid to a supplier against a purchase (receipt), or money the supplier gave back.
/// Records are never edited or deleted: a correction is a new entry in the other direction.
/// The purchase's Paid = payments - refunds.
/// </summary>
public class PurchasePayment : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int PurchaseId { get; set; }

    /// <summary>The purchase order the receipt belongs to (null for purchases entered directly)</summary>
    public int? PurchaseOrderId { get; set; }

    /// <summary><see cref="PurchasePaymentType"/>: Payment (money out) or Refund (money back in)</summary>
    [Required, MaxLength(20)]
    public string Type { get; set; } = PurchasePaymentType.Payment;

    /// <summary>Always positive; the direction comes from Type</summary>
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

public static class PurchasePaymentType
{
    public const string Payment = "Payment";
    public const string Refund = "Refund";
}
