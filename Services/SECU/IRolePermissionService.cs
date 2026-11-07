namespace ReactPosApi.Services;

public interface IRolePermissionService
{
    Task<List<string>> GetPermissionsByRoleAsync(string role);
    Task<List<string>> GetConfiguredRolesAsync();
    Task<(bool success, int count, string? error)> UpdateRoleAsync(string role, string callerRole, List<string> menuKeys);
}
