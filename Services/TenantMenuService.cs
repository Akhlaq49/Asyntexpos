using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class TenantMenuService : ITenantMenuService
{
    private readonly AppDbContext _db;

    public TenantMenuService(AppDbContext db) => _db = db;

    public async Task<List<string>> GetHiddenMenuKeysAsync()
    {
        return await _db.TenantMenuConfigs
            .Select(c => c.MenuKey)
            .ToListAsync();
    }

    public async Task<(bool success, int count)> UpdateHiddenMenuKeysAsync(string callerRole, List<string> hiddenKeys)
    {
        if (callerRole != "SuperAdmin" && callerRole != "Admin")
            throw new UnauthorizedAccessException("Only SuperAdmin/Admin can configure tenant menus.");

        var existing = await _db.TenantMenuConfigs.ToListAsync();
        _db.TenantMenuConfigs.RemoveRange(existing);

        var newConfigs = hiddenKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .Select(k => new TenantMenuConfig { MenuKey = k, TenantId = _db.CurrentTenantId })
            .ToList();
        _db.TenantMenuConfigs.AddRange(newConfigs);

        await _db.SaveChangesAsync();
        return (true, newConfigs.Count);
    }

    public async Task<string> GetDefaultDashboardAsync()
    {
        var tenantId = _db.CurrentTenantId;
        var tenant = await _db.Tenants.FindAsync(tenantId);
        return tenant?.DefaultDashboard ?? "/admin-dashboard-2";
    }

    public async Task<bool> SetDefaultDashboardAsync(string callerRole, string dashboardPath)
    {
        if (callerRole != "SuperAdmin" && callerRole != "Admin")
            throw new UnauthorizedAccessException("Only SuperAdmin/Admin can set default dashboard.");

        var tenantId = _db.CurrentTenantId;
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return false;

        tenant.DefaultDashboard = dashboardPath;
        await _db.SaveChangesAsync();
        return true;
    }
}
