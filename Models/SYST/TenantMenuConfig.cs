namespace ReactPosApi.Models;

/// <summary>
/// Stores menu keys that are HIDDEN for a specific tenant.
/// If a menu key has a row here, it is disabled for that tenant.
/// No row = menu is visible (default behavior).
/// </summary>
public class TenantMenuConfig : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string MenuKey { get; set; } = string.Empty;
}
