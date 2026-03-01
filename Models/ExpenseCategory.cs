using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class ExpenseCategory
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
