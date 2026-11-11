using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public interface ILocationService
{
    Task<List<Location>> ListAsync(bool includeInactive = false);
    Task<Location> SaveAsync(int? id, LocationDto dto);
    Task DeleteAsync(int id);
    /// <summary>Finds a location by id, code or name (the old free-text store/warehouse values).</summary>
    Task<int?> ResolveAsync(int? id, string? codeOrName);
    /// <summary>The signed-in user's home location and whether they are limited to it.</summary>
    Task<(int? LocationId, bool Restricted)> UserLocationAsync();
    /// <summary>Location a document should use: the user's own when restricted, else the one asked for, else the user's home, else the default.</summary>
    Task<int> PickAsync(int? requestedId, string? requestedName = null);
}

public class LocationService : ILocationService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IHttpContextAccessor _http;

    public LocationService(AppDbContext db, IInventoryService inventory, IHttpContextAccessor http)
    {
        _db = db;
        _inventory = inventory;
        _http = http;
    }

    public async Task<List<Location>> ListAsync(bool includeInactive = false)
    {
        await _inventory.DefaultLocationIdAsync(); // makes sure "Main" exists
        return await _db.Locations.Where(l => includeInactive || l.IsActive)
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.Type).ThenBy(l => l.Name).ToListAsync();
    }

    public async Task<Location> SaveAsync(int? id, LocationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new BusinessRuleException("Enter a location name.");
        var code = (string.IsNullOrWhiteSpace(dto.Code) ? dto.Name : dto.Code).Trim().ToUpperInvariant();
        if (code.Length > 20) code = code[..20];
        if (await _db.Locations.AnyAsync(l => l.Code == code && l.Id != (id ?? 0)))
            throw new BusinessRuleException($"Another location already uses the code {code}.");

        var types = new[] { "HeadOffice", "Branch", "Warehouse", "Factory", "Kitchen" };
        var loc = id.HasValue ? await _db.Locations.FirstOrDefaultAsync(l => l.Id == id) ?? throw new KeyNotFoundException("Location not found")
                              : new Location();
        loc.Code = code;
        loc.Name = dto.Name.Trim();
        loc.Type = types.Contains(dto.Type) ? dto.Type : "Branch";
        loc.ParentId = dto.ParentId == loc.Id ? null : dto.ParentId;
        loc.Address = dto.Address;
        loc.Phone = dto.Phone;
        loc.DrugLicenceNo = dto.DrugLicenceNo;
        loc.SellsOnline = dto.SellsOnline;
        loc.IsActive = dto.IsActive;
        if (dto.IsDefault)
        {
            await _db.Locations.Where(l => l.IsDefault && l.Id != loc.Id).ForEachAsync(l => l.IsDefault = false);
            loc.IsDefault = true;
            loc.IsActive = true;
        }
        if (!id.HasValue) _db.Locations.Add(loc);
        await _db.SaveChangesAsync();
        return loc;
    }

    public async Task DeleteAsync(int id)
    {
        var loc = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id) ?? throw new KeyNotFoundException("Location not found");
        if (loc.IsDefault) throw new BusinessRuleException("The default location cannot be deleted. Make another location the default first.");
        if (await _db.ProductBatches.AnyAsync(b => b.LocationId == id && b.Quantity != 0))
            throw new BusinessRuleException($"{loc.Name} still holds stock. Transfer it out first, or mark the location inactive.");
        if (await _db.StockMovements.AnyAsync(m => m.LocationId == id) || await _db.Sales.AnyAsync(s => s.LocationId == id))
        {
            loc.IsActive = false; // history points at it; keep the row
        }
        else _db.Locations.Remove(loc);
        await _db.SaveChangesAsync();
    }

    public async Task<int?> ResolveAsync(int? id, string? codeOrName)
    {
        if (id.HasValue && await _db.Locations.AnyAsync(l => l.Id == id)) return id;
        if (string.IsNullOrWhiteSpace(codeOrName)) return null;
        var v = codeOrName.Trim();
        return await _db.Locations.Where(l => l.Code == v || l.Name == v).Select(l => (int?)l.Id).FirstOrDefaultAsync();
    }

    public async Task<(int? LocationId, bool Restricted)> UserLocationAsync()
    {
        var idText = _http.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idText, out var partyId)) return (null, false);
        var p = await _db.Parties.Where(x => x.Id == partyId).Select(x => new { x.LocationId, x.RestrictToLocation }).FirstOrDefaultAsync();
        return p == null ? (null, false) : (p.LocationId, p.RestrictToLocation && p.LocationId != null);
    }

    public async Task<int> PickAsync(int? requestedId, string? requestedName = null)
    {
        var (home, restricted) = await UserLocationAsync();
        var asked = await ResolveAsync(requestedId, requestedName);
        if (restricted)
        {
            if (asked.HasValue && asked != home)
                throw new BusinessRuleException("You can only work at your own branch.");
            return home!.Value;
        }
        return asked ?? home ?? await _inventory.DefaultLocationIdAsync();
    }
}

// ════════════════════════════════════════════════════════════════════
// Two-step stock transfers
// ════════════════════════════════════════════════════════════════════

public class StockTransferService : IStockTransferService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ILocationService _locations;
    private readonly IAuditService _audit;

    public StockTransferService(AppDbContext db, IInventoryService inventory, ILocationService locations, IAuditService audit)
    {
        _db = db;
        _inventory = inventory;
        _locations = locations;
        _audit = audit;
    }

    public async Task<object> GetAllAsync()
    {
        var transfers = await _db.StockTransfers.Include(t => t.Items).OrderByDescending(t => t.CreatedAt).ToListAsync();
        return await MapAsync(transfers);
    }

    public async Task<StockTransferDto?> GetAsync(int id)
    {
        var t = await _db.StockTransfers.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        return t == null ? null : (await MapAsync(new List<StockTransfer> { t })).First();
    }

    private async Task<List<StockTransferDto>> MapAsync(List<StockTransfer> transfers)
    {
        var productIds = transfers.SelectMany(t => t.Items.Select(i => i.ProductId)).Distinct().ToList();
        var productMap = await _db.Products.Include(p => p.Images).Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        return transfers.Select(t => new StockTransferDto
        {
            Id = t.Id, WarehouseFrom = t.WarehouseFrom, WarehouseTo = t.WarehouseTo,
            ReferenceNumber = t.ReferenceNumber, Notes = t.Notes,
            NoOfProducts = t.Items.Select(i => i.ProductId).Distinct().Count(), QuantityTransferred = t.Items.Sum(i => i.Quantity),
            Date = t.Date.ToString("dd MMM yyyy"),
            FromLocationId = t.FromLocationId, ToLocationId = t.ToLocationId, Status = t.Status,
            DispatchedBy = t.DispatchedBy, DispatchedAt = t.DispatchedAt?.ToString("dd MMM yyyy HH:mm"),
            ReceivedBy = t.ReceivedBy, ReceivedAt = t.ReceivedAt?.ToString("dd MMM yyyy HH:mm"),
            StockRequestId = t.StockRequestId,
            Items = t.Items.Select(i =>
            {
                productMap.TryGetValue(i.ProductId, out var prod);
                return new StockTransferItemDto
                {
                    Id = i.Id, ProductId = i.ProductId, ProductName = prod?.ProductName ?? "",
                    ProductImage = prod?.Images.FirstOrDefault()?.ImagePath ?? "",
                    Sku = prod?.SKU ?? "", Category = prod?.Category ?? "", Quantity = i.Quantity,
                    VariantId = i.VariantId, BatchId = i.BatchId, BatchNo = i.BatchNo,
                    ExpiryDate = i.ExpiryDate?.ToString("yyyy-MM-dd"), ReceivedQty = i.ReceivedQty, UnitCost = i.UnitCost
                };
            }).ToList()
        }).ToList();
    }

    private async Task<string> NextReferenceAsync()
    {
        var n = await _db.StockTransfers.CountAsync() + 1;
        string r;
        do { r = $"TR{n++:D4}"; } while (await _db.StockTransfers.AnyAsync(t => t.ReferenceNumber == r));
        return r;
    }

    public async Task<object> CreateAsync(CreateStockTransferDto dto)
    {
        var from = await _locations.ResolveAsync(dto.FromLocationId, dto.WarehouseFrom)
                   ?? throw new BusinessRuleException("Choose the location the stock leaves from.");
        var to = await _locations.ResolveAsync(dto.ToLocationId, dto.WarehouseTo)
                 ?? throw new BusinessRuleException("Choose the location the stock goes to.");
        if (from == to) throw new BusinessRuleException("The two locations must be different.");
        if (dto.Items.Count == 0 || dto.Items.Any(i => i.Quantity <= 0)) throw new BusinessRuleException("Add at least one product with a quantity above zero.");

        var names = await _db.Locations.Where(l => l.Id == from || l.Id == to).ToDictionaryAsync(l => l.Id, l => l.Name);
        using var tx = await _db.Database.BeginTransactionAsync();
        var entity = new StockTransfer
        {
            WarehouseFrom = names[from], WarehouseTo = names[to], FromLocationId = from, ToLocationId = to,
            ReferenceNumber = string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? await NextReferenceAsync() : dto.ReferenceNumber.Trim(),
            Notes = dto.Notes, Date = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, Status = "Draft",
            Items = dto.Items.Select(i => new StockTransferItem { ProductId = i.ProductId, Quantity = i.Quantity, VariantId = i.VariantId, BatchId = i.BatchId }).ToList()
        };
        _db.StockTransfers.Add(entity);
        await _db.SaveChangesAsync();

        var mode = (dto.Mode ?? "").Trim().ToLowerInvariant();
        if (mode != "draft") await DispatchInternalAsync(entity);
        if (mode == "") await ReceiveInternalAsync(entity, null);
        _audit.Log("Created", "StockTransfer", entity.ReferenceNumber, new { from = entity.WarehouseFrom, to = entity.WarehouseTo, entity.Status });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return new { entity.Id, entity.ReferenceNumber, entity.Status };
    }

    public async Task<object?> UpdateAsync(int id, CreateStockTransferDto dto)
    {
        var entity = await _db.StockTransfers.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null) return null;
        if (entity.Status != "Draft")
            throw new BusinessRuleException($"Transfer {entity.ReferenceNumber} is {entity.Status.ToLower()}; only drafts can be edited. Cancel it and create a new one.");

        var from = await _locations.ResolveAsync(dto.FromLocationId, dto.WarehouseFrom) ?? entity.FromLocationId;
        var to = await _locations.ResolveAsync(dto.ToLocationId, dto.WarehouseTo) ?? entity.ToLocationId;
        if (from == to) throw new BusinessRuleException("The two locations must be different.");
        var names = await _db.Locations.Where(l => l.Id == from || l.Id == to).ToDictionaryAsync(l => l.Id, l => l.Name);
        entity.FromLocationId = from; entity.ToLocationId = to;
        if (from.HasValue) entity.WarehouseFrom = names[from.Value];
        if (to.HasValue) entity.WarehouseTo = names[to.Value];
        entity.Notes = dto.Notes;
        if (!string.IsNullOrWhiteSpace(dto.ReferenceNumber)) entity.ReferenceNumber = dto.ReferenceNumber;
        _db.StockTransferItems.RemoveRange(entity.Items);
        entity.Items = dto.Items.Select(i => new StockTransferItem { StockTransferId = id, ProductId = i.ProductId, Quantity = i.Quantity, VariantId = i.VariantId, BatchId = i.BatchId }).ToList();
        await _db.SaveChangesAsync();
        return new { entity.Id };
    }

    public async Task<object> DispatchAsync(int id)
    {
        var entity = await _db.StockTransfers.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id) ?? throw new KeyNotFoundException("Transfer not found");
        if (entity.Status != "Draft") throw new BusinessRuleException($"Transfer {entity.ReferenceNumber} has already been {entity.Status.ToLower()}.");
        using var tx = await _db.Database.BeginTransactionAsync();
        await DispatchInternalAsync(entity);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return new { entity.Id, entity.Status };
    }

    public async Task<object> ReceiveAsync(int id, ReceiveStockTransferDto? dto)
    {
        var entity = await _db.StockTransfers.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id) ?? throw new KeyNotFoundException("Transfer not found");
        if (entity.Status != "Dispatched") throw new BusinessRuleException($"Transfer {entity.ReferenceNumber} is {entity.Status.ToLower()}, not in transit.");
        var (home, restricted) = await _locations.UserLocationAsync();
        if (restricted && home != entity.ToLocationId) throw new BusinessRuleException("Only the receiving branch can confirm this transfer.");
        using var tx = await _db.Database.BeginTransactionAsync();
        await ReceiveInternalAsync(entity, dto);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        var shortBy = entity.Items.Sum(i => i.Quantity - (i.ReceivedQty ?? i.Quantity));
        return new { entity.Id, entity.Status, shortBy };
    }

    public async Task<object> CancelAsync(int id)
    {
        var entity = await _db.StockTransfers.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id) ?? throw new KeyNotFoundException("Transfer not found");
        if (entity.Status is "Received" or "Cancelled") throw new BusinessRuleException($"Transfer {entity.ReferenceNumber} is already {entity.Status.ToLower()}.");
        using var tx = await _db.Database.BeginTransactionAsync();
        if (entity.Status == "Dispatched")
        {
            // Stock goes back into the sender's batches.
            var at = new StockRef("TransferIn", entity.ReferenceNumber + " cancelled", entity.Id, entity.FromLocationId, null, "StockTransfer");
            foreach (var g in entity.Items.GroupBy(i => i.ProductId))
            {
                var product = await _db.Products.FirstAsync(p => p.Id == g.Key);
                foreach (var i in g)
                {
                    if (i.BatchId.HasValue)
                        await _inventory.RestoreAsync(product, new[] { new BatchAllocation(i.BatchId.Value, i.Quantity, i.UnitCost, i.BatchNo ?? "", i.ExpiryDate) }, 0, at with { VariantId = i.VariantId });
                    else
                        await _inventory.RestoreAsync(product, Array.Empty<BatchAllocation>(), i.Quantity, at with { VariantId = i.VariantId });
                }
            }
        }
        entity.Status = "Cancelled";
        _audit.Log("Cancelled", "StockTransfer", entity.ReferenceNumber, null);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return new { entity.Id, entity.Status };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.StockTransfers.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null) return false;
        if (entity.Status is "Dispatched" or "Received")
            throw new BusinessRuleException($"Transfer {entity.ReferenceNumber} has moved stock. Cancel it instead (in-transit only); received transfers stay on record.");
        _db.StockTransfers.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Takes stock out of the sender, splitting each line by the batches it came from.</summary>
    private async Task DispatchInternalAsync(StockTransfer entity)
    {
        var at = new StockRef("TransferOut", entity.ReferenceNumber, entity.Id, entity.FromLocationId, null, "StockTransfer");
        var lines = entity.Items.ToList();
        var split = new List<StockTransferItem>();
        foreach (var line in lines)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId)
                          ?? throw new BusinessRuleException("A product on this transfer no longer exists.");
            var here = at with { VariantId = line.VariantId };
            List<BatchAllocation> taken;
            if (line.BatchId.HasValue)
            {
                var batch = await _db.ProductBatches.FirstOrDefaultAsync(b => b.Id == line.BatchId && b.LocationId == entity.FromLocationId);
                if (batch == null || batch.Quantity < line.Quantity)
                    throw new BusinessRuleException($"{product.ProductName}: batch {batch?.BatchNo ?? "?"} has only {batch?.Quantity ?? 0} at {entity.WarehouseFrom}.");
                taken = new List<BatchAllocation> { (await _inventory.DeductFromBatchAsync(product, batch.Id, line.Quantity, here))! };
            }
            else
            {
                var available = await _inventory.AvailableAsync(product, entity.FromLocationId, line.VariantId, sellableOnly: true);
                if (available < line.Quantity)
                    throw new BusinessRuleException($"{product.ProductName}: only {available} unexpired at {entity.WarehouseFrom}, {line.Quantity} to send.");
                taken = await _inventory.DeductFefoAsync(product, line.Quantity, here);
            }

            // One transfer line per batch, so the receiver gets the same batch numbers and expiries.
            var first = true;
            foreach (var a in taken)
            {
                var target = first ? line : new StockTransferItem { StockTransferId = entity.Id, ProductId = line.ProductId, VariantId = line.VariantId };
                target.Quantity = a.Quantity; target.BatchId = a.BatchId; target.BatchNo = a.BatchNo;
                target.ExpiryDate = a.ExpiryDate; target.UnitCost = a.UnitCost;
                if (!first) split.Add(target);
                first = false;
            }
        }
        foreach (var s in split) entity.Items.Add(s);
        entity.Status = "Dispatched";
        entity.DispatchedAt = DateTime.UtcNow;
        entity.DispatchedBy = _audit.CurrentUser;
    }

    /// <summary>Puts stock into the receiver with the sender's batch numbers, expiries and costs.</summary>
    private async Task ReceiveInternalAsync(StockTransfer entity, ReceiveStockTransferDto? dto)
    {
        var at = new StockRef("TransferIn", entity.ReferenceNumber, entity.Id, entity.ToLocationId, null, "StockTransfer");
        foreach (var line in entity.Items)
        {
            var asked = dto?.Lines.FirstOrDefault(l => l.ItemId == line.Id);
            var qty = asked == null ? line.Quantity : Math.Clamp(asked.ReceivedQty, 0, line.Quantity);
            line.ReceivedQty = qty;
            if (qty == 0) continue;
            var product = await _db.Products.FirstAsync(p => p.Id == line.ProductId);
            var source = line.BatchId.HasValue ? await _db.ProductBatches.FirstOrDefaultAsync(b => b.Id == line.BatchId) : null;
            var batch = await _inventory.AddStockAsync(product, qty, line.BatchNo, line.ExpiryDate, line.UnitCost,
                supplier: source?.Supplier, manufacturedDate: source?.ManufacturedDate, at: at with { VariantId = line.VariantId });
            line.ReceivedBatch = batch;
        }
        var shortBy = entity.Items.Sum(i => i.Quantity - (i.ReceivedQty ?? 0));
        if (shortBy > 0)
        {
            entity.Notes = string.IsNullOrWhiteSpace(entity.Notes) ? $"Received {shortBy} short." : $"{entity.Notes} | Received {shortBy} short.";
            _audit.Log("ReceivedShort", "StockTransfer", entity.ReferenceNumber, new { shortBy, dto?.Notes });
        }
        entity.Status = "Received";
        entity.ReceivedAt = DateTime.UtcNow;
        entity.ReceivedBy = _audit.CurrentUser;
        if (entity.StockRequestId.HasValue)
        {
            var req = await _db.StockRequests.FirstOrDefaultAsync(r => r.Id == entity.StockRequestId);
            if (req != null) req.Status = "Fulfilled";
        }
    }
}

// ════════════════════════════════════════════════════════════════════
// Branch requisitions
// ════════════════════════════════════════════════════════════════════

public interface IStockRequestService
{
    Task<object> ListAsync(string? status, int? locationId);
    Task<object> CreateAsync(StockRequestDto dto);
    Task<object> SubmitAsync(int id);
    Task<object> ApproveAsync(int id, ApproveStockRequestDto dto);
    Task<object> RejectAsync(int id, string? reason);
    Task DeleteAsync(int id);
    /// <summary>Creates draft requests for every branch item below its minimum.</summary>
    Task<object> GenerateReorderAsync();
}

public class StockRequestService : IStockRequestService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ILocationService _locations;
    private readonly IStockTransferService _transfers;
    private readonly IAuditService _audit;

    public StockRequestService(AppDbContext db, IInventoryService inventory, ILocationService locations, IStockTransferService transfers, IAuditService audit)
    {
        _db = db;
        _inventory = inventory;
        _locations = locations;
        _transfers = transfers;
        _audit = audit;
    }

    public async Task<object> ListAsync(string? status, int? locationId)
    {
        var q = _db.StockRequests.Include(r => r.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        if (locationId.HasValue) q = q.Where(r => r.ToLocationId == locationId || r.FromLocationId == locationId);
        var list = await q.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync();
        var locs = await _db.Locations.ToDictionaryAsync(l => l.Id, l => l.Name);
        var pids = list.SelectMany(r => r.Items.Select(i => i.ProductId)).Distinct().ToList();
        var prods = await _db.Products.Where(p => pids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProductName);
        return list.Select(r => new
        {
            r.Id, r.Reference, r.Status, r.Source, r.Notes, r.RequestedBy, r.ApprovedBy, r.TransferId,
            r.FromLocationId, fromLocation = locs.GetValueOrDefault(r.FromLocationId),
            r.ToLocationId, toLocation = locs.GetValueOrDefault(r.ToLocationId),
            createdAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            items = r.Items.Select(i => new { i.Id, i.ProductId, productName = prods.GetValueOrDefault(i.ProductId), i.VariantId, i.RequestedQty, i.ApprovedQty })
        });
    }

    private async Task<string> NextReferenceAsync()
    {
        var n = await _db.StockRequests.CountAsync() + 1;
        string r;
        do { r = $"RQ{n++:D4}"; } while (await _db.StockRequests.AnyAsync(x => x.Reference == r));
        return r;
    }

    public async Task<object> CreateAsync(StockRequestDto dto)
    {
        var to = await _locations.PickAsync(dto.ToLocationId);
        var toLoc = await _db.Locations.FirstAsync(l => l.Id == to);
        var from = dto.FromLocationId ?? toLoc.ParentId
                   ?? await _db.Locations.Where(l => l.Type == "Warehouse" && l.IsActive && l.Id != to).Select(l => (int?)l.Id).FirstOrDefaultAsync()
                   ?? throw new BusinessRuleException("Choose the warehouse to request from.");
        if (from == to) throw new BusinessRuleException("A location cannot request stock from itself.");
        if (dto.Items.Count == 0 || dto.Items.Any(i => i.RequestedQty <= 0)) throw new BusinessRuleException("Add at least one product with a quantity.");

        var req = new StockRequest
        {
            Reference = await NextReferenceAsync(), FromLocationId = from, ToLocationId = to,
            Status = dto.Submit ? "Submitted" : "Draft", Source = "Manual", Notes = dto.Notes, RequestedBy = _audit.CurrentUser,
            Items = dto.Items.Select(i => new StockRequestItem { ProductId = i.ProductId, VariantId = i.VariantId, RequestedQty = i.RequestedQty }).ToList()
        };
        _db.StockRequests.Add(req);
        await _db.SaveChangesAsync();
        return new { req.Id, req.Reference, req.Status };
    }

    public async Task<object> SubmitAsync(int id)
    {
        var req = await _db.StockRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new KeyNotFoundException("Request not found");
        if (req.Status != "Draft") throw new BusinessRuleException($"Request {req.Reference} is already {req.Status.ToLower()}.");
        req.Status = "Submitted";
        await _db.SaveChangesAsync();
        return new { req.Id, req.Status };
    }

    public async Task<object> ApproveAsync(int id, ApproveStockRequestDto dto)
    {
        var req = await _db.StockRequests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id) ?? throw new KeyNotFoundException("Request not found");
        if (req.Status is not ("Submitted" or "Draft")) throw new BusinessRuleException($"Request {req.Reference} is {req.Status.ToLower()}.");
        foreach (var i in req.Items)
        {
            var line = dto.Lines.FirstOrDefault(l => l.ItemId == i.Id);
            i.ApprovedQty = Math.Max(0, line?.ApprovedQty ?? i.RequestedQty);
        }
        var lines = req.Items.Where(i => i.ApprovedQty > 0).ToList();
        if (lines.Count == 0) throw new BusinessRuleException("Approve a quantity for at least one product, or reject the request.");

        // The warehouse picks and dispatches straight away; the branch confirms receipt.
        var created = await _transfers.CreateAsync(new CreateStockTransferDto
        {
            FromLocationId = req.FromLocationId, ToLocationId = req.ToLocationId, Mode = dto.Dispatch ? "dispatch" : "draft",
            Notes = $"For request {req.Reference}",
            Items = lines.Select(i => new CreateStockTransferItemDto { ProductId = i.ProductId, VariantId = i.VariantId, Quantity = i.ApprovedQty!.Value }).ToList()
        });
        var transferId = (int)created.GetType().GetProperty("Id")!.GetValue(created)!;
        var transfer = await _db.StockTransfers.FirstAsync(t => t.Id == transferId);
        transfer.StockRequestId = req.Id;
        req.TransferId = transferId;
        req.Status = "Approved";
        req.ApprovedBy = _audit.CurrentUser;
        await _db.SaveChangesAsync();
        return new { req.Id, req.Status, transferId, transfer.ReferenceNumber };
    }

    public async Task<object> RejectAsync(int id, string? reason)
    {
        var req = await _db.StockRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new KeyNotFoundException("Request not found");
        if (req.Status is "Approved" or "Fulfilled") throw new BusinessRuleException($"Request {req.Reference} is already {req.Status.ToLower()}.");
        req.Status = "Rejected";
        req.ApprovedBy = _audit.CurrentUser;
        if (!string.IsNullOrWhiteSpace(reason)) req.Notes = string.IsNullOrWhiteSpace(req.Notes) ? $"Rejected: {reason}" : $"{req.Notes} | Rejected: {reason}";
        await _db.SaveChangesAsync();
        return new { req.Id, req.Status };
    }

    public async Task DeleteAsync(int id)
    {
        var req = await _db.StockRequests.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id) ?? throw new KeyNotFoundException("Request not found");
        if (req.Status is "Approved" or "Fulfilled") throw new BusinessRuleException($"Request {req.Reference} has a transfer; it stays on record.");
        _db.StockRequests.Remove(req);
        await _db.SaveChangesAsync();
    }

    public async Task<object> GenerateReorderAsync()
    {
        var levels = await _db.LocationStockLevels.Where(l => l.MinQty > 0).ToListAsync();
        var locations = await _db.Locations.Where(l => l.IsActive).ToListAsync();
        var created = new List<string>();
        foreach (var byLoc in levels.GroupBy(l => l.LocationId))
        {
            var loc = locations.FirstOrDefault(l => l.Id == byLoc.Key);
            if (loc == null) continue;
            var supplier = loc.ParentId ?? locations.FirstOrDefault(l => l.Type == "Warehouse" && l.Id != loc.Id)?.Id;
            if (supplier == null) continue;
            // Skip products that already have an open request for this branch.
            var open = await _db.StockRequestItems.Where(i => i.StockRequest!.ToLocationId == loc.Id && (i.StockRequest.Status == "Draft" || i.StockRequest.Status == "Submitted"))
                .Select(i => i.ProductId).ToListAsync();

            var items = new List<StockRequestItem>();
            foreach (var level in byLoc.Where(l => !open.Contains(l.ProductId)))
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == level.ProductId);
                if (product == null) continue;
                var onHand = await _inventory.AvailableAsync(product, loc.Id, null, sellableOnly: true, anyVariant: true);
                if (onHand >= level.MinQty) continue;
                var target = Math.Max(level.MaxQty, level.MinQty);
                items.Add(new StockRequestItem { ProductId = product.Id, RequestedQty = target - onHand });
            }
            if (items.Count == 0) continue;
            var req = new StockRequest
            {
                Reference = await NextReferenceAsync(), FromLocationId = supplier.Value, ToLocationId = loc.Id,
                Status = "Submitted", Source = "AutoReorder", RequestedBy = "System", Items = items
            };
            _db.StockRequests.Add(req);
            await _db.SaveChangesAsync();
            created.Add(req.Reference);
        }
        return new { created = created.Count, references = created };
    }
}
