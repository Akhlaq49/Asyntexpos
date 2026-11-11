using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// A journal entry typed in by a person (Database/ACCT/Schema.sql): the accounts and the debit or credit of each line
/// are chosen by hand. Used for what no other document posts: reclasses, corrections, depreciation / amortization,
/// accruals. Like every document it is posted to the ledger (LedgerPoster), so a rebuild keeps it.
/// </summary>
public class JournalVoucher : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public DateTime Date { get; set; }
    /// <summary>JV-0001, unique per company</summary>
    [MaxLength(30)] public string Reference { get; set; } = string.Empty;
    [MaxLength(300)] public string Description { get; set; } = string.Empty;
    /// <summary>Set on a voucher made by "Reverse": the voucher it undoes (its lines with debit and credit swapped)</summary>
    public int? ReversalOfId { get; set; }
    [MaxLength(150)] public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<JournalVoucherLine> Lines { get; set; } = new();
}

public class JournalVoucherLine : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int JournalVoucherId { get; set; }
    public JournalVoucher? JournalVoucher { get; set; }
    /// <summary>Order of the line on the voucher</summary>
    public int LineNo { get; set; }
    public int AccountId { get; set; }
    /// <summary>One of Debit / Credit is more than zero, the other is zero</summary>
    [Column(TypeName = "decimal(18,2)")] public decimal Debit { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Credit { get; set; }
    /// <summary><see cref="LedgerPartyType"/>: needed on receivable / payable / loan lines</summary>
    [MaxLength(20)] public string? PartyType { get; set; }
    public int? PartyId { get; set; }
    [MaxLength(200)] public string? PartyName { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}
