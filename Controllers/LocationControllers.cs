using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>Branches, warehouses and the head-office view of stock across them.</summary>
[Authorize]
[ApiController]
[Route("api/locations")]
public class LocationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILocationService _locations;
    private readonly IAuditService _audit;

    public LocationsController(AppDbContext db, ILocationService locations, IAuditService audit)
    {
        _db = db;
        _locations = locations;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false)
        => Ok(await _locations.ListAsync(includeInactive));

    /// <summary>The signed-in user's branch (or the default) and whether they may switch.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var (home, restricted) = await _locations.UserLocationAsync();
        var id = await _locations.PickAsync(null);
        var loc = await _db.Locations.FirstAsync(l => l.Id == id);
        return Ok(new { location = loc, homeLocationId = home, restricted });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LocationDto dto)
    {
        if (!_audit.IsAdmin) return Forbid();
        return Ok(await _locations.SaveAsync(null, dto));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] LocationDto dto)
    {
        if (!_audit.IsAdmin) return Forbid();
        return Ok(await _locations.SaveAsync(id, dto));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_audit.IsAdmin) return Forbid();
        await _locations.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>One product's stock at every location, by batch.</summary>
    [HttpGet("product-stock/{productId:int}")]
    public async Task<IActionResult> ProductStock(int productId)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return NotFound();
        var locations = await _db.Locations.ToDictionaryAsync(l => l.Id, l => l.Name);
        var batches = await _db.ProductBatches.Where(b => b.ProductId == productId && b.Quantity != 0).ToListAsync();
        var today = DateTime.UtcNow.Date;
        var rows = batches.GroupBy(b => b.LocationId).Select(g => new
        {
            locationId = g.Key,
            location = g.Key.HasValue ? locations.GetValueOrDefault(g.Key.Value) : "Not assigned",
            quantity = g.Sum(b => b.Quantity),
            sellable = g.Where(b => b.ExpiryDate == null || b.ExpiryDate.Value.Date >= today).Sum(b => b.Quantity),
            batches = g.OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue).Select(b => new
            {
                b.Id, b.BatchNo, b.VariantId, expiryDate = b.ExpiryDate?.ToString("yyyy-MM-dd"), b.Quantity, b.PurchasePrice
            })
        }).OrderBy(r => r.location).ToList();
        var noBatch = product.Quantity - batches.Sum(b => b.Quantity);
        return Ok(new { productId, product.ProductName, total = product.Quantity, noBatch, locations = rows });
    }

    /// <summary>Head office: products down, locations across.</summary>
    [HttpGet("stock-matrix")]
    public async Task<IActionResult> StockMatrix([FromQuery] string? search, [FromQuery] string? category, [FromQuery] bool lowOnly = false, [FromQuery] int take = 300)
    {
        var locs = await _locations.ListAsync();
        var q = _db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.ProductName.Contains(search) || (p.GenericName != null && p.GenericName.Contains(search)) || (p.SKU != null && p.SKU.Contains(search)));
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(p => p.Category == category);
        var products = await q.OrderBy(p => p.ProductName).Take(Math.Clamp(take, 1, 2000))
            .Select(p => new { p.Id, p.ProductName, p.GenericName, p.SKU, p.Category, p.Quantity, p.PurchasePrice, p.Price, p.QuantityAlert }).ToListAsync();
        var ids = products.Select(p => p.Id).ToList();
        var sums = await _db.ProductBatches.Where(b => ids.Contains(b.ProductId))
            .GroupBy(b => new { b.ProductId, b.LocationId })
            .Select(g => new { g.Key.ProductId, g.Key.LocationId, Qty = g.Sum(b => b.Quantity) }).ToListAsync();
        var levels = await _db.LocationStockLevels.Where(l => ids.Contains(l.ProductId)).ToListAsync();

        var rows = products.Select(p =>
        {
            var perLoc = locs.ToDictionary(l => l.Id.ToString(), l => sums.Where(s => s.ProductId == p.Id && s.LocationId == l.Id).Sum(s => s.Qty));
            var inBatches = sums.Where(s => s.ProductId == p.Id).Sum(s => s.Qty);
            var low = locs.Where(l => levels.Any(v => v.ProductId == p.Id && v.LocationId == l.Id && perLoc[l.Id.ToString()] < v.MinQty)).Select(l => l.Id).ToList();
            return new { p.Id, p.ProductName, p.GenericName, p.SKU, p.Category, total = p.Quantity, noBatch = p.Quantity - inBatches, costValue = p.Quantity * p.PurchasePrice, perLocation = perLoc, lowAt = low };
        }).Where(r => !lowOnly || r.lowAt.Count > 0).ToList();
        return Ok(new { locations = locs.Select(l => new { l.Id, l.Code, l.Name, l.Type }), rows });
    }

    /// <summary>Head office: stock value, expiry exposure and sales per location.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] string? from, [FromQuery] string? to)
    {
        var locs = await _locations.ListAsync();
        var start = DateTime.TryParse(from, out var f) ? f.Date : DateTime.UtcNow.Date.AddDays(-29);
        var end = (DateTime.TryParse(to, out var t) ? t.Date : DateTime.UtcNow.Date).AddDays(1);
        var today = DateTime.UtcNow.Date;
        var soon = today.AddDays(90);

        var stock = await _db.ProductBatches.Where(b => b.Quantity > 0)
            .GroupBy(b => b.LocationId)
            .Select(g => new
            {
                LocationId = g.Key,
                Units = g.Sum(b => b.Quantity),
                Value = g.Sum(b => b.Quantity * b.PurchasePrice),
                Expired = g.Where(b => b.ExpiryDate != null && b.ExpiryDate < today).Sum(b => b.Quantity * b.PurchasePrice),
                Near = g.Where(b => b.ExpiryDate != null && b.ExpiryDate >= today && b.ExpiryDate <= soon).Sum(b => b.Quantity * b.PurchasePrice)
            }).ToListAsync();
        var sales = await _db.Sales.Where(s => s.SaleDate >= start && s.SaleDate < end)
            .GroupBy(s => s.LocationId)
            .Select(g => new { LocationId = g.Key, Bills = g.Count(), Total = g.Sum(s => s.GrandTotal), Due = g.Sum(s => s.Due) }).ToListAsync();
        var inTransit = await _db.StockTransfers.Where(x => x.Status == "Dispatched")
            .GroupBy(x => x.ToLocationId).Select(g => new { LocationId = g.Key, Count = g.Count() }).ToListAsync();
        var openRequests = await _db.StockRequests.Where(x => x.Status == "Submitted")
            .GroupBy(x => x.FromLocationId).Select(g => new { LocationId = g.Key, Count = g.Count() }).ToListAsync();

        var rows = locs.Select(l =>
        {
            var s = stock.FirstOrDefault(x => x.LocationId == l.Id);
            var sa = sales.FirstOrDefault(x => x.LocationId == l.Id);
            return new
            {
                l.Id, l.Code, l.Name, l.Type, l.IsDefault,
                stockUnits = s?.Units ?? 0, stockValue = Math.Round(s?.Value ?? 0, 2),
                expiredValue = Math.Round(s?.Expired ?? 0, 2), nearExpiryValue = Math.Round(s?.Near ?? 0, 2),
                bills = sa?.Bills ?? 0, sales = sa?.Total ?? 0, due = sa?.Due ?? 0,
                incomingTransfers = inTransit.FirstOrDefault(x => x.LocationId == l.Id)?.Count ?? 0,
                requestsToFulfil = openRequests.FirstOrDefault(x => x.LocationId == l.Id)?.Count ?? 0
            };
        }).ToList();
        var unassigned = sales.FirstOrDefault(x => x.LocationId == null);
        return Ok(new
        {
            from = start.ToString("yyyy-MM-dd"), to = end.AddDays(-1).ToString("yyyy-MM-dd"), rows,
            salesWithoutLocation = unassigned == null ? null : new { unassigned.Bills, unassigned.Total }
        });
    }

    [HttpGet("{id:int}/levels")]
    public async Task<IActionResult> Levels(int id)
    {
        var rows = await (from l in _db.LocationStockLevels
                          join p in _db.Products on l.ProductId equals p.Id
                          where l.LocationId == id
                          orderby p.ProductName
                          select new { l.ProductId, p.ProductName, l.MinQty, l.MaxQty }).ToListAsync();
        return Ok(rows);
    }

    /// <summary>Saves min/max per product for a location (rows with both at 0 are removed).</summary>
    [HttpPut("{id:int}/levels")]
    public async Task<IActionResult> SaveLevels(int id, [FromBody] List<StockLevelDto> rows)
    {
        if (!await _db.Locations.AnyAsync(l => l.Id == id)) return NotFound();
        var existing = await _db.LocationStockLevels.Where(l => l.LocationId == id).ToListAsync();
        foreach (var r in rows)
        {
            if (r.MinQty < 0 || r.MaxQty < 0) throw new BusinessRuleException("Levels cannot be negative.");
            var row = existing.FirstOrDefault(e => e.ProductId == r.ProductId);
            if (r.MinQty == 0 && r.MaxQty == 0)
            {
                if (row != null) _db.LocationStockLevels.Remove(row);
                continue;
            }
            if (row == null)
            {
                row = new LocationStockLevel { LocationId = id, ProductId = r.ProductId };
                _db.LocationStockLevels.Add(row);
                existing.Add(row);
            }
            row.MinQty = r.MinQty;
            row.MaxQty = Math.Max(r.MaxQty, r.MinQty);
        }
        await _db.SaveChangesAsync();
        return Ok(new { saved = rows.Count });
    }
}

/// <summary>The stock ledger: every movement with its source document.</summary>
[Authorize]
[ApiController]
[Route("api/stock-movements")]
public class StockMovementsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StockMovementsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? productId, [FromQuery] int? locationId, [FromQuery] string? type,
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? reference, [FromQuery] int take = 500)
    {
        var q = _db.StockMovements.AsQueryable();
        if (productId.HasValue) q = q.Where(m => m.ProductId == productId);
        if (locationId.HasValue) q = q.Where(m => m.LocationId == locationId);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(m => m.MovementType == type);
        if (!string.IsNullOrWhiteSpace(reference)) q = q.Where(m => m.Reference != null && m.Reference.Contains(reference));
        if (DateTime.TryParse(from, out var f)) q = q.Where(m => m.CreatedAt >= f.Date);
        if (DateTime.TryParse(to, out var t)) q = q.Where(m => m.CreatedAt < t.Date.AddDays(1));

        var rows = await (from m in q
                          join p in _db.Products on m.ProductId equals p.Id into pj
                          from p in pj.DefaultIfEmpty()
                          join l in _db.Locations on m.LocationId equals l.Id into lj
                          from l in lj.DefaultIfEmpty()
                          join b in _db.ProductBatches on m.BatchId equals b.Id into bj
                          from b in bj.DefaultIfEmpty()
                          orderby m.Id descending
                          select new
                          {
                              m.Id, m.ProductId, productName = p != null ? p.ProductName : "", m.VariantId,
                              m.LocationId, location = l != null ? l.Name : null, m.BatchId, batchNo = b != null ? b.BatchNo : null,
                              m.Quantity, m.UnitCost, m.MovementType, m.SourceType, m.SourceId, m.Reference, m.CreatedBy, m.CreatedAt
                          }).Take(Math.Clamp(take, 1, 5000)).ToListAsync();
        return Ok(rows);
    }

    /// <summary>Checks that the batches still add up to each product's total.</summary>
    [HttpGet("reconcile")]
    public async Task<IActionResult> Reconcile()
    {
        var sums = await _db.ProductBatches.GroupBy(b => b.ProductId).Select(g => new { ProductId = g.Key, Qty = g.Sum(b => b.Quantity) }).ToListAsync();
        var products = await _db.Products.Select(p => new { p.Id, p.ProductName, p.Quantity }).ToListAsync();
        var off = products.Select(p => new { p.Id, p.ProductName, total = p.Quantity, inBatches = sums.FirstOrDefault(s => s.ProductId == p.Id)?.Qty ?? 0 })
            .Where(x => x.total != x.inBatches).ToList();
        return Ok(new { products = products.Count, mismatched = off.Count, rows = off });
    }
}

[Authorize]
[ApiController]
[Route("api/stock-requests")]
public class StockRequestsController : ControllerBase
{
    private readonly IStockRequestService _service;
    public StockRequestsController(IStockRequestService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? locationId) => Ok(await _service.ListAsync(status, locationId));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StockRequestDto dto) => Ok(await _service.CreateAsync(dto));

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id) => Ok(await _service.SubmitAsync(id));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveStockRequestDto dto) => Ok(await _service.ApproveAsync(id, dto));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectDto dto) => Ok(await _service.RejectAsync(id, dto.Reason));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Raises requests for every branch product below its minimum.</summary>
    [HttpPost("generate-reorder")]
    public async Task<IActionResult> Generate() => Ok(await _service.GenerateReorderAsync());
}
