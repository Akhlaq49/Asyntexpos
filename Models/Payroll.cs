using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

public class Payroll
{
    [Key]
    public int Id { get; set; }

    /// <summary>Employee – FK to Parties</summary>
    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Party Employee { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BasicSalary { get; set; }

    // Allowances
    [Column(TypeName = "decimal(18,2)")]
    public decimal HRA { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Conveyance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MedicalAllowance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Bonus { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherAllowance { get; set; }

    // Deductions
    [Column(TypeName = "decimal(18,2)")]
    public decimal PF { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProfessionalTax { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TDS { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LoanDeduction { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherDeduction { get; set; }

    // Totals
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAllowance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDeduction { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetSalary { get; set; }

    /// <summary>Paid / Unpaid</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Unpaid";

    public int? Month { get; set; }
    public int? Year { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
