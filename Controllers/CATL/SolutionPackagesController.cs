using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Inventory > Solution Packages: the product categorization (categories and their families, and the plans -
/// Standard / Premium - each family is in) and the building sizes. The products and their package quantities are in
/// the catalogue (CatalogController).
/// </summary>
[Authorize]
[ApiController]
[Route("api/solution-packages")]
public class SolutionPackagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CatalogService _catalog;
    public SolutionPackagesController(AppDbContext db, CatalogService catalog) { _db = db; _catalog = catalog; }

    public record NameDto(string? Name);
    public record LineDto(int GroupId, string? Name, bool InStandard, bool InPremium);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await LoadAsync());

    private async Task<object> LoadAsync()
    {
        var groups = await _db.SolutionGroups.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync();
        var lines = await _db.SolutionProductLines.AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToListAsync();
        var sizes = await _db.PackageSizes.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync();
        var tiers = await _catalog.TiersAsync();
        var standard = tiers.FirstOrDefault(t => t.Code == CatalogService.Standard)?.Id ?? -1;
        var premium = tiers.FirstOrDefault(t => t.Code == CatalogService.Premium)?.Id ?? -1;
        var familyTiers = (await _db.CatalogFamilyTiers.AsNoTracking().ToListAsync()).ToLookup(x => x.FamilyId, x => x.TierId);
        return new
        {
            groups = groups.Select(g => new
            {
                g.Id, g.Name, g.SortOrder,
                lines = lines.Where(l => l.GroupId == g.Id).Select(l =>
                {
                    bool inStandard = familyTiers[l.Id].Contains(standard), inPremium = familyTiers[l.Id].Contains(premium);
                    return new { l.Id, l.GroupId, l.Name, inStandard, inPremium, usage = CatalogService.UsageOf(inStandard, inPremium), l.SortOrder };
                }),
            }),
            sizes = sizes.Select(s => new { s.Id, s.Name, s.SortOrder }),
        };
    }

    private static string? Clean(string? name, int max) =>
        string.IsNullOrWhiteSpace(name) ? null : (name.Trim().Length > max ? name.Trim()[..max] : name.Trim());

    private ObjectResult Problem409(string message) => Conflict(new { message });

    // ── Categories (headings of the categorization) ──

    [HttpPost("groups")]
    public async Task<IActionResult> AddGroup([FromBody] NameDto dto)
    {
        var name = Clean(dto.Name, 100);
        if (name == null) return BadRequest(new { message = "Enter the group name." });
        if (await _db.SolutionGroups.AnyAsync(g => g.Name == name)) return Problem409($"There is already a group \"{name}\".");
        var order = (await _db.SolutionGroups.MaxAsync(g => (int?)g.SortOrder) ?? 0) + 1;
        _db.SolutionGroups.Add(new SolutionGroup { Name = name, SortOrder = order });
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    [HttpPut("groups/{id}")]
    public async Task<IActionResult> RenameGroup(int id, [FromBody] NameDto dto)
    {
        var g = await _db.SolutionGroups.FindAsync(id);
        if (g == null) return NotFound();
        var name = Clean(dto.Name, 100);
        if (name == null) return BadRequest(new { message = "Enter the group name." });
        if (await _db.SolutionGroups.AnyAsync(x => x.Id != id && x.Name == name)) return Problem409($"There is already a group \"{name}\".");
        g.Name = name;
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    [HttpDelete("groups/{id}")]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var g = await _db.SolutionGroups.FindAsync(id);
        if (g == null) return NotFound();
        _db.SolutionGroups.Remove(g);   // its families go with it
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    // ── Families (rows of the categorization) ──

    [HttpPost("lines")]
    public async Task<IActionResult> AddLine([FromBody] LineDto dto)
    {
        var name = Clean(dto.Name, 150);
        if (name == null) return BadRequest(new { message = "Enter the product name." });
        if (!await _db.SolutionGroups.AnyAsync(g => g.Id == dto.GroupId)) return BadRequest(new { message = "Choose the group." });
        if (await _db.SolutionProductLines.AnyAsync(l => l.Name == name)) return Problem409($"\"{name}\" is already in the categorization.");
        var order = (await _db.SolutionProductLines.Where(l => l.GroupId == dto.GroupId).MaxAsync(l => (int?)l.SortOrder) ?? 0) + 1;
        var line = new SolutionProductLine { GroupId = dto.GroupId, Name = name, SortOrder = order };
        _db.SolutionProductLines.Add(line);
        await _db.SaveChangesAsync();
        await _catalog.SetFamilyTiersAsync(line.Id, dto.InStandard, dto.InPremium);   // the ticks are the family's tiers
        return Ok(await LoadAsync());
    }

    [HttpPut("lines/{id}")]
    public async Task<IActionResult> UpdateLine(int id, [FromBody] LineDto dto)
    {
        var l = await _db.SolutionProductLines.FindAsync(id);
        if (l == null) return NotFound();
        var name = Clean(dto.Name, 150);
        if (name == null) return BadRequest(new { message = "Enter the product name." });
        if (!await _db.SolutionGroups.AnyAsync(g => g.Id == dto.GroupId)) return BadRequest(new { message = "Choose the group." });
        if (await _db.SolutionProductLines.AnyAsync(x => x.Id != id && x.Name == name)) return Problem409($"\"{name}\" is already in the categorization.");
        if (l.GroupId != dto.GroupId)
            l.SortOrder = (await _db.SolutionProductLines.Where(x => x.GroupId == dto.GroupId).MaxAsync(x => (int?)x.SortOrder) ?? 0) + 1;
        l.GroupId = dto.GroupId; l.Name = name;
        await _db.SaveChangesAsync();
        await _catalog.SetFamilyTiersAsync(l.Id, dto.InStandard, dto.InPremium);
        return Ok(await LoadAsync());
    }

    [HttpDelete("lines/{id}")]
    public async Task<IActionResult> DeleteLine(int id)
    {
        var l = await _db.SolutionProductLines.FindAsync(id);
        if (l == null) return NotFound();
        _db.SolutionProductLines.Remove(l);
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    // ── Building sizes (columns of the package quantities) ──

    [HttpPost("sizes")]
    public async Task<IActionResult> AddSize([FromBody] NameDto dto)
    {
        var name = Clean(dto.Name, 50);
        if (name == null) return BadRequest(new { message = "Enter the building size." });
        if (await _db.PackageSizes.AnyAsync(s => s.Name == name)) return Problem409($"There is already a \"{name}\" column.");
        var order = (await _db.PackageSizes.MaxAsync(s => (int?)s.SortOrder) ?? 0) + 1;
        _db.PackageSizes.Add(new PackageSize { Name = name, SortOrder = order });
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    [HttpPut("sizes/{id}")]
    public async Task<IActionResult> RenameSize(int id, [FromBody] NameDto dto)
    {
        var s = await _db.PackageSizes.FindAsync(id);
        if (s == null) return NotFound();
        var name = Clean(dto.Name, 50);
        if (name == null) return BadRequest(new { message = "Enter the building size." });
        if (await _db.PackageSizes.AnyAsync(x => x.Id != id && x.Name == name)) return Problem409($"There is already a \"{name}\" column.");
        s.Name = name;
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    [HttpDelete("sizes/{id}")]
    public async Task<IActionResult> DeleteSize(int id)
    {
        var s = await _db.PackageSizes.FindAsync(id);
        if (s == null) return NotFound();
        _db.PackageSizes.Remove(s);
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    // ── Order (arrows on the page) ──

    /// <summary>Moves a category, family or size one place up (dir = -1) or down (dir = 1)</summary>
    [HttpPost("move/{kind}/{id}")]
    public async Task<IActionResult> Move(string kind, int id, [FromQuery] int dir)
    {
        if (dir is not (-1 or 1)) return BadRequest();
        switch (kind)
        {
            case "groups": Swap(await _db.SolutionGroups.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), id, dir, x => x.Id, (x, o) => x.SortOrder = o); break;
            case "sizes": Swap(await _db.PackageSizes.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), id, dir, x => x.Id, (x, o) => x.SortOrder = o); break;
            case "lines":
                var line = await _db.SolutionProductLines.FindAsync(id);
                if (line == null) return NotFound();
                Swap(await _db.SolutionProductLines.Where(x => x.GroupId == line.GroupId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), id, dir, x => x.Id, (x, o) => x.SortOrder = o);
                break;
            default: return NotFound();
        }
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    /// <summary>Swaps an element with its neighbour and renumbers the list 1..n (so equal orders never get stuck)</summary>
    public static void Swap<T>(List<T> list, int id, int dir, Func<T, int> getId, Action<T, int> setOrder)
    {
        var i = list.FindIndex(x => getId(x) == id);
        if (i < 0 || i + dir < 0 || i + dir >= list.Count) return;
        (list[i], list[i + dir]) = (list[i + dir], list[i]);
        for (var n = 0; n < list.Count; n++) setOrder(list[n], n + 1);
    }
}
