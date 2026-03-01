using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class Expense
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Reference { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ExpenseName { get; set; } = string.Empty;

    public int ExpenseCategoryId { get; set; }

    [ForeignKey(nameof(ExpenseCategoryId))]
    public ExpenseCategory? ExpenseCategory { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
