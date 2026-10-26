using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// A staff member asks the company's Admin / SuperAdmin to approve a quotation before it can be converted to a sale.
/// Sent to one or more admins (recipients); any of them can approve or discard it (with a note).
/// </summary>
public class QuotationApproval : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    public int QuotationId { get; set; }
    [MaxLength(50)]
    public string QuotationReference { get; set; } = string.Empty;
    [MaxLength(150)]
    public string CustomerName { get; set; } = string.Empty;
    /// <summary>Quotation total when it was sent</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotal { get; set; }

    public int RequestedById { get; set; }
    [MaxLength(150)]
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(500)]
    public string? Message { get; set; }

    /// <summary>Admin ids it was sent to, as ",12,15,"</summary>
    [MaxLength(500)]
    public string RecipientIds { get; set; } = ",";
    [MaxLength(500)]
    public string RecipientNames { get; set; } = string.Empty;

    /// <summary>Pending, Approved, Discarded, Withdrawn (by the staff member) or Cancelled (quotation changed / deleted)</summary>
    [MaxLength(20)]
    public string Status { get; set; } = QuotationApprovalStatus.Pending;
    public int? DecidedById { get; set; }
    [MaxLength(150)]
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    [MaxLength(500)]
    public string? DecisionNote { get; set; }
}

public static class QuotationApprovalStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Discarded = "Discarded";
    public const string Withdrawn = "Withdrawn";
    public const string Cancelled = "Cancelled";
}
