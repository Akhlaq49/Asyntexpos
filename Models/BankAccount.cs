using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class BankAccount
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string HolderName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string BankName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Branch { get; set; } = string.Empty;

    [MaxLength(50)]
    public string IFSC { get; set; } = string.Empty;

    public int AccountTypeId { get; set; }

    [ForeignKey(nameof(AccountTypeId))]
    public AccountType? AccountType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningBalance { get; set; }

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";

    public bool IsDefault { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
