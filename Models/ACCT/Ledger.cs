using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Double-entry ledger (Database/LedgerSchema.sql). Every document that moves money or stock (sale, payment, purchase,
/// return, expense, capital / loan entry) posts one balanced journal entry: total debits = total credits.
/// Reports (trial balance, profit and loss, balance sheet, account ledger) read only these lines.
/// The entries are derived from the documents: a document that changes is posted again (LedgerPoster).
/// </summary>
public class LedgerAccount : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    /// <summary><see cref="LedgerRootType"/>: Asset, Liability, Equity, Income, Expense</summary>
    [MaxLength(20)] public string RootType { get; set; } = LedgerRootType.Asset;
    public int? ParentId { get; set; }
    public bool IsGroup { get; set; }
    /// <summary>The posting rules find the account by this key (receivable, stock, sales ...); null = added by a person</summary>
    [MaxLength(60)] public string? SystemKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class JournalEntry : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public DateTime Date { get; set; }
    /// <summary><see cref="VoucherType"/>: the document that posted it</summary>
    [MaxLength(40)] public string VoucherType { get; set; } = string.Empty;
    public int VoucherId { get; set; }
    [MaxLength(100)] public string? Reference { get; set; }
    [MaxLength(300)] public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<JournalLine> Lines { get; set; } = new();
}

public class JournalLine : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public int AccountId { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Debit { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Credit { get; set; }
    /// <summary>Customer, Supplier or Lender - receivable / payable / loan lines are kept per party</summary>
    [MaxLength(20)] public string? PartyType { get; set; }
    public int? PartyId { get; set; }
    [MaxLength(200)] public string? PartyName { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

/// <summary>One row per company</summary>
public class AccountingSettings : ITenantEntity
{
    public int TenantId { get; set; }
    /// <summary>Entries on or before this date can't change (documents dated then can't be added, edited or deleted)</summary>
    public DateTime? LockDate { get; set; }
    /// <summary>First month of the financial year (1 = January, 7 = July). Income and expense accounts start each year at
    /// zero in the year-based reports; their earlier total is carried forward as profit of earlier years.</summary>
    public int FiscalYearStartMonth { get; set; } = 1;
    /// <summary>The posting rules the ledger was built with; a different value = rebuilt on the next report</summary>
    public int RulesVersion { get; set; }
    public bool NeedsRebuild { get; set; } = true;
    public DateTime? RebuiltAt { get; set; }
    [MaxLength(500)] public string? LastError { get; set; }
}

public static class LedgerRootType
{
    public const string Asset = "Asset", Liability = "Liability", Equity = "Equity", Income = "Income", Expense = "Expense";
    public static readonly string[] All = { Asset, Liability, Equity, Income, Expense };
    /// <summary>Debit-normal: Asset and Expense; the others are credit-normal</summary>
    public static bool IsDebitNormal(string rootType) => rootType is Asset or Expense;
    public static bool IsProfitAndLoss(string rootType) => rootType is Income or Expense;
}

public static class VoucherType
{
    public const string Sale = "Sale", SalePayment = "SalePayment", SalesReturn = "SalesReturn",
        Purchase = "Purchase", PurchasePayment = "PurchasePayment", PurchaseReturn = "PurchaseReturn",
        PurchaseReturnRefund = "PurchaseReturnRefund", Expense = "Expense", FinanceEntry = "FinanceEntry", JournalVoucher = "JournalVoucher";
    public static readonly string[] All = { Sale, SalePayment, SalesReturn, Purchase, PurchasePayment, PurchaseReturn, PurchaseReturnRefund, Expense, FinanceEntry, JournalVoucher };
}

public static class LedgerPartyType
{
    public const string Customer = "Customer", Supplier = "Supplier", Lender = "Lender";
}

/// <summary>Thrown when a change would alter an entry dated on or before the lock date (shown to the user as a 400)</summary>
public class LedgerLockedException : Exception
{
    public LedgerLockedException(string message) : base(message) { }
}
