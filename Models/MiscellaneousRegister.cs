using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class MiscellaneousRegister
{
    [Key]
    public int Id { get; set; }

    public int CustomerId { get; set; }

    [Required, MaxLength(100)]
    public string TransactionType { get; set; } = string.Empty; // Credit, Debit, Adjustment

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ReferenceId { get; set; } // Link to installment plan or payment ID

    [Required, MaxLength(50)]
    public string ReferenceType { get; set; } = string.Empty; // InstallmentPayment, ManualAdjustment, etc.

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? CreatedBy { get; set; } // User who made the transaction

    // Navigation property
    [ForeignKey("CustomerId")]
    public Party? Customer { get; set; }
}