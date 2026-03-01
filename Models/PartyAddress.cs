using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactPosApi.Models;

/// <summary>
/// Stores billing/shipping addresses for Parties (customers, etc.).
/// A Party can have multiple addresses with different types.
/// </summary>
public class PartyAddress
{
    [Key]
    public int Id { get; set; }

    /// <summary>FK to Party (customer)</summary>
    public int PartyId { get; set; }

    [ForeignKey("PartyId")]
    public Party Party { get; set; } = null!;

    /// <summary>Billing, Shipping, Home, Office, etc.</summary>
    [Required, MaxLength(30)]
    public string AddressType { get; set; } = "Billing";

    [MaxLength(200)]
    public string? FirstName { get; set; }

    [MaxLength(200)]
    public string? LastName { get; set; }

    [MaxLength(500)]
    public string? AddressLine1 { get; set; }

    [MaxLength(500)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    public bool IsDefault { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
