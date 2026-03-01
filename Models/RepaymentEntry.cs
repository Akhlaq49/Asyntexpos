using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class RepaymentEntry
{
    [Key]
    public int Id { get; set; }

    public int PlanId { get; set; }

    public int InstallmentNo { get; set; }

    [Required, MaxLength(20)]
    public string DueDate { get; set; } = string.Empty;

    public decimal EmiAmount { get; set; }

    public decimal Principal { get; set; }

    public decimal Interest { get; set; }

    public decimal Balance { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "upcoming";

    [MaxLength(20)]
    public string? PaidDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualPaidAmount { get; set; } // Actual cash amount paid by customer

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MiscAdjustedAmount { get; set; } // Amount adjusted from misc register

    // Navigation
    [ForeignKey("PlanId")]
    public InstallmentPlan? Plan { get; set; }
}
