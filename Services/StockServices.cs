using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class StockEntryService : IStockEntryService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ILocationService _locations;
    public StockEntryService(AppDbContext db, IInventoryService inventory, ILocationService locations)
    {
        _db = db;
        _inventory = inventory;
        _locations = locations;
    }

    private async Task<StockRef> RefFor(string reference, string? store, string? warehouse)
        => new("StockEntry", reference, null, await _locations.ResolveAsync(null, store) ?? await _locations.ResolveAsync(null, warehouse), null, "StockEntry");

    public async Task<List<StockEntryDto>> GetAllAsync()
    {
        var entries = await _db.StockEntries.OrderByDescending(s => s.Date).ToListAsync();
        var productIds = entries.Select(e => e.ProductId).Distinct().ToList();
        var products = await _db.Products.Include(p => p.Images)
            .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        return entries.Select(e =>
        {
            products.TryGetValue(e.ProductId, out var prod);
            return new StockEntryDto
            {
                Id = e.Id, Warehouse = e.Warehouse, Store = e.Store, ProductId = e.ProductId,
                ProductName = prod?.ProductName ?? "", ProductImage = prod?.Images.FirstOrDefault()?.ImagePath ?? "",
                Sku = prod?.SKU ?? "", Category = prod?.Category ?? "",
                Person = e.Person, Quantity = e.Quantity, Date = e.Date.ToString("dd MMM yyyy")
            };
        }).ToList();
    }

    public async Task<StockEntryDto?> CreateAsync(CreateStockEntryDto dto)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            await _inventory.AddStockAsync(product, dto.Quantity, null, null, product.PurchasePrice, store: dto.Store,
                at: await RefFor("Stock entry", dto.Store, dto.Warehouse));
            var entity = new StockEntry
            {
                Warehouse = dto.Warehouse, Store = dto.Store, ProductId = dto.ProductId,
                Person = dto.Person, Quantity = dto.Quantity, Date = DateTime.UtcNow
            };
            _db.StockEntries.Add(entity);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new StockEntryDto
            {
                Id = entity.Id, Warehouse = entity.Warehouse, Store = entity.Store, ProductId = entity.ProductId,
                ProductName = product.ProductName, ProductImage = product.Images.FirstOrDefault()?.ImagePath ?? "",
                Sku = product.SKU ?? "", Category = product.Category ?? "",
                Person = entity.Person, Quantity = entity.Quantity, Date = entity.Date.ToString("dd MMM yyyy")
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<StockEntryDto?> UpdateAsync(int id, CreateStockEntryDto dto)
    {
        var entity = await _db.StockEntries.FindAsync(id);
        if (entity == null) return null;

        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var oldProduct = await _db.Products.FirstOrDefaultAsync(p => p.Id == entity.ProductId);
            if (oldProduct != null) await _inventory.DeductFefoAsync(oldProduct, entity.Quantity, await RefFor($"Stock entry {entity.Id} edit", entity.Store, entity.Warehouse), spillToOtherLocations: true);
            await _inventory.AddStockAsync(product, dto.Quantity, null, null, product.PurchasePrice, store: dto.Store,
                at: await RefFor($"Stock entry {entity.Id} edit", dto.Store, dto.Warehouse));

            entity.Warehouse = dto.Warehouse;
            entity.Store = dto.Store;
            entity.ProductId = dto.ProductId;
            entity.Person = dto.Person;
            entity.Quantity = dto.Quantity;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new StockEntryDto
            {
                Id = entity.Id, Warehouse = entity.Warehouse, Store = entity.Store, ProductId = entity.ProductId,
                ProductName = product.ProductName, ProductImage = product.Images.FirstOrDefault()?.ImagePath ?? "",
                Sku = product.SKU ?? "", Category = product.Category ?? "",
                Person = entity.Person, Quantity = entity.Quantity, Date = entity.Date.ToString("dd MMM yyyy")
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.StockEntries.FindAsync(id);
        if (entity == null) return false;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == entity.ProductId);
            if (product != null) await _inventory.DeductFefoAsync(product, entity.Quantity, await RefFor($"Stock entry {entity.Id} deleted", entity.Store, entity.Warehouse), spillToOtherLocations: true);

            _db.StockEntries.Remove(entity);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public class StockAdjustmentService : IStockAdjustmentService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IAuditService _audit;

    private readonly ILocationService _locations;

    public StockAdjustmentService(AppDbContext db, IInventoryService inventory, IAuditService audit, ILocationService locations)
    {
        _db = db;
        _inventory = inventory;
        _audit = audit;
        _locations = locations;
    }

    public async Task<List<StockAdjustmentDto>> GetAllAsync()
    {
        var entries = await _db.StockAdjustments.OrderByDescending(s => s.Date).ToListAsync();
        var productIds = entries.Select(e => e.ProductId).Distinct().ToList();
        var products = await _db.Products.Include(p => p.Images)
            .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var batchIds = entries.Where(e => e.BatchId != null).Select(e => e.BatchId!.Value).Distinct().ToList();
        var batches = await _db.ProductBatches.Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BatchNo);

        return entries.Select(e =>
        {
            products.TryGetValue(e.ProductId, out var prod);
            return Map(e, prod, e.BatchId != null && batches.TryGetValue(e.BatchId.Value, out var no) ? no : null);
        }).ToList();
    }

    /// <summary>Applies a signed quantity to stock and returns the batch it touched.</summary>
    private async Task<int?> ApplyAsync(Product product, int quantity, int? batchId, string? batchNo, string? expiry, StockRef at)
    {
        if (quantity > 0)
        {
            if (batchId.HasValue)
            {
                var batch = await _db.ProductBatches.FirstOrDefaultAsync(b => b.Id == batchId && b.ProductId == product.Id);
                if (batch != null)
                {
                    await _inventory.RestoreAsync(product, new[] { new BatchAllocation(batch.Id, quantity, batch.PurchasePrice, batch.BatchNo, batch.ExpiryDate) }, 0, at);
                    return batch.Id;
                }
            }
            var added = await _inventory.AddStockAsync(product, quantity, batchNo,
                DateTime.TryParse(expiry, out var d) ? d : null, product.PurchasePrice, at: at);
            await _db.SaveChangesAsync();
            return added.Id;
        }
        if (quantity < 0)
        {
            if (batchId.HasValue)
            {
                var taken = await _inventory.DeductFromBatchAsync(product, batchId.Value, -quantity, at);
                if (taken != null && taken.Quantity < -quantity)
                    throw new InvalidOperationException($"The chosen batch has only {taken.Quantity} in stock.");
                return taken?.BatchId;
            }
            var allocations = await _inventory.DeductFefoAsync(product, -quantity, at, spillToOtherLocations: at.LocationId == null);
            return allocations.FirstOrDefault()?.BatchId;
        }
        return batchId;
    }

    public async Task<StockAdjustmentDto?> CreateAsync(CreateStockAdjustmentDto dto)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var locationId = await _locations.PickAsync(dto.LocationId, string.IsNullOrWhiteSpace(dto.Store) ? dto.Warehouse : dto.Store);
            var reference = string.IsNullOrWhiteSpace(dto.ReferenceNumber) ? "Adjustment" : dto.ReferenceNumber;
            var batchId = await ApplyAsync(product, dto.Quantity, dto.BatchId, dto.BatchNo, dto.ExpiryDate,
                new StockRef("Adjustment", reference, null, locationId, dto.VariantId, "StockAdjustment"));
            var entity = new StockAdjustment
            {
                LocationId = locationId, VariantId = dto.VariantId,
                Warehouse = dto.Warehouse, Store = dto.Store, ProductId = dto.ProductId,
                ReferenceNumber = dto.ReferenceNumber, Person = string.IsNullOrWhiteSpace(dto.Person) ? _audit.CurrentUser : dto.Person,
                Quantity = dto.Quantity, Notes = dto.Notes, Date = DateTime.UtcNow,
                Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Other" : dto.Reason, BatchId = batchId
            };
            _db.StockAdjustments.Add(entity);
            _audit.Log("StockAdjusted", "Product", product.Id, new { product = product.ProductName, dto.Quantity, entity.Reason, dto.Notes });
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Map(entity, product, await BatchNoOf(batchId));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<StockAdjustmentDto?> UpdateAsync(int id, CreateStockAdjustmentDto dto)
    {
        var entity = await _db.StockAdjustments.FindAsync(id);
        if (entity == null) return null;

        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var oldProduct = await _db.Products.FirstOrDefaultAsync(p => p.Id == entity.ProductId);
            var at = new StockRef("Adjustment", entity.ReferenceNumber + " edit", entity.Id, entity.LocationId, entity.VariantId, "StockAdjustment");
            if (oldProduct != null) await ApplyAsync(oldProduct, -entity.Quantity, entity.BatchId, null, null, at);
            var batchId = await ApplyAsync(product, dto.Quantity, dto.BatchId ?? (entity.ProductId == dto.ProductId ? entity.BatchId : null), dto.BatchNo, dto.ExpiryDate,
                at with { VariantId = dto.VariantId ?? entity.VariantId });

            _audit.Log("StockAdjustmentEdited", "StockAdjustment", entity.Id, new { product = product.ProductName, from = entity.Quantity, to = dto.Quantity, dto.Reason });
            entity.Warehouse = dto.Warehouse;
            entity.Store = dto.Store;
            entity.ProductId = dto.ProductId;
            entity.ReferenceNumber = dto.ReferenceNumber;
            entity.Person = string.IsNullOrWhiteSpace(dto.Person) ? entity.Person : dto.Person;
            entity.Quantity = dto.Quantity;
            entity.Notes = dto.Notes;
            entity.Reason = string.IsNullOrWhiteSpace(dto.Reason) ? entity.Reason : dto.Reason;
            entity.BatchId = batchId;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Map(entity, product, await BatchNoOf(batchId));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.StockAdjustments.FindAsync(id);
        if (entity == null) return false;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == entity.ProductId);
            if (product != null) await ApplyAsync(product, -entity.Quantity, entity.BatchId, null, null,
                new StockRef("Adjustment", entity.ReferenceNumber + " deleted", entity.Id, entity.LocationId, entity.VariantId, "StockAdjustment"));

            _audit.Log("Deleted", "StockAdjustment", entity.Id, new { product = product?.ProductName, entity.Quantity, entity.Reason });
            _db.StockAdjustments.Remove(entity);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string?> BatchNoOf(int? batchId)
        => batchId == null ? null : await _db.ProductBatches.Where(b => b.Id == batchId).Select(b => b.BatchNo).FirstOrDefaultAsync();

    private static StockAdjustmentDto Map(StockAdjustment e, Product? prod, string? batchNo) => new()
    {
        Id = e.Id, Warehouse = e.Warehouse, Store = e.Store, ProductId = e.ProductId,
        ProductName = prod?.ProductName ?? "", ProductImage = prod?.Images.FirstOrDefault()?.ImagePath ?? "",
        Sku = prod?.SKU ?? "", Category = prod?.Category ?? "",
        ReferenceNumber = e.ReferenceNumber, Person = e.Person,
        Quantity = e.Quantity, Notes = e.Notes, Date = e.Date.ToString("dd MMM yyyy"),
        Reason = e.Reason, BatchId = e.BatchId, BatchNo = batchNo
    };
}
