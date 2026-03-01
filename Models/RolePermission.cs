namespace ReactPosApi.Models;

public class RolePermission
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string MenuKey { get; set; } = string.Empty;
}
