using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class IncomeCategory
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<FinanceIncome> Incomes { get; set; } = new List<FinanceIncome>();
}
