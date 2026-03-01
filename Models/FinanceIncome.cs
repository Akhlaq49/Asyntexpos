using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class FinanceIncome
{
    [Key]
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(50)]
    public string Reference { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Store { get; set; } = string.Empty;

    public int IncomeCategoryId { get; set; }

    [ForeignKey(nameof(IncomeCategoryId))]
    public IncomeCategory? IncomeCategory { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string Account { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
