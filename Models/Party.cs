using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Unified table for all person/entity types: Admin, Manager, User, Customer, Guarantor,
/// Supplier, Biller, Store, Warehouse, etc.
/// The Role field distinguishes between types.
/// </summary>
public class Party
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? LastName { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? PhoneWork { get; set; }

    [MaxLength(200)]
    public string? SO { get; set; }  // Son/Daughter Of

    [MaxLength(50)]
    public string? Cnic { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(500)]
    public string? Picture { get; set; }

    [MaxLength(50)]
    public string? Code { get; set; }  // e.g. SU001, BI001

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(200)]
    public string? ContactPerson { get; set; }

    [MaxLength(100)]
    public string? UserName { get; set; }  // For Store role

    // Employee-specific fields (only used when Role is "Employee")
    public int? DepartmentId { get; set; }
    public int? DesignationId { get; set; }
    public int? ShiftId { get; set; }
    public DateTime? DateOfJoining { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BasicSalary { get; set; }

    [MaxLength(50)]
    public string? EmployeeId { get; set; }  // Display ID like EMP001

    // Auth fields (only used when Role is Admin/Manager/User/Store/Employee)
    [MaxLength(500)]
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Determines the party type: "Admin", "Manager", "User", "Customer", "Guarantor",
    /// "Supplier", "Biller", "Store", "Warehouse", "Employee"
    /// </summary>
    [Required, MaxLength(30)]
    public string Role { get; set; } = "Customer";

    [Required, MaxLength(20)]
    public string Status { get; set; } = "active";   // active / inactive

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation — employee FKs
    [ForeignKey(nameof(DepartmentId))]
    public Department? Department { get; set; }

    [ForeignKey(nameof(DesignationId))]
    public Designation? Designation { get; set; }

    [ForeignKey(nameof(ShiftId))]
    public Shift? Shift { get; set; }

    // Navigation — plans this party guarantees
    public ICollection<PlanGuarantor> GuaranteedPlans { get; set; } = new List<PlanGuarantor>();

    // Navigation — addresses (billing/shipping)
    public ICollection<PartyAddress> Addresses { get; set; } = new List<PartyAddress>();
}
