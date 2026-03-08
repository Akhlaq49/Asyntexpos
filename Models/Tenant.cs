using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

/// <summary>
/// Interface for all entities that belong to a tenant.
/// Implemented by every model to enable row-level tenant isolation.
/// </summary>
public interface ITenantEntity
{
    int TenantId { get; set; }
}

/// <summary>
/// Represents a tenant (business/organization) in the multi-tenant system.
/// A tenant is created automatically when a user registers (becomes Admin/Super Admin).
/// All data (inventory, finance, HRM, etc.) is scoped to a tenant.
/// </summary>
public class Tenant
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [MaxLength(200)]
    public string DefaultDashboard { get; set; } = "/admin-dashboard-2";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
