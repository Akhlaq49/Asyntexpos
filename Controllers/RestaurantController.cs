using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

public class RecipeDto
{
    public decimal YieldQty { get; set; } = 1;
    public string? Notes { get; set; }
    public int? KitchenStationId { get; set; }
    public List<RecipeItemDto> Items { get; set; } = new();
}

public class RecipeItemDto
{
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "g";
    public decimal WastagePct { get; set; }
}

public class ModifierGroupDto
{
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;
    public List<ModifierDto> Modifiers { get; set; } = new();
}

public class ModifierDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int? IngredientId { get; set; }
    public decimal? IngredientQty { get; set; }
}

public class DiningTableDto
{
    public int LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Seats { get; set; } = 4;
    public string Status { get; set; } = "Free";
}

public class KitchenStationDto
{
    public int LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PrinterName { get; set; }
}

public class StatusDto
{
    public string Status { get; set; } = string.Empty;
}

/// <summary>Menu, recipes and food cost, modifiers, tables and the kitchen display.</summary>
[Authorize]
[ApiController]
[Route("api/restaurant")]
public class RestaurantController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRestaurantService _restaurant;
    private readonly ILocationService _locations;

    public RestaurantController(AppDbContext db, IRestaurantService restaurant, ILocationService locations)
    {
        _db = db;
        _restaurant = restaurant;
        _locations = locations;
    }

    // ── Menu and recipes ──

    /// <summary>Menu items with their station, modifiers and food cost.</summary>
    [HttpGet("menu")]
    public async Task<IActionResult> Menu()
    {
        var items = await _db.Products.Where(p => p.IsMenuItem).OrderBy(p => p.Category).ThenBy(p => p.ProductName)
            .Select(p => new { p.Id, p.ProductName, p.Category, p.Price, p.KitchenStationId }).ToListAsync();
        var links = await _db.ProductModifierGroups.ToListAsync();
        var groups = await _db.ModifierGroups.Include(g => g.Modifiers).ToDictionaryAsync(g => g.Id);
        var result = new List<object>();
        foreach (var p in items)
        {
            var cost = await _restaurant.PlateCostAsync(p.Id);
            result.Add(new
            {
                p.Id, p.ProductName, p.Category, p.Price, p.KitchenStationId, plateCost = cost,
                foodCostPct = p.Price > 0 ? Math.Round(cost / p.Price * 100, 1) : (decimal?)null,
                modifierGroups = links.Where(l => l.ProductId == p.Id).Select(l => groups.GetValueOrDefault(l.ModifierGroupId)).Where(g => g != null)
                    .Select(g => new { g!.Id, g.Name, g.MinSelect, g.MaxSelect, modifiers = g.Modifiers.Select(m => new { m.Id, m.Name, m.Price }) })
            });
        }
        return Ok(result);
    }

    [HttpGet("recipes/{productId:int}")]
    public async Task<IActionResult> Recipe(int productId)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId);
        if (p == null) return NotFound();
        var r = await _db.Recipes.Include(x => x.Items).FirstOrDefaultAsync(x => x.ProductId == productId);
        var ids = r?.Items.Select(i => i.IngredientId).ToList() ?? new();
        var ings = await _db.Products.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var groups = await _db.ProductModifierGroups.Where(x => x.ProductId == productId).Select(x => x.ModifierGroupId).ToListAsync();
        return Ok(new
        {
            product = new { p.Id, p.ProductName, p.Price, p.IsMenuItem, p.KitchenStationId },
            yieldQty = r?.YieldQty ?? 1, notes = r?.Notes, modifierGroupIds = groups,
            plateCost = await _restaurant.PlateCostAsync(productId),
            items = r?.Items.Select(i => new
            {
                i.Id, i.IngredientId, ingredient = ings.GetValueOrDefault(i.IngredientId)?.ProductName, i.Quantity, i.Unit, i.WastagePct,
                unitCost = ings.GetValueOrDefault(i.IngredientId)?.PurchasePrice ?? 0,
                lineCost = Math.Round(i.Quantity * (1 + i.WastagePct / 100m) * (ings.GetValueOrDefault(i.IngredientId)?.PurchasePrice ?? 0), 2)
            }) ?? Enumerable.Empty<object>()
        });
    }

    /// <summary>Saves a dish's recipe; the product becomes a menu item.</summary>
    [HttpPut("recipes/{productId:int}")]
    public async Task<IActionResult> SaveRecipe(int productId, [FromBody] RecipeDto dto)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId) ?? throw new KeyNotFoundException("Product not found");
        if (dto.YieldQty <= 0) throw new BusinessRuleException("Portions per recipe must be above zero.");
        if (dto.Items.Any(i => i.Quantity <= 0 || i.IngredientId == productId)) throw new BusinessRuleException("Each ingredient needs a quantity, and a dish cannot contain itself.");
        if (p.Quantity != 0 && !p.IsMenuItem)
            throw new BusinessRuleException($"{p.ProductName} holds stock ({p.Quantity}). Dishes carry no stock of their own; use a separate product for the menu item.");
        var r = await _db.Recipes.Include(x => x.Items).FirstOrDefaultAsync(x => x.ProductId == productId);
        if (r == null) { r = new Recipe { ProductId = productId }; _db.Recipes.Add(r); }
        else _db.RecipeItems.RemoveRange(r.Items);
        r.YieldQty = dto.YieldQty;
        r.Notes = dto.Notes;
        r.Items = dto.Items.Select(i => new RecipeItem { IngredientId = i.IngredientId, Quantity = i.Quantity, Unit = string.IsNullOrWhiteSpace(i.Unit) ? "g" : i.Unit, WastagePct = Math.Clamp(i.WastagePct, 0, 99) }).ToList();
        p.IsMenuItem = true;
        p.KitchenStationId = dto.KitchenStationId;
        await _db.SaveChangesAsync();
        return Ok(new { plateCost = await _restaurant.PlateCostAsync(productId) });
    }

    [HttpDelete("recipes/{productId:int}")]
    public async Task<IActionResult> DeleteRecipe(int productId)
    {
        _db.Recipes.RemoveRange(_db.Recipes.Where(r => r.ProductId == productId));
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId);
        if (p != null) p.IsMenuItem = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("recipes/{productId:int}/modifier-groups")]
    public async Task<IActionResult> SetProductModifiers(int productId, [FromBody] List<int> groupIds)
    {
        _db.ProductModifierGroups.RemoveRange(_db.ProductModifierGroups.Where(x => x.ProductId == productId));
        foreach (var g in groupIds.Distinct()) _db.ProductModifierGroups.Add(new ProductModifierGroup { ProductId = productId, ModifierGroupId = g });
        await _db.SaveChangesAsync();
        return Ok(new { saved = groupIds.Distinct().Count() });
    }

    // ── Modifiers ──

    [HttpGet("modifier-groups")]
    public async Task<IActionResult> ModifierGroups() => Ok((await _db.ModifierGroups.Include(g => g.Modifiers).OrderBy(g => g.Name).ToListAsync())
        .Select(g => new { g.Id, g.Name, g.MinSelect, g.MaxSelect, modifiers = g.Modifiers.Select(m => new { m.Id, m.Name, m.Price, m.IngredientId, m.IngredientQty }) }));

    [HttpPost("modifier-groups")]
    public async Task<IActionResult> CreateGroup([FromBody] ModifierGroupDto dto) => Ok(await SaveGroupAsync(null, dto));

    [HttpPut("modifier-groups/{id:int}")]
    public async Task<IActionResult> UpdateGroup(int id, [FromBody] ModifierGroupDto dto) => Ok(await SaveGroupAsync(id, dto));

    private async Task<object> SaveGroupAsync(int? id, ModifierGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new BusinessRuleException("Enter a group name.");
        if (dto.MaxSelect < Math.Max(1, dto.MinSelect)) throw new BusinessRuleException("Maximum choices must be at least the minimum (and at least 1).");
        var g = id.HasValue ? await _db.ModifierGroups.Include(x => x.Modifiers).FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Group not found") : new ModifierGroup();
        if (id.HasValue) _db.Modifiers.RemoveRange(g.Modifiers);
        g.Name = dto.Name.Trim(); g.MinSelect = dto.MinSelect; g.MaxSelect = dto.MaxSelect;
        g.Modifiers = dto.Modifiers.Where(m => !string.IsNullOrWhiteSpace(m.Name)).Select(m => new Modifier { Name = m.Name.Trim(), Price = m.Price, IngredientId = m.IngredientId, IngredientQty = m.IngredientQty }).ToList();
        if (!id.HasValue) _db.ModifierGroups.Add(g);
        await _db.SaveChangesAsync();
        return new { g.Id };
    }

    [HttpDelete("modifier-groups/{id:int}")]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        _db.ProductModifierGroups.RemoveRange(_db.ProductModifierGroups.Where(x => x.ModifierGroupId == id));
        _db.ModifierGroups.RemoveRange(_db.ModifierGroups.Where(x => x.Id == id));
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Tables and stations ──

    [HttpGet("tables")]
    public async Task<IActionResult> Tables([FromQuery] int? locationId)
    {
        var loc = locationId ?? await _locations.PickAsync(null);
        var tables = await _db.DiningTables.Where(t => t.LocationId == loc).OrderBy(t => t.Name).ToListAsync();
        var ids = tables.Select(t => (int?)t.Id).ToList();
        // Bills still open on each occupied table (each kitchen round is its own bill).
        var since = DateTime.UtcNow.AddHours(-16);
        var open = await _db.Sales.Where(s => ids.Contains(s.DiningTableId) && s.SaleDate >= since && s.OrderType == "DineIn" && s.Due > 0)
            .OrderBy(s => s.Id).Select(s => new { s.Id, s.Reference, s.DiningTableId, s.GrandTotal, s.Due, s.Covers, s.SaleDate }).ToListAsync();
        return Ok(tables.Select(t =>
        {
            var bills = t.Status == "Occupied" ? open.Where(o => o.DiningTableId == t.Id).ToList() : new();
            return new { t.Id, t.Name, t.Seats, t.Status, t.LocationId, openBills = bills, due = bills.Sum(b => b.Due), currentBill = bills.LastOrDefault() };
        }));
    }

    [HttpPost("tables")]
    public async Task<IActionResult> CreateTable([FromBody] DiningTableDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new BusinessRuleException("Enter a table name.");
        var t = new DiningTable { LocationId = dto.LocationId > 0 ? dto.LocationId : await _locations.PickAsync(null), Name = dto.Name.Trim(), Seats = Math.Max(1, dto.Seats) };
        _db.DiningTables.Add(t);
        await _db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpPut("tables/{id:int}")]
    public async Task<IActionResult> UpdateTable(int id, [FromBody] DiningTableDto dto)
    {
        var t = await _db.DiningTables.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Table not found");
        t.Name = dto.Name.Trim(); t.Seats = Math.Max(1, dto.Seats);
        t.Status = dto.Status is "Occupied" or "Reserved" ? dto.Status : "Free";
        await _db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpDelete("tables/{id:int}")]
    public async Task<IActionResult> DeleteTable(int id)
    {
        _db.DiningTables.RemoveRange(_db.DiningTables.Where(x => x.Id == id));
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("stations")]
    public async Task<IActionResult> Stations([FromQuery] int? locationId)
    {
        var q = _db.KitchenStations.AsQueryable();
        if (locationId.HasValue) q = q.Where(s => s.LocationId == locationId);
        return Ok(await q.OrderBy(s => s.Name).ToListAsync());
    }

    [HttpPost("stations")]
    public async Task<IActionResult> CreateStation([FromBody] KitchenStationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new BusinessRuleException("Enter a station name.");
        var s = new KitchenStation { LocationId = dto.LocationId > 0 ? dto.LocationId : await _locations.PickAsync(null), Name = dto.Name.Trim(), PrinterName = dto.PrinterName };
        _db.KitchenStations.Add(s);
        await _db.SaveChangesAsync();
        return Ok(s);
    }

    [HttpPut("stations/{id:int}")]
    public async Task<IActionResult> UpdateStation(int id, [FromBody] KitchenStationDto dto)
    {
        var s = await _db.KitchenStations.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Station not found");
        s.Name = dto.Name.Trim(); s.PrinterName = dto.PrinterName;
        await _db.SaveChangesAsync();
        return Ok(s);
    }

    [HttpDelete("stations/{id:int}")]
    public async Task<IActionResult> DeleteStation(int id)
    {
        if (await _db.Products.AnyAsync(p => p.KitchenStationId == id)) throw new BusinessRuleException("Dishes are routed to this station. Move them first.");
        _db.KitchenStations.RemoveRange(_db.KitchenStations.Where(x => x.Id == id));
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Kitchen display ──

    [HttpGet("kitchen")]
    public async Task<IActionResult> Kitchen([FromQuery] int? stationId, [FromQuery] bool includeServed = false)
    {
        var q = _db.KitchenTickets.Include(t => t.Items).AsQueryable();
        if (stationId.HasValue) q = q.Where(t => t.StationId == stationId);
        if (!includeServed) q = q.Where(t => t.Status != "Served");
        var tickets = await q.OrderBy(t => t.CreatedAt).Take(200).ToListAsync();
        var saleIds = tickets.Select(t => t.SaleId).Distinct().ToList();
        var sales = await _db.Sales.Where(s => saleIds.Contains(s.Id)).Select(s => new { s.Id, s.Reference, s.OrderType, s.DiningTableId, s.CustomerName }).ToDictionaryAsync(s => s.Id);
        var tables = await _db.DiningTables.ToDictionaryAsync(t => t.Id, t => t.Name);
        var stations = await _db.KitchenStations.ToDictionaryAsync(s => s.Id, s => s.Name);
        var now = DateTime.UtcNow;
        return Ok(tickets.Select(t =>
        {
            var s = sales.GetValueOrDefault(t.SaleId);
            return new
            {
                t.Id, t.TicketNo, t.Status, t.StationId, station = stations.GetValueOrDefault(t.StationId), t.CreatedAt, t.ReadyAt,
                minutes = (int)(now - t.CreatedAt).TotalMinutes, bill = s?.Reference, orderType = s?.OrderType,
                table = s?.DiningTableId != null ? tables.GetValueOrDefault(s.DiningTableId.Value) : null, customer = s?.CustomerName,
                items = t.Items.Select(i => new { i.ItemName, i.Quantity, i.Notes })
            };
        }));
    }

    [HttpPost("kitchen/{id:int}/status")]
    public async Task<IActionResult> TicketStatus(int id, [FromBody] StatusDto dto)
    {
        var t = await _db.KitchenTickets.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Ticket not found");
        if (dto.Status is not ("New" or "Preparing" or "Ready" or "Served")) throw new BusinessRuleException("Unknown status.");
        t.Status = dto.Status;
        if (dto.Status == "Ready") t.ReadyAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { t.Id, t.Status });
    }

    /// <summary>Frees a table once its bill is settled.</summary>
    [HttpPost("tables/{id:int}/free")]
    public async Task<IActionResult> FreeTable(int id)
    {
        var t = await _db.DiningTables.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Table not found");
        t.Status = "Free";
        await _db.SaveChangesAsync();
        return Ok(t);
    }

    /// <summary>Food cost per dish against its price.</summary>
    [HttpGet("food-cost")]
    public async Task<IActionResult> FoodCost()
    {
        var items = await _db.Products.Where(p => p.IsMenuItem).Select(p => new { p.Id, p.ProductName, p.Category, p.Price }).ToListAsync();
        var rows = new List<object>();
        foreach (var p in items)
        {
            var cost = await _restaurant.PlateCostAsync(p.Id);
            rows.Add(new { p.Id, p.ProductName, p.Category, p.Price, plateCost = cost, margin = p.Price - cost, foodCostPct = p.Price > 0 ? Math.Round(cost / p.Price * 100, 1) : 0 });
        }
        return Ok(rows);
    }
}
