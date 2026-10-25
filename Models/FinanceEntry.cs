using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Money and value that don't come from sales, purchases or expenses - needed for the balance sheet:
/// the owner's capital and drawings, loans, and fixed assets (with their depreciation).
/// </summary>
public class FinanceEntry : ITenantEntity
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>One of <see cref="FinanceEntryType"/></summary>
    [MaxLength(30)]
    public string Type { get; set; } = FinanceEntryType.OwnerInvestment;

    /// <summary>Owner / lender / asset name (loans and assets are grouped by it)</summary>
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(30)]
    public string Method { get; set; } = "Cash";

    public DateTime Date { get; set; } = DateTime.UtcNow;

    [MaxLength(60)]
    public string? Reference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(150)]
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class FinanceEntryType
{
    public const string OwnerInvestment = "OwnerInvestment";  // cash in, capital up
    public const string OwnerDrawing = "OwnerDrawing";        // cash out, capital down
    public const string LoanReceived = "LoanReceived";        // cash in, loan owed up
    public const string LoanRepayment = "LoanRepayment";      // cash out, loan owed down
    public const string AssetPurchase = "AssetPurchase";      // cash out, fixed assets up
    public const string Depreciation = "Depreciation";        // no cash, fixed asset value down

    public static readonly string[] All = { OwnerInvestment, OwnerDrawing, LoanReceived, LoanRepayment, AssetPurchase, Depreciation };

    public static string Label(string type) => type switch
    {
        OwnerInvestment => "Owner investment",
        OwnerDrawing => "Owner drawing",
        LoanReceived => "Loan received",
        LoanRepayment => "Loan repayment",
        AssetPurchase => "Fixed asset purchase",
        Depreciation => "Depreciation",
        _ => type,
    };

    /// <summary>"In", "Out" or null when no money moves (depreciation)</summary>
    public static string? Direction(string type) => type switch
    {
        OwnerInvestment or LoanReceived => "In",
        OwnerDrawing or LoanRepayment or AssetPurchase => "Out",
        _ => null,
    };
}
