namespace ReactPosApi.Models;

public class RolePermission : ITenantEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string MenuKey { get; set; } = string.Empty;
}
