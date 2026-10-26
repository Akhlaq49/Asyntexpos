using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Unified table for all person/entity types: Admin, Manager, User, Customer, Guarantor,
/// Supplier, Biller, Store, Warehouse, etc.
/// The Role field distinguishes between types.
/// </summary>
public class Party : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

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

    /* ---- customer's building (site for the automation work) ---- */
    /// <summary>House, Villa, Apartment ...</summary>
    [MaxLength(50)]
    public string? BuildingType { get; set; }
    /// <summary>Size: 10 Marla, 1 Kanal, 2 BHK ...</summary>
    [MaxLength(50)]
    public string? BuildingSpace { get; set; }
    /// <summary>Construction stage: Initial, Grey Structure, Finishing ...</summary>
    [MaxLength(50)]
    public string? BuildingStage { get; set; }

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



    // Navigation — addresses (billing/shipping)
    public ICollection<PartyAddress> Addresses { get; set; } = new List<PartyAddress>();
}
