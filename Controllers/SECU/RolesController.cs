using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>Staff roles of the company: list (everyone signed in), add / rename / delete (admins)</summary>
[Authorize]
[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;
    public RolesController(AppDbContext db) => _db = db;

    public record SaveRoleDto(string Name);
    public record RoleDto(int Id, string Name, bool IsSystem, int Users);

    private bool CallerIsAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return role == RoleCatalog.SuperAdmin || role == RoleCatalog.Admin;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await RoleCatalog.EnsureDefaultsAsync(_db, _db.CurrentTenantId);
        var roles = await _db.Roles.OrderByDescending(r => r.IsSystem).ThenBy(r => r.Id).ToListAsync();
        var counts = await _db.Parties.Where(p => p.Role != null).GroupBy(p => p.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Role!, x => x.Count);
        return Ok(roles.Select(r => new RoleDto(r.Id, r.Name, r.IsSystem, counts.TryGetValue(r.Name, out var c) ? c : 0)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRoleDto dto)
    {
        if (!CallerIsAdmin()) return StatusCode(403, new { message = "Only an admin can change roles." });
        await RoleCatalog.EnsureDefaultsAsync(_db, _db.CurrentTenantId);
        var (name, error) = await ValidateAsync(dto.Name, null);
        if (error != null) return BadRequest(new { message = error });
        var role = new Role { Name = name!, CreatedAt = DateTime.UtcNow };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return Ok(new RoleDto(role.Id, role.Name, false, 0));
    }

    /// <summary>Rename: the role's users and menu permissions move to the new name</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, [FromBody] SaveRoleDto dto)
    {
        if (!CallerIsAdmin()) return StatusCode(403, new { message = "Only an admin can change roles." });
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { message = "This role no longer exists." });
        if (role.IsSystem) return BadRequest(new { message = $"{role.Name} is built in and can't be renamed." });
        var (name, error) = await ValidateAsync(dto.Name, role.Id);
        if (error != null) return BadRequest(new { message = error });
        var old = role.Name;
        if (old == name) return Ok(new RoleDto(role.Id, role.Name, false, await _db.Parties.CountAsync(p => p.Role == old)));

        using var transaction = await _db.Database.BeginTransactionAsync();
        role.Name = name!;
        foreach (var p in await _db.Parties.Where(p => p.Role == old).ToListAsync()) p.Role = name!;
        foreach (var rp in await _db.RolePermissions.Where(rp => rp.Role == old).ToListAsync()) rp.Role = name!;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new RoleDto(role.Id, role.Name, false, await _db.Parties.CountAsync(p => p.Role == name)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CallerIsAdmin()) return StatusCode(403, new { message = "Only an admin can change roles." });
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { message = "This role no longer exists." });
        if (role.IsSystem) return BadRequest(new { message = $"{role.Name} is built in and can't be deleted." });
        var users = await _db.Parties.CountAsync(p => p.Role == role.Name);
        if (users > 0) return BadRequest(new { message = $"{users} user(s) have the role {role.Name}. Give them another role first." });
        _db.RolePermissions.RemoveRange(await _db.RolePermissions.Where(rp => rp.Role == role.Name).ToListAsync());
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<(string? name, string? error)> ValidateAsync(string? raw, int? exceptId)
    {
        var name = System.Text.RegularExpressions.Regex.Replace((raw ?? "").Trim(), @"\s+", " ");
        if (name.Length == 0) return (null, "Enter the role name.");
        if (name.Length > 50) return (null, "Keep the role name to 50 characters.");
        if (name.Contains('/')) return (null, "A role name can't contain \"/\".");
        if (RoleCatalog.Reserved.Contains(name, StringComparer.OrdinalIgnoreCase)) return (null, $"\"{name}\" is reserved - choose another name.");
        var taken = (await _db.Roles.Where(r => exceptId == null || r.Id != exceptId).Select(r => r.Name).ToListAsync())
            .Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (taken) return (null, $"There is already a role called \"{name}\".");
        return (name, null);
    }
}
