using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

public class BomDto
{
    public string Name { get; set; } = string.Empty;
    public int FinishedProductId { get; set; }
    public int OutputQuantity { get; set; } = 1;
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public int? ShelfLifeDays { get; set; }
    public decimal ExpectedYieldPct { get; set; } = 100;
    public string? Notes { get; set; }
    public string Status { get; set; } = "active";
    public List<BomItemDto> Items { get; set; } = new();
    public List<BomOperationDto> Operations { get; set; } = new();
}

public class BomItemDto
{
    public int RawMaterialId { get; set; }
    /// <summary>Base units needed for one run of OutputQuantity.</summary>
    public decimal Quantity { get; set; }
    public int? SupplierId { get; set; }
}

public class BomOperationDto
{
    public int WorkCentreId { get; set; }
    public string? Description { get; set; }
    public int? PlannedMinutes { get; set; }
}

public class WorkCentreDto
{
    public string Name { get; set; } = string.Empty;
    public int? LocationId { get; set; }
    public decimal CostPerHour { get; set; }
}

public class CreateMoDto
{
    public int BomId { get; set; }
    public int Quantity { get; set; } = 1;
    public int? LocationId { get; set; }
    public int? OutputLocationId { get; set; }
    public string? OutputBatchNo { get; set; }
    public string? OutputExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public class CompleteMoDto
{
    public int? ActualOutputQty { get; set; }
    public int ScrapQty { get; set; }
    public string? OutputBatchNo { get; set; }
    public string? OutputExpiryDate { get; set; }
    public List<CompleteMoLineDto> Items { get; set; } = new();
}

public class CompleteMoLineDto
{
    public int ItemId { get; set; }
    /// <summary>Base units actually used (empty = the planned quantity).</summary>
    public decimal? ConsumedQty { get; set; }
    public decimal WastageQty { get; set; }
}

/// <summary>
/// Bills of materials and production orders. Materials are taken earliest-expiry first at the
/// production location; the output becomes its own batch with expiry and actual cost.
/// </summary>
[Authorize]
[ApiController]
[Route("api/manufacturing")]
public class ManufacturingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ILocationService _locations;
    private readonly IStockRequestService _requests;
    private readonly IAuditService _audit;

    public ManufacturingController(AppDbContext db, IInventoryService inventory, ILocationService locations, IStockRequestService requests, IAuditService audit)
    {
        _db = db;
        _inventory = inventory;
        _locations = locations;
        _requests = requests;
        _audit = audit;
    }

    // ── Work centres ──

    [HttpGet("work-centres")]
    public async Task<IActionResult> WorkCentres() => Ok(await _db.WorkCentres.OrderBy(w => w.Name).ToListAsync());

    [HttpPost("work-centres")]
    public async Task<IActionResult> CreateWorkCentre([FromBody] WorkCentreDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new BusinessRuleException("Enter a name.");
        var w = new WorkCentre { Name = dto.Name.Trim(), LocationId = dto.LocationId, CostPerHour = dto.CostPerHour };
        _db.WorkCentres.Add(w);
        await _db.SaveChangesAsync();
        return Ok(w);
    }

    [HttpPut("work-centres/{id:int}")]
    public async Task<IActionResult> UpdateWorkCentre(int id, [FromBody] WorkCentreDto dto)
    {
        var w = await _db.WorkCentres.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Work centre not found");
        w.Name = dto.Name.Trim(); w.LocationId = dto.LocationId; w.CostPerHour = dto.CostPerHour;
        await _db.SaveChangesAsync();
        return Ok(w);
    }

    [HttpDelete("work-centres/{id:int}")]
    public async Task<IActionResult> DeleteWorkCentre(int id)
    {
        if (await _db.BomOperations.AnyAsync(o => o.WorkCentreId == id) || await _db.MoOperations.AnyAsync(o => o.WorkCentreId == id))
            throw new BusinessRuleException("This work centre is used by a recipe or an order.");
        _db.WorkCentres.RemoveRange(_db.WorkCentres.Where(w => w.Id == id));
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Bills of materials ──

    [HttpGet("bom")]
    public async Task<IActionResult> Boms()
    {
        var boms = await _db.BillsOfMaterials.Include(b => b.Items).Include(b => b.Operations).OrderBy(b => b.Name).ToListAsync();
        var ids = boms.SelectMany(b => b.Items.Select(i => i.RawMaterialId)).Concat(boms.Where(b => b.FinishedProductId != null).Select(b => b.FinishedProductId!.Value)).Distinct().ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        return Ok(boms.Select(b => MapBom(b, products)));
    }

    [HttpGet("bom/{id:int}")]
    public async Task<IActionResult> Bom(int id)
    {
        var b = await _db.BillsOfMaterials.Include(x => x.Items).Include(x => x.Operations).FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound();
        var ids = b.Items.Select(i => i.RawMaterialId).Append(b.FinishedProductId ?? 0).ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        return Ok(MapBom(b, products));
    }

    private static object MapBom(BillOfMaterials b, Dictionary<int, Product> products)
    {
        // Estimated cost uses each material's current trade price per base unit.
        var materials = b.Items.Sum(i => i.Quantity * (products.GetValueOrDefault(i.RawMaterialId)?.PurchasePrice ?? i.UnitCost));
        var perUnit = b.OutputQuantity > 0 ? (materials + b.LaborCost + b.OverheadCost) / b.OutputQuantity : 0;
        var finished = b.FinishedProductId.HasValue ? products.GetValueOrDefault(b.FinishedProductId.Value) : null;
        return new
        {
            b.Id, b.Name, b.FinishedProductId, finishedProduct = finished?.ProductName ?? b.FinishedProductName, b.OutputQuantity,
            b.LaborCost, b.OverheadCost, b.ShelfLifeDays, b.ExpectedYieldPct, b.Notes, b.Status, b.Version,
            estimatedMaterialCost = Math.Round(materials, 2), estimatedUnitCost = Math.Round(perUnit, 4),
            sellingPrice = finished?.Price, estimatedMarginPct = finished?.Price > 0 ? Math.Round((finished.Price - perUnit) / finished.Price * 100, 1) : (decimal?)null,
            items = b.Items.Select(i => new
            {
                i.Id, i.RawMaterialId, rawMaterial = products.GetValueOrDefault(i.RawMaterialId)?.ProductName, i.Quantity, i.SupplierId,
                unitCost = products.GetValueOrDefault(i.RawMaterialId)?.PurchasePrice ?? i.UnitCost
            }),
            operations = b.Operations.OrderBy(o => o.Sequence).Select(o => new { o.Id, o.Sequence, o.WorkCentreId, o.Description, o.PlannedMinutes })
        };
    }

    [HttpPost("bom")]
    public async Task<IActionResult> CreateBom([FromBody] BomDto dto) => Ok(new { id = (await SaveBomAsync(null, dto)).Id });

    [HttpPut("bom/{id:int}")]
    public async Task<IActionResult> UpdateBom(int id, [FromBody] BomDto dto) => Ok(new { id = (await SaveBomAsync(id, dto)).Id });

    private async Task<BillOfMaterials> SaveBomAsync(int? id, BomDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new BusinessRuleException("Enter a recipe name.");
        if (dto.OutputQuantity <= 0) throw new BusinessRuleException("Output quantity must be above zero.");
        if (dto.Items.Count == 0 || dto.Items.Any(i => i.Quantity <= 0)) throw new BusinessRuleException("Add raw materials with quantities above zero.");
        var finished = await _db.Products.FirstOrDefaultAsync(p => p.Id == dto.FinishedProductId) ?? throw new BusinessRuleException("Choose the finished product.");
        if (dto.Items.Any(i => i.RawMaterialId == dto.FinishedProductId)) throw new BusinessRuleException("A product cannot be its own raw material.");
        var matIds = dto.Items.Select(i => i.RawMaterialId).ToList();
        var mats = await _db.Products.Where(p => matIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        if (mats.Count != matIds.Distinct().Count()) throw new BusinessRuleException("A raw material was not found.");

        var b = id.HasValue ? await _db.BillsOfMaterials.Include(x => x.Items).Include(x => x.Operations).FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Recipe not found")
                            : new BillOfMaterials();
        if (id.HasValue)
        {
            _db.BomItems.RemoveRange(b.Items);
            _db.BomOperations.RemoveRange(b.Operations);
            b.Version++;
        }
        b.Name = dto.Name.Trim(); b.FinishedProductId = finished.Id; b.FinishedProductName = finished.ProductName;
        b.FinishedProductCategory = finished.Category; b.FinishedProductSubCategory = finished.SubCategory; b.SalePrice = finished.Price;
        b.OutputQuantity = dto.OutputQuantity; b.LaborCost = dto.LaborCost; b.OverheadCost = dto.OverheadCost;
        b.ShelfLifeDays = dto.ShelfLifeDays; b.ExpectedYieldPct = dto.ExpectedYieldPct is > 0 and <= 100 ? dto.ExpectedYieldPct : 100;
        b.Notes = dto.Notes; b.Status = dto.Status == "inactive" ? "inactive" : "active"; b.UpdatedAt = DateTime.UtcNow;
        b.Items = dto.Items.Select(i => new BomItem { RawMaterialId = i.RawMaterialId, Quantity = i.Quantity, UnitCost = Math.Round(mats[i.RawMaterialId].PurchasePrice, 2), SupplierId = i.SupplierId }).ToList();
        b.Operations = dto.Operations.Select((o, n) => new BomOperation { Sequence = n + 1, WorkCentreId = o.WorkCentreId, Description = o.Description, PlannedMinutes = o.PlannedMinutes }).ToList();
        if (!id.HasValue) _db.BillsOfMaterials.Add(b);
        await _db.SaveChangesAsync();
        return b;
    }

    [HttpDelete("bom/{id:int}")]
    public async Task<IActionResult> DeleteBom(int id)
    {
        if (await _db.ManufacturingOrders.AnyAsync(o => o.BomId == id))
        {
            var b = await _db.BillsOfMaterials.FirstAsync(x => x.Id == id);
            b.Status = "inactive"; // orders refer to it
        }
        else _db.BillsOfMaterials.RemoveRange(_db.BillsOfMaterials.Where(b => b.Id == id));
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Production orders ──

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] string? status)
    {
        var q = _db.ManufacturingOrders.Include(o => o.Items).Include(o => o.Operations).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
        var orders = await q.OrderByDescending(o => o.Id).Take(500).ToListAsync();
        return Ok(await MapOrdersAsync(orders));
    }

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> Order(int id)
    {
        var o = await _db.ManufacturingOrders.Include(x => x.Items).Include(x => x.Operations).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        return Ok((await MapOrdersAsync(new List<ManufacturingOrder> { o })).First());
    }

    private async Task<List<object>> MapOrdersAsync(List<ManufacturingOrder> orders)
    {
        var pids = orders.SelectMany(o => o.Items.Select(i => i.RawMaterialId)).Concat(orders.Where(o => o.FinishedProductId != null).Select(o => o.FinishedProductId!.Value)).Distinct().ToList();
        var products = await _db.Products.Where(p => pids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var boms = await _db.BillsOfMaterials.ToDictionaryAsync(b => b.Id, b => b.Name);
        var locs = await _db.Locations.ToDictionaryAsync(l => l.Id, l => l.Name);
        var centres = await _db.WorkCentres.ToDictionaryAsync(w => w.Id, w => w.Name);
        var result = new List<object>();
        foreach (var o in orders)
        {
            var lines = new List<object>();
            foreach (var i in o.Items)
            {
                var p = products.GetValueOrDefault(i.RawMaterialId);
                var available = p == null || o.Status is "Completed" or "Cancelled" ? (int?)null : await _inventory.AvailableAsync(p, o.LocationId, null, sellableOnly: true);
                lines.Add(new
                {
                    i.Id, i.RawMaterialId, rawMaterial = p?.ProductName, i.RequiredQuantity, i.ConsumedQuantity, i.WastageQty, i.UnitCost, i.TotalCost,
                    available, shortBy = available.HasValue ? Math.Max(0, (int)Math.Ceiling(i.RequiredQuantity) - available.Value) : 0
                });
            }
            result.Add(new
            {
                o.Id, o.Reference, o.BomId, bom = boms.GetValueOrDefault(o.BomId), o.FinishedProductId,
                finishedProduct = o.FinishedProductId.HasValue ? products.GetValueOrDefault(o.FinishedProductId.Value)?.ProductName : null,
                o.Quantity, o.Status, o.LocationId, location = o.LocationId.HasValue ? locs.GetValueOrDefault(o.LocationId.Value) : null,
                o.OutputLocationId, outputLocation = o.OutputLocationId.HasValue ? locs.GetValueOrDefault(o.OutputLocationId.Value) : null,
                o.OutputBatchNo, outputExpiryDate = o.OutputExpiryDate?.ToString("yyyy-MM-dd"), o.ActualOutputQty, o.ScrapQty,
                yieldPct = o.ActualOutputQty.HasValue && o.Quantity > 0 ? Math.Round(o.ActualOutputQty.Value * 100m / o.Quantity, 1) : (decimal?)null,
                o.LaborCost, o.OverheadCost, o.TotalMaterialCost, o.TotalCost,
                unitCost = o.ActualOutputQty > 0 ? Math.Round(o.TotalCost / o.ActualOutputQty.Value, 4) : (decimal?)null,
                startDate = o.StartDate?.ToString("yyyy-MM-dd HH:mm"), completionDate = o.CompletionDate?.ToString("yyyy-MM-dd HH:mm"),
                o.Notes, items = lines,
                operations = o.Operations.OrderBy(x => x.Sequence).Select(x => new
                {
                    x.Id, x.Sequence, x.WorkCentreId, workCentre = centres.GetValueOrDefault(x.WorkCentreId), x.Description, x.Status, x.Operator,
                    startedAt = x.StartedAt?.ToString("yyyy-MM-dd HH:mm"), completedAt = x.CompletedAt?.ToString("yyyy-MM-dd HH:mm")
                })
            });
        }
        return result;
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateMoDto dto)
    {
        var bom = await _db.BillsOfMaterials.Include(b => b.Items).Include(b => b.Operations).FirstOrDefaultAsync(b => b.Id == dto.BomId)
                  ?? throw new BusinessRuleException("Choose a recipe.");
        if (bom.Status != "active") throw new BusinessRuleException("This recipe is inactive.");
        if (dto.Quantity <= 0) throw new BusinessRuleException("Quantity must be above zero.");
        var location = await _locations.PickAsync(dto.LocationId);
        var n = await _db.ManufacturingOrders.CountAsync() + 1;
        string reference;
        do { reference = $"MO{n++:D4}"; } while (await _db.ManufacturingOrders.AnyAsync(o => o.Reference == reference));

        var factor = (decimal)dto.Quantity / bom.OutputQuantity;
        var order = new ManufacturingOrder
        {
            Reference = reference, BomId = bom.Id, FinishedProductId = bom.FinishedProductId, Quantity = dto.Quantity,
            LocationId = location, OutputLocationId = dto.OutputLocationId ?? location, Status = "Draft",
            OutputBatchNo = string.IsNullOrWhiteSpace(dto.OutputBatchNo) ? reference : dto.OutputBatchNo.Trim(),
            OutputExpiryDate = DateTime.TryParse(dto.OutputExpiryDate, out var e) ? e.Date : bom.ShelfLifeDays.HasValue ? DateTime.UtcNow.Date.AddDays(bom.ShelfLifeDays.Value) : null,
            LaborCost = Math.Round(bom.LaborCost * factor, 2), OverheadCost = Math.Round(bom.OverheadCost * factor, 2), Notes = dto.Notes,
            Items = bom.Items.Select(i => new ManufacturingOrderItem
            {
                RawMaterialId = i.RawMaterialId, RequiredQuantity = Math.Round(i.Quantity * factor, 4), UnitCost = i.UnitCost,
                TotalCost = Math.Round(i.Quantity * factor * i.UnitCost, 2), SupplierId = i.SupplierId
            }).ToList(),
            Operations = bom.Operations.OrderBy(o => o.Sequence).Select(o => new MoOperation { Sequence = o.Sequence, WorkCentreId = o.WorkCentreId, Description = o.Description }).ToList()
        };
        _db.ManufacturingOrders.Add(order);
        await _db.SaveChangesAsync();
        return Ok(new { order.Id, order.Reference });
    }

    [HttpPost("orders/{id:int}/start")]
    public async Task<IActionResult> Start(int id)
    {
        var o = await _db.ManufacturingOrders.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Order not found");
        if (o.Status != "Draft") throw new BusinessRuleException($"Order {o.Reference} is {o.Status}.");
        o.Status = "InProgress";
        o.StartDate = DateTime.UtcNow;
        o.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { o.Id, o.Status });
    }

    [HttpPost("orders/{id:int}/operations/{opId:int}/{step}")]
    public async Task<IActionResult> Operation(int id, int opId, string step)
    {
        var op = await _db.MoOperations.FirstOrDefaultAsync(x => x.Id == opId && x.OrderId == id) ?? throw new KeyNotFoundException("Step not found");
        var order = await _db.ManufacturingOrders.FirstAsync(x => x.Id == id);
        if (order.Status is "Completed" or "Cancelled") throw new BusinessRuleException($"Order {order.Reference} is {order.Status}.");
        if (order.Status == "Draft") { order.Status = "InProgress"; order.StartDate = DateTime.UtcNow; }
        switch (step)
        {
            case "start": op.Status = "Running"; op.StartedAt = DateTime.UtcNow; op.Operator = _audit.CurrentUser; break;
            case "finish": op.Status = "Done"; op.StartedAt ??= DateTime.UtcNow; op.CompletedAt = DateTime.UtcNow; op.Operator ??= _audit.CurrentUser; break;
            default: return BadRequest(new { message = "Use start or finish." });
        }
        await _db.SaveChangesAsync();
        return Ok(new { op.Id, op.Sequence, op.WorkCentreId, op.Status, op.Operator, op.StartedAt, op.CompletedAt });
    }

    /// <summary>Asks the warehouse for whatever materials the production location is short of.</summary>
    [HttpPost("orders/{id:int}/request-materials")]
    public async Task<IActionResult> RequestMaterials(int id, [FromQuery] int? fromLocationId)
    {
        var o = await _db.ManufacturingOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Order not found");
        var items = new List<StockRequestItemDto>();
        foreach (var i in o.Items)
        {
            var p = await _db.Products.FirstAsync(x => x.Id == i.RawMaterialId);
            var short_ = (int)Math.Ceiling(i.RequiredQuantity) - await _inventory.AvailableAsync(p, o.LocationId, null);
            if (short_ > 0) items.Add(new StockRequestItemDto { ProductId = p.Id, RequestedQty = short_ });
        }
        if (items.Count == 0) return Ok(new { message = "All materials are in stock at the production location." });
        return Ok(await _requests.CreateAsync(new StockRequestDto { FromLocationId = fromLocationId, ToLocationId = o.LocationId, Notes = $"Materials for {o.Reference}", Items = items }));
    }

    [HttpPost("orders/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteMoDto dto)
    {
        var o = await _db.ManufacturingOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Order not found");
        if (o.Status is "Completed" or "Cancelled") throw new BusinessRuleException($"Order {o.Reference} is already {o.Status.ToLower()}.");
        var output = dto.ActualOutputQty ?? o.Quantity;
        if (output < 0 || dto.ScrapQty < 0) throw new BusinessRuleException("Quantities cannot be negative.");
        var finished = await _db.Products.FirstOrDefaultAsync(p => p.Id == o.FinishedProductId) ?? throw new BusinessRuleException("The finished product no longer exists.");

        using var tx = await _db.Database.BeginTransactionAsync();
        var consumeAt = new StockRef("Consumption", o.Reference, o.Id, o.LocationId, null, "ManufacturingOrder");
        decimal materialCost = 0;
        foreach (var line in o.Items)
        {
            var asked = dto.Items.FirstOrDefault(x => x.ItemId == line.Id);
            var used = asked?.ConsumedQty ?? line.RequiredQuantity;
            var wastage = asked?.WastageQty ?? 0;
            var qty = (int)Math.Ceiling(used + wastage);
            var p = await _db.Products.FirstAsync(x => x.Id == line.RawMaterialId);
            var available = await _inventory.AvailableAsync(p, o.LocationId, null);
            if (available < qty) throw new BusinessRuleException($"{p.ProductName}: {available} unexpired at the production location, {qty} needed. Request materials or adjust quantities.");
            var taken = await _inventory.DeductFefoAsync(p, qty, consumeAt);
            await _db.SaveChangesAsync();
            foreach (var a in taken) _db.MoItemBatches.Add(new MoItemBatch { MoItemId = line.Id, BatchId = a.BatchId, Quantity = a.Quantity, UnitCost = a.UnitCost });
            line.ConsumedQuantity = used;
            line.WastageQty = wastage;
            line.UnitCost = Math.Round(_inventory.AverageCost(taken, p.PurchasePrice), 2);
            line.TotalCost = Math.Round(taken.Sum(a => a.UnitCost * a.Quantity), 2);
            materialCost += line.TotalCost;
        }

        o.TotalMaterialCost = Math.Round(materialCost, 2);
        o.TotalCost = o.TotalMaterialCost + o.LaborCost + o.OverheadCost;
        o.ActualOutputQty = output;
        o.ScrapQty = dto.ScrapQty;
        if (!string.IsNullOrWhiteSpace(dto.OutputBatchNo)) o.OutputBatchNo = dto.OutputBatchNo.Trim();
        if (DateTime.TryParse(dto.OutputExpiryDate, out var exp)) o.OutputExpiryDate = exp.Date;

        if (output > 0)
        {
            var unitCost = Math.Round(o.TotalCost / output, 4);
            var batch = await _inventory.AddStockAsync(finished, output, o.OutputBatchNo, o.OutputExpiryDate, unitCost,
                manufacturedDate: DateTime.UtcNow.Date, at: new StockRef("Production", o.Reference, o.Id, o.OutputLocationId ?? o.LocationId, null, "ManufacturingOrder"));
            await _db.SaveChangesAsync();
            o.OutputBatchId = batch.Id;
        }
        o.Status = "Completed";
        o.StartDate ??= DateTime.UtcNow;
        o.CompletionDate = DateTime.UtcNow;
        o.UpdatedAt = DateTime.UtcNow;
        await _db.MoOperations.Where(x => x.OrderId == o.Id && x.Status != "Done").ForEachAsync(x => { x.Status = "Done"; x.CompletedAt = DateTime.UtcNow; });
        _audit.Log("ProductionCompleted", "ManufacturingOrder", o.Reference, new { output, dto.ScrapQty, o.TotalCost });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { o.Id, o.Status, o.TotalCost, unitCost = output > 0 ? Math.Round(o.TotalCost / output, 4) : 0, o.OutputBatchNo });
    }

    /// <summary>Cancels a draft/in-progress order, or reverses a completed one (output must still be in stock).</summary>
    [HttpPost("orders/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var o = await _db.ManufacturingOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Order not found");
        if (o.Status == "Cancelled") return Ok(new { o.Id, o.Status });
        using var tx = await _db.Database.BeginTransactionAsync();
        if (o.Status == "Completed")
        {
            var at = new StockRef("Production", o.Reference + " reversed", o.Id, null, null, "ManufacturingOrder");
            if (o.OutputBatchId.HasValue && o.ActualOutputQty > 0)
            {
                var finished = await _db.Products.FirstAsync(p => p.Id == o.FinishedProductId);
                var batch = await _db.ProductBatches.FirstOrDefaultAsync(b => b.Id == o.OutputBatchId);
                if (batch == null || batch.Quantity < o.ActualOutputQty)
                    throw new BusinessRuleException($"Only {batch?.Quantity ?? 0} of the {o.ActualOutputQty} produced are still in stock; the order cannot be reversed.");
                await _inventory.DeductFromBatchAsync(finished, batch.Id, o.ActualOutputQty.Value, at);
            }
            var itemIds = o.Items.Select(i => i.Id).ToList();
            var rows = await _db.MoItemBatches.Where(b => itemIds.Contains(b.MoItemId)).ToListAsync();
            foreach (var line in o.Items)
            {
                var p = await _db.Products.FirstAsync(x => x.Id == line.RawMaterialId);
                var mine = rows.Where(r => r.MoItemId == line.Id).ToList();
                await _inventory.RestoreAsync(p, mine.Where(r => r.BatchId != null).Select(r => new BatchAllocation(r.BatchId!.Value, r.Quantity, r.UnitCost, "", null)),
                    mine.Where(r => r.BatchId == null).Sum(r => r.Quantity), at with { LocationId = o.LocationId });
            }
            _db.MoItemBatches.RemoveRange(rows);
        }
        o.Status = "Cancelled";
        o.UpdatedAt = DateTime.UtcNow;
        _audit.Log("Cancelled", "ManufacturingOrder", o.Reference, null);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { o.Id, o.Status });
    }

    [HttpDelete("orders/{id:int}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var o = await _db.ManufacturingOrders.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        if (o.Status is "Completed" or "InProgress") throw new BusinessRuleException("Cancel the order first.");
        _db.ManufacturingOrders.Remove(o);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Planned vs actual output and cost per order.</summary>
    [HttpGet("reports/yield")]
    public async Task<IActionResult> Yield([FromQuery] string? from, [FromQuery] string? to)
    {
        var start = DateTime.TryParse(from, out var f) ? f.Date : DateTime.UtcNow.Date.AddDays(-89);
        var end = (DateTime.TryParse(to, out var t) ? t.Date : DateTime.UtcNow.Date).AddDays(1);
        var orders = await _db.ManufacturingOrders.Include(o => o.Items).Where(o => o.Status == "Completed" && o.CompletionDate >= start && o.CompletionDate < end).ToListAsync();
        var boms = await _db.BillsOfMaterials.ToDictionaryAsync(b => b.Id);
        var rows = orders.Select(o =>
        {
            var b = boms.GetValueOrDefault(o.BomId);
            var planned = o.Items.Sum(i => i.RequiredQuantity * i.UnitCost);
            return new
            {
                o.Reference, recipe = b?.Name, completed = o.CompletionDate?.ToString("yyyy-MM-dd"), o.Quantity, actual = o.ActualOutputQty, o.ScrapQty,
                yieldPct = o.Quantity > 0 ? Math.Round((o.ActualOutputQty ?? 0) * 100m / o.Quantity, 1) : 0,
                expectedYieldPct = b?.ExpectedYieldPct ?? 100,
                wastageUnits = o.Items.Sum(i => i.WastageQty),
                materialCost = o.TotalMaterialCost, o.TotalCost,
                unitCost = o.ActualOutputQty > 0 ? Math.Round(o.TotalCost / o.ActualOutputQty.Value, 4) : 0
            };
        }).ToList();
        return Ok(new { from = start.ToString("yyyy-MM-dd"), to = end.AddDays(-1).ToString("yyyy-MM-dd"), rows });
    }
}
