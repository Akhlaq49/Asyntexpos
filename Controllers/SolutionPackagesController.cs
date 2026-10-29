using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Inventory > Solution Packages: the product categorization (which product lines the Standard and the
/// Premium solution use) and the package quantities (how many of each item a building of each size needs).
/// Read by the package calculation engine.
/// </summary>
[Authorize]
[ApiController]
[Route("api/solution-packages")]
public class SolutionPackagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantProvider _tenant;
    public SolutionPackagesController(AppDbContext db, ITenantProvider tenant) { _db = db; _tenant = tenant; }

    public record NameDto(string? Name);
    public record LineDto(int GroupId, string? Name, bool InStandard, bool InPremium);
    public record ItemDto(string? Name, int? ProductLineId, int? ProductId, Dictionary<int, int>? Quantities);
    public record CellDto(int ItemId, int SizeId, int Quantity);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // a company registered after the server started gets the starting data here
        if (_tenant.TenantId > 0) SolutionPackageDefaults.Ensure(_db, _tenant.TenantId);
        return Ok(await LoadAsync());
    }

    private async Task<object> LoadAsync()
    {
        var groups = await _db.SolutionGroups.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync();
        var lines = await _db.SolutionProductLines.AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToListAsync();
        var sizes = await _db.PackageSizes.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync();
        var items = await _db.PackageItems.AsNoTracking().OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToListAsync();
        var qty = await _db.PackageQuantities.AsNoTracking().ToListAsync();
        var productIds = items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).Distinct().ToList();
        var products = await _db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProductName);
        var lineById = lines.ToDictionary(l => l.Id);
        var groupName = groups.ToDictionary(g => g.Id, g => g.Name);
        var sizeIds = sizes.Select(s => s.Id).ToHashSet();

        return new
        {
            groups = groups.Select(g => new
            {
                g.Id, g.Name, g.SortOrder,
                lines = lines.Where(l => l.GroupId == g.Id).Select(l => new { l.Id, l.GroupId, l.Name, l.InStandard, l.InPremium, l.SortOrder }),
            }),
            sizes = sizes.Select(s => new { s.Id, s.Name, s.SortOrder }),
            items = items.Select(i =>
            {
                var line = i.ProductLineId != null && lineById.TryGetValue(i.ProductLineId.Value, out var l) ? l : null;
                return new
                {
                    i.Id, i.Name, i.SortOrder,
                    productLineId = line?.Id,
                    productLineName = line?.Name,
                    groupName = line != null && groupName.TryGetValue(line.GroupId, out var gn) ? gn : null,
                    inStandard = line?.InStandard ?? false,
                    inPremium = line?.InPremium ?? false,
                    productId = i.ProductId != null && products.ContainsKey(i.ProductId.Value) ? i.ProductId : null,
                    productName = i.ProductId != null && products.TryGetValue(i.ProductId.Value, out var pn) ? pn : null,
                    quantities = qty.Where(q => q.PackageItemId == i.Id && sizeIds.Contains(q.PackageSizeId) && q.Quantity > 0)
                                    .ToDictionary(q => q.PackageSizeId.ToString(), q => q.Quantity),
                };
            }),
        };
    }

    private static string? Clean(string? name, int max) =>
        string.IsNullOrWhiteSpace(name) ? null : (name.Trim().Length > max ? name.Trim()[..max] : name.Trim());

    private ObjectResult Problem409(string message) => Conflict(new { message });

    // ── Groups (headings of the categorization) ──

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
        var lineIds = await _db.SolutionProductLines.Where(l => l.GroupId == id).Select(l => l.Id).ToListAsync();
        await UnlinkItemsAsync(lineIds);
        _db.SolutionGroups.Remove(g);   // its lines go with it
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    // ── Product lines (rows of the categorization) ──

    [HttpPost("lines")]
    public async Task<IActionResult> AddLine([FromBody] LineDto dto)
    {
        var name = Clean(dto.Name, 150);
        if (name == null) return BadRequest(new { message = "Enter the product name." });
        if (!await _db.SolutionGroups.AnyAsync(g => g.Id == dto.GroupId)) return BadRequest(new { message = "Choose the group." });
        if (await _db.SolutionProductLines.AnyAsync(l => l.Name == name)) return Problem409($"\"{name}\" is already in the categorization.");
        var order = (await _db.SolutionProductLines.Where(l => l.GroupId == dto.GroupId).MaxAsync(l => (int?)l.SortOrder) ?? 0) + 1;
        _db.SolutionProductLines.Add(new SolutionProductLine { GroupId = dto.GroupId, Name = name, InStandard = dto.InStandard, InPremium = dto.InPremium, SortOrder = order });
        await _db.SaveChangesAsync();
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
        l.GroupId = dto.GroupId; l.Name = name; l.InStandard = dto.InStandard; l.InPremium = dto.InPremium;
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    [HttpDelete("lines/{id}")]
    public async Task<IActionResult> DeleteLine(int id)
    {
        var l = await _db.SolutionProductLines.FindAsync(id);
        if (l == null) return NotFound();
        await UnlinkItemsAsync(new List<int> { id });
        _db.SolutionProductLines.Remove(l);
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    /// <summary>Package items of deleted lines become "not categorised" (a soft delete keeps the link otherwise)</summary>
    private async Task UnlinkItemsAsync(List<int> lineIds)
    {
        foreach (var item in await _db.PackageItems.Where(i => i.ProductLineId != null && lineIds.Contains(i.ProductLineId.Value)).ToListAsync())
            item.ProductLineId = null;
    }

    // ── Building sizes (columns of the packages sheet) ──

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
        _db.PackageSizes.Remove(s);   // its quantities go with it
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    // ── Package items (rows of the packages sheet) ──

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] ItemDto dto)
    {
        var item = new PackageItem();
        var error = await FillItemAsync(item, dto, null);
        if (error != null) return error;
        item.SortOrder = (await _db.PackageItems.MaxAsync(i => (int?)i.SortOrder) ?? 0) + 1;
        _db.PackageItems.Add(item);
        await _db.SaveChangesAsync();
        await SetQuantitiesAsync(item.Id, dto.Quantities);
        return Ok(await LoadAsync());
    }

    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] ItemDto dto)
    {
        var item = await _db.PackageItems.FindAsync(id);
        if (item == null) return NotFound();
        var error = await FillItemAsync(item, dto, id);
        if (error != null) return error;
        await _db.SaveChangesAsync();
        if (dto.Quantities != null) await SetQuantitiesAsync(id, dto.Quantities);
        return Ok(await LoadAsync());
    }

    private async Task<IActionResult?> FillItemAsync(PackageItem item, ItemDto dto, int? id)
    {
        var name = Clean(dto.Name, 150);
        if (name == null) return BadRequest(new { message = "Enter the item name." });
        if (await _db.PackageItems.AnyAsync(i => i.Id != (id ?? 0) && i.Name == name)) return Problem409($"\"{name}\" is already in the packages.");
        if (dto.ProductLineId != null && !await _db.SolutionProductLines.AnyAsync(l => l.Id == dto.ProductLineId))
            return BadRequest(new { message = "That categorization product no longer exists." });
        if (dto.ProductId != null && !await _db.Products.AnyAsync(p => p.Id == dto.ProductId))
            return BadRequest(new { message = "That inventory product no longer exists." });
        item.Name = name; item.ProductLineId = dto.ProductLineId; item.ProductId = dto.ProductId;
        return null;
    }

    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var item = await _db.PackageItems.FindAsync(id);
        if (item == null) return NotFound();
        _db.PackageItems.Remove(item);   // its quantities go with it
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    /// <summary>Save edited cells of the packages sheet in one go</summary>
    [HttpPut("quantities")]
    public async Task<IActionResult> SaveQuantities([FromBody] List<CellDto> cells)
    {
        if (cells.Any(c => c.Quantity < 0)) return BadRequest(new { message = "Quantities can't be negative." });
        foreach (var byItem in cells.GroupBy(c => c.ItemId))
            await SetQuantitiesAsync(byItem.Key, byItem.ToDictionary(c => c.SizeId, c => c.Quantity), save: false);
        await _db.SaveChangesAsync();
        return Ok(await LoadAsync());
    }

    private async Task SetQuantitiesAsync(int itemId, Dictionary<int, int>? quantities, bool save = true)
    {
        if (quantities == null || quantities.Count == 0) return;
        if (!await _db.PackageItems.AnyAsync(i => i.Id == itemId)) return;
        var sizeIds = await _db.PackageSizes.Select(s => s.Id).ToListAsync();
        var existing = await _db.PackageQuantities.Where(q => q.PackageItemId == itemId).ToListAsync();
        foreach (var (sizeId, value) in quantities)
        {
            if (!sizeIds.Contains(sizeId)) continue;
            var qty = Math.Max(0, value);
            var row = existing.FirstOrDefault(q => q.PackageSizeId == sizeId);
            if (row != null) row.Quantity = qty;   // updated in place (0 = none), so the item + size pair stays unique
            else if (qty > 0) _db.PackageQuantities.Add(new PackageQuantity { PackageItemId = itemId, PackageSizeId = sizeId, Quantity = qty });
        }
        if (save) await _db.SaveChangesAsync();
    }

    // ── Order (arrows on the page) ──

    /// <summary>Moves a group, product line, size or item one place up (dir = -1) or down (dir = 1)</summary>
    [HttpPost("move/{kind}/{id}")]
    public async Task<IActionResult> Move(string kind, int id, [FromQuery] int dir)
    {
        if (dir is not (-1 or 1)) return BadRequest();
        switch (kind)
        {
            case "groups": Swap(await _db.SolutionGroups.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), id, dir, x => x.Id, (x, o) => x.SortOrder = o); break;
            case "sizes": Swap(await _db.PackageSizes.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), id, dir, x => x.Id, (x, o) => x.SortOrder = o); break;
            case "items": Swap(await _db.PackageItems.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(), id, dir, x => x.Id, (x, o) => x.SortOrder = o); break;
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

    private static void Swap<T>(List<T> list, int id, int dir, Func<T, int> getId, Action<T, int> setOrder)
    {
        var i = list.FindIndex(x => getId(x) == id);
        if (i < 0 || i + dir < 0 || i + dir >= list.Count) return;
        (list[i], list[i + dir]) = (list[i + dir], list[i]);
        for (var n = 0; n < list.Count; n++) setOrder(list[n], n + 1);   // renumber, so equal orders never get stuck
    }
}
