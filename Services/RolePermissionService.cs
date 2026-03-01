using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class RolePermissionService : IRolePermissionService
{
    private readonly AppDbContext _db;

    public RolePermissionService(AppDbContext db) => _db = db;

    public async Task<List<string>> GetPermissionsByRoleAsync(string role)
    {
        if (role == "Admin")
            return new List<string> { "*" };

        return await _db.RolePermissions
            .Where(rp => rp.Role == role)
            .Select(rp => rp.MenuKey)
            .ToListAsync();
    }

    public async Task<List<string>> GetConfiguredRolesAsync()
    {
        return await _db.RolePermissions
            .Select(rp => rp.Role)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();
    }

    public async Task<(bool success, int count, string? error)> UpdateRoleAsync(string role, string callerRole, List<string> menuKeys)
    {
        if (callerRole != "Admin")
            return (false, 0, "Forbidden");

        if (role == "Admin")
            return (false, 0, "Admin role always has full access and cannot be modified.");

        var existing = await _db.RolePermissions
            .Where(rp => rp.Role == role)
            .ToListAsync();
        _db.RolePermissions.RemoveRange(existing);

        var newPerms = menuKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .Select(k => new RolePermission { Role = role, MenuKey = k })
            .ToList();
        _db.RolePermissions.AddRange(newPerms);

        await _db.SaveChangesAsync();
        return (true, newPerms.Count, null);
    }
}
