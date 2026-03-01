using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class InstallmentPlan
{
    [Key]
    public int Id { get; set; }

    // Foreign keys
    public int CustomerId { get; set; }
    public int ProductId { get; set; }

    public decimal ProductPrice { get; set; }

    /// <summary>Custom finance amount (may differ from product price)</summary>
    public decimal? FinanceAmount { get; set; }

    public decimal DownPayment { get; set; }

    public decimal FinancedAmount { get; set; }

    public decimal InterestRate { get; set; }  // annual %

    public int Tenure { get; set; }  // months

    public decimal EmiAmount { get; set; }

    public decimal TotalPayable { get; set; }

    public decimal TotalInterest { get; set; }

    [Required, MaxLength(20)]
    public string StartDate { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public int PaidInstallments { get; set; }

    public int RemainingInstallments { get; set; }

    [MaxLength(20)]
    public string? NextDueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("CustomerId")]
    public Party? Customer { get; set; }

    [ForeignKey("ProductId")]
    public Product? Product { get; set; }

    public ICollection<RepaymentEntry> Schedule { get; set; } = new List<RepaymentEntry>();

    public ICollection<PlanGuarantor> PlanGuarantors { get; set; } = new List<PlanGuarantor>();
}
