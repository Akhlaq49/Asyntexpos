using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

public class AccountType : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
}
