using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// The company's staff roles (Roles table). Every company starts with the default list;
/// "SuperAdmin" is the company owner (not in the table, never shown in the role lists).
/// </summary>
public static class RoleCatalog
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";

    public static readonly string[] Defaults =
    {
        "Admin", "Manager", "Salesman", "Supervisor", "Store Keeper", "Delivery Biker",
        "Maintenance", "Quality Analyst", "Accountant", "Purchase", "User",
    };

    /// <summary>Names that belong to customers / suppliers etc. (same records table) - not usable as staff roles</summary>
    public static readonly string[] Reserved = { "SuperAdmin", "Customer", "Supplier", "Biller", "Store", "Warehouse" };

    /// <summary>Gives a company the default roles the first time they are needed</summary>
    public static async Task EnsureDefaultsAsync(AppDbContext db, int tenantId)
    {
        if (tenantId <= 0) return;
        if (await db.Roles.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tenantId)) return;
        db.Roles.AddRange(Defaults.Select(n => new Role { TenantId = tenantId, Name = n, IsSystem = n == Admin, CreatedAt = DateTime.UtcNow }));
        await db.SaveChangesAsync();
    }

    /// <summary>Role names that make a record a staff user of the company (its roles + SuperAdmin)</summary>
    public static async Task<List<string>> UserRoleNamesAsync(AppDbContext db, int tenantId)
    {
        await EnsureDefaultsAsync(db, tenantId);
        var names = await db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).Select(r => r.Name).ToListAsync();
        names.Add(SuperAdmin);
        return names;
    }
}
