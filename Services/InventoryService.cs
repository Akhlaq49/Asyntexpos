using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>One slice of stock taken from a batch.</summary>
public record BatchAllocation(int BatchId, int Quantity, decimal UnitCost, string BatchNo, DateTime? ExpiryDate);

/// <summary>
/// Where a stock change happens and why. LocationId/VariantId narrow which batches are used;
/// Type and Reference are written to the movement ledger.
/// </summary>
public record StockRef(string Type, string? Reference = null, int? SourceId = null, int? LocationId = null, int? VariantId = null, string? SourceType = null);

/// <summary>
/// Single place that changes stock. Batches (ProductBatch) are the balance per product, variant,
/// location and expiry; Product.Quantity is kept equal to the total of all of them (plus legacy stock
/// that has no batch). Every change also writes a StockMovement row. Callers save changes themselves.
/// All quantities are in base units.
/// </summary>
public interface IInventoryService
{
    Task<ProductBatch> AddStockAsync(Product product, int quantity, string? batchNo, DateTime? expiryDate,
        decimal unitCost, string? supplier = null, int? purchaseId = null, int? purchaseItemId = null,
        DateTime? manufacturedDate = null, string? store = null, StockRef? at = null);

    /// <summary>First-expiry-first-out, skipping expired batches. With a location only that location's batches are used.</summary>
    Task<List<BatchAllocation>> DeductFefoAsync(Product product, int quantity, StockRef? at = null, bool spillToOtherLocations = false);

    Task<BatchAllocation?> DeductFromBatchAsync(Product product, int batchId, int quantity, StockRef? at = null);

    Task RestoreAsync(Product product, IEnumerable<BatchAllocation> allocations, int legacyQuantity = 0, StockRef? at = null);

    Task RemovePurchaseItemStockAsync(Product product, int purchaseItemId, int quantity, StockRef? at = null);

    Task SetTotalAsync(Product product, int newTotal, string? batchNo = null, DateTime? expiryDate = null, StockRef? at = null);

    decimal AverageCost(IEnumerable<BatchAllocation> allocations, decimal fallback);

    /// <summary>Stock on hand. sellableOnly leaves out expired batches. Legacy stock (no batch) counts only when no location is given.</summary>
    Task<int> AvailableAsync(Product product, int? locationId = null, int? variantId = null, bool sellableOnly = true, bool anyVariant = false);

    /// <summary>The tenant's default location, created as "Main" on first use.</summary>
    Task<int> DefaultLocationIdAsync();
}

public class InventoryService : IInventoryService
{
    public const string OpeningBatchNo = "OPENING";
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private int? _defaultLocation;
    private List<ChannelConnection>? _channels;

    public InventoryService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private string CurrentUser => _http.HttpContext?.User?.Identity?.Name ?? "System";

    private async Task<List<ProductBatch>> BatchesOf(int productId)
    {
        // Load every batch (tracked) so in-memory changes made earlier in this request are respected.
        var fromDb = await _db.ProductBatches.Where(b => b.ProductId == productId).ToListAsync();
        var added = _db.ChangeTracker.Entries<ProductBatch>()
            .Where(e => e.State == EntityState.Added && e.Entity.ProductId == productId)
            .Select(e => e.Entity);
        return fromDb.Concat(added).Distinct().ToList();
    }

    public async Task<int> DefaultLocationIdAsync()
    {
        if (_defaultLocation.HasValue) return _defaultLocation.Value;
        var loc = await _db.Locations.Where(l => l.IsActive).OrderByDescending(l => l.IsDefault).ThenBy(l => l.Id).FirstOrDefaultAsync()
                  ?? _db.ChangeTracker.Entries<Location>().Select(e => e.Entity).FirstOrDefault();
        if (loc == null)
        {
            loc = new Location { Code = "MAIN", Name = "Main Branch", Type = "Branch", IsDefault = true, SellsOnline = true };
            _db.Locations.Add(loc);
            await _db.SaveChangesAsync();
        }
        _defaultLocation = loc.Id;
        return loc.Id;
    }

    private void Log(Product product, ProductBatch? batch, int qty, decimal unitCost, StockRef? at, int? locationId, int? variantId)
    {
        if (qty == 0) return;
        _db.StockMovements.Add(new StockMovement
        {
            TenantId = product.TenantId, ProductId = product.Id, VariantId = batch?.VariantId ?? variantId,
            LocationId = batch?.LocationId ?? locationId, Batch = batch, BatchId = batch?.Id > 0 ? batch.Id : null,
            Quantity = qty, UnitCost = unitCost,
            MovementType = at?.Type ?? "Other", SourceType = at?.SourceType ?? at?.Type, SourceId = at?.SourceId,
            Reference = at?.Reference, CreatedBy = CurrentUser, CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Queues a stock push for every channel that publishes this location.</summary>
    private async Task QueueChannelSync(Product product, int? locationId)
    {
        _channels ??= await _db.ChannelConnections.Where(c => c.Status == "Active" && c.SyncStock).ToListAsync();
        foreach (var c in _channels.Where(c => locationId == null || c.LocationId == locationId))
        {
            var queued = _db.ChangeTracker.Entries<SyncOutbox>().Any(e => e.State == EntityState.Added
                         && e.Entity.ConnectionId == c.Id && e.Entity.EntityId == product.Id && e.Entity.Kind == "StockLevel")
                      || await _db.SyncOutbox.AnyAsync(o => o.ConnectionId == c.Id && o.EntityId == product.Id && o.Kind == "StockLevel" && o.Status == "Pending");
            if (!queued)
                _db.SyncOutbox.Add(new SyncOutbox { TenantId = product.TenantId, ConnectionId = c.Id, Kind = "StockLevel", EntityId = product.Id });
        }
    }

    private static bool Matches(ProductBatch b, StockRef? at)
        => at == null || ((at.LocationId == null || b.LocationId == at.LocationId) && b.VariantId == at.VariantId);

    public async Task<ProductBatch> AddStockAsync(Product product, int quantity, string? batchNo, DateTime? expiryDate,
        decimal unitCost, string? supplier = null, int? purchaseId = null, int? purchaseItemId = null,
        DateTime? manufacturedDate = null, string? store = null, StockRef? at = null)
    {
        var no = string.IsNullOrWhiteSpace(batchNo) ? OpeningBatchNo : batchNo.Trim();
        var expiry = expiryDate?.Date;
        var locationId = at?.LocationId ?? await DefaultLocationIdAsync();
        var variantId = at?.VariantId;
        ProductBatch? batch = null;

        // A purchase line always gets its own batch so edits can undo it exactly.
        if (purchaseItemId == null)
        {
            batch = (await BatchesOf(product.Id)).FirstOrDefault(b => b.PurchaseItemId == null && b.BatchNo == no
                && b.ExpiryDate?.Date == expiry && b.LocationId == locationId && b.VariantId == variantId);
        }

        if (batch == null)
        {
            batch = new ProductBatch
            {
                ProductId = product.Id, TenantId = product.TenantId, BatchNo = no, ExpiryDate = expiry,
                ManufacturedDate = manufacturedDate, Quantity = 0, PurchasePrice = unitCost,
                Supplier = supplier, PurchaseId = purchaseId, PurchaseItemId = purchaseItemId,
                Store = store ?? product.Store, LocationId = locationId, VariantId = variantId
            };
            _db.ProductBatches.Add(batch);
        }
        else if (unitCost > 0 && batch.Quantity + quantity > 0)
        {
            batch.PurchasePrice = (batch.PurchasePrice * batch.Quantity + unitCost * quantity) / (batch.Quantity + quantity);
        }

        batch.Quantity += quantity;
        batch.UpdatedAt = DateTime.UtcNow;
        product.Quantity += quantity;
        if (unitCost > 0) product.PurchasePrice = unitCost;
        product.UpdatedAt = DateTime.UtcNow;
        Log(product, batch, quantity, unitCost > 0 ? unitCost : batch.PurchasePrice, at, locationId, variantId);
        await QueueChannelSync(product, locationId);
        SyncLegacyExpiry(product, await BatchesOf(product.Id));
        return batch;
    }

    public async Task<List<BatchAllocation>> DeductFefoAsync(Product product, int quantity, StockRef? at = null, bool spillToOtherLocations = false)
    {
        var result = new List<BatchAllocation>();
        if (quantity <= 0) return result;

        var today = DateTime.UtcNow.Date;
        var batches = (await BatchesOf(product.Id))
            .Where(b => b.Quantity > 0 && (b.ExpiryDate == null || b.ExpiryDate.Value.Date >= today)
                        && (spillToOtherLocations ? (at == null || b.VariantId == at.VariantId) : Matches(b, at)))
            // Own location first; other locations only when spilling.
            .OrderBy(b => at?.LocationId == null || b.LocationId == at.LocationId ? 0 : 1)
            .ThenBy(b => b.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(b => b.CreatedAt)
            .ToList();

        var remaining = quantity;
        foreach (var b in batches)
        {
            if (remaining <= 0) break;
            var take = Math.Min(b.Quantity, remaining);
            b.Quantity -= take;
            b.UpdatedAt = DateTime.UtcNow;
            remaining -= take;
            result.Add(new BatchAllocation(b.Id, take, b.PurchasePrice, b.BatchNo, b.ExpiryDate));
            Log(product, b, -take, b.PurchasePrice, at, b.LocationId, b.VariantId);
        }

        // Whatever is left comes from stock that has no batch record (older data).
        if (remaining > 0) Log(product, null, -remaining, product.PurchasePrice, at, at?.LocationId, at?.VariantId);
        product.Quantity -= quantity;
        product.UpdatedAt = DateTime.UtcNow;
        await QueueChannelSync(product, at?.LocationId);
        SyncLegacyExpiry(product, await BatchesOf(product.Id));
        return result;
    }

    public async Task<BatchAllocation?> DeductFromBatchAsync(Product product, int batchId, int quantity, StockRef? at = null)
    {
        var batch = (await BatchesOf(product.Id)).FirstOrDefault(b => b.Id == batchId);
        if (batch == null || quantity <= 0) return null;
        var take = Math.Min(batch.Quantity, quantity);
        batch.Quantity -= take;
        batch.UpdatedAt = DateTime.UtcNow;
        product.Quantity -= take;
        product.UpdatedAt = DateTime.UtcNow;
        Log(product, batch, -take, batch.PurchasePrice, at, batch.LocationId, batch.VariantId);
        await QueueChannelSync(product, batch.LocationId);
        SyncLegacyExpiry(product, await BatchesOf(product.Id));
        return new BatchAllocation(batch.Id, take, batch.PurchasePrice, batch.BatchNo, batch.ExpiryDate);
    }

    public async Task RestoreAsync(Product product, IEnumerable<BatchAllocation> allocations, int legacyQuantity = 0, StockRef? at = null)
    {
        var batches = await BatchesOf(product.Id);
        foreach (var a in allocations)
        {
            var b = batches.FirstOrDefault(x => x.Id == a.BatchId);
            if (b != null) { b.Quantity += a.Quantity; b.UpdatedAt = DateTime.UtcNow; }
            product.Quantity += a.Quantity;
            Log(product, b, a.Quantity, a.UnitCost, at, b?.LocationId ?? at?.LocationId, b?.VariantId ?? at?.VariantId);
            await QueueChannelSync(product, b?.LocationId);
        }
        if (legacyQuantity != 0) Log(product, null, legacyQuantity, product.PurchasePrice, at, at?.LocationId, at?.VariantId);
        product.Quantity += legacyQuantity;
        product.UpdatedAt = DateTime.UtcNow;
        SyncLegacyExpiry(product, batches);
    }

    public async Task RemovePurchaseItemStockAsync(Product product, int purchaseItemId, int quantity, StockRef? at = null)
    {
        var batches = await BatchesOf(product.Id);
        var batch = batches.FirstOrDefault(b => b.PurchaseItemId == purchaseItemId);
        if (batch != null)
        {
            // Stock already sold from this batch cannot be taken back from it; the total still drops.
            batch.Quantity = Math.Max(0, batch.Quantity - quantity);
            batch.UpdatedAt = DateTime.UtcNow;
            // The batch row is kept (at zero) so the movement history keeps its link.
            batch.PurchaseItemId = null;
        }
        Log(product, batch, -quantity, batch?.PurchasePrice ?? product.PurchasePrice, at, batch?.LocationId, batch?.VariantId);
        product.Quantity = Math.Max(0, product.Quantity - quantity);
        product.UpdatedAt = DateTime.UtcNow;
        await QueueChannelSync(product, batch?.LocationId);
        SyncLegacyExpiry(product, batches);
    }

    public async Task SetTotalAsync(Product product, int newTotal, string? batchNo = null, DateTime? expiryDate = null, StockRef? at = null)
    {
        var diff = newTotal - product.Quantity;
        at ??= new StockRef("Adjustment", "Product edit");
        if (diff > 0)
            await AddStockAsync(product, diff, batchNo, expiryDate, product.PurchasePrice, at: at);
        else if (diff < 0)
            await DeductFefoAsync(product, -diff, at with { LocationId = null });
    }

    public decimal AverageCost(IEnumerable<BatchAllocation> allocations, decimal fallback)
    {
        var list = allocations.Where(a => a.Quantity > 0).ToList();
        var qty = list.Sum(a => a.Quantity);
        if (qty == 0) return fallback;
        var cost = list.Sum(a => a.UnitCost * a.Quantity) / qty;
        return cost > 0 ? Math.Round(cost, 4) : fallback;
    }

    public async Task<int> AvailableAsync(Product product, int? locationId = null, int? variantId = null, bool sellableOnly = true, bool anyVariant = false)
    {
        var today = DateTime.UtcNow.Date;
        var all = await BatchesOf(product.Id);
        var batches = all.Where(b => (locationId == null || b.LocationId == locationId) && (anyVariant || b.VariantId == variantId)).ToList();
        var good = batches.Where(b => b.Quantity > 0 && (!sellableOnly || b.ExpiryDate == null || b.ExpiryDate.Value.Date >= today)).Sum(b => b.Quantity);
        if (locationId == null && variantId == null)
            good += Math.Max(0, product.Quantity - all.Sum(b => b.Quantity));
        return good;
    }

    /// <summary>Keeps the old single ExpiryDate column showing the nearest expiry, for screens that still read it.</summary>
    private static void SyncLegacyExpiry(Product product, List<ProductBatch> batches)
    {
        var next = batches.Where(b => b.Quantity > 0 && b.ExpiryDate != null)
            .OrderBy(b => b.ExpiryDate).FirstOrDefault();
        if (next != null) product.ExpiryDate = next.ExpiryDate!.Value.ToString("yyyy-MM-dd");
    }
}
