using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class StockEntryService : IStockEntryService
{
    private readonly AppDbContext _db;
    public StockEntryService(AppDbContext db) => _db = db;

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

        product.Quantity += dto.Quantity;
        var entity = new StockEntry
        {
            Warehouse = dto.Warehouse, Store = dto.Store, ProductId = dto.ProductId,
            Person = dto.Person, Quantity = dto.Quantity, Date = DateTime.UtcNow
        };
        _db.StockEntries.Add(entity);
        await _db.SaveChangesAsync();

        return new StockEntryDto
        {
            Id = entity.Id, Warehouse = entity.Warehouse, Store = entity.Store, ProductId = entity.ProductId,
            ProductName = product.ProductName, ProductImage = product.Images.FirstOrDefault()?.ImagePath ?? "",
            Sku = product.SKU ?? "", Category = product.Category ?? "",
            Person = entity.Person, Quantity = entity.Quantity, Date = entity.Date.ToString("dd MMM yyyy")
        };
    }

    public async Task<StockEntryDto?> UpdateAsync(int id, CreateStockEntryDto dto)
    {
        var entity = await _db.StockEntries.FindAsync(id);
        if (entity == null) return null;

        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null) return null;

        var oldProduct = await _db.Products.FindAsync(entity.ProductId);
        if (oldProduct != null) oldProduct.Quantity -= entity.Quantity;
        product.Quantity += dto.Quantity;

        entity.Warehouse = dto.Warehouse;
        entity.Store = dto.Store;
        entity.ProductId = dto.ProductId;
        entity.Person = dto.Person;
        entity.Quantity = dto.Quantity;
        await _db.SaveChangesAsync();

        return new StockEntryDto
        {
            Id = entity.Id, Warehouse = entity.Warehouse, Store = entity.Store, ProductId = entity.ProductId,
            ProductName = product.ProductName, ProductImage = product.Images.FirstOrDefault()?.ImagePath ?? "",
            Sku = product.SKU ?? "", Category = product.Category ?? "",
            Person = entity.Person, Quantity = entity.Quantity, Date = entity.Date.ToString("dd MMM yyyy")
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.StockEntries.FindAsync(id);
        if (entity == null) return false;

        var product = await _db.Products.FindAsync(entity.ProductId);
        if (product != null) product.Quantity -= entity.Quantity;

        _db.StockEntries.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}

public class StockTransferService : IStockTransferService
{
    private readonly AppDbContext _db;
    public StockTransferService(AppDbContext db) => _db = db;

    public async Task<object> GetAllAsync()
    {
        var transfers = await _db.StockTransfers.Include(t => t.Items).OrderByDescending(t => t.CreatedAt).ToListAsync();
        var productIds = transfers.SelectMany(t => t.Items.Select(i => i.ProductId)).Distinct().ToList();
        var products = await _db.Products.Include(p => p.Images).Where(p => productIds.Contains(p.Id)).ToListAsync();
        var productMap = products.ToDictionary(p => p.Id);

        return transfers.Select(t => new StockTransferDto
        {
            Id = t.Id, WarehouseFrom = t.WarehouseFrom, WarehouseTo = t.WarehouseTo,
            ReferenceNumber = t.ReferenceNumber, Notes = t.Notes,
            NoOfProducts = t.Items.Count, QuantityTransferred = t.Items.Sum(i => i.Quantity),
            Date = t.Date.ToString("dd MMM yyyy"),
            Items = t.Items.Select(i =>
            {
                productMap.TryGetValue(i.ProductId, out var prod);
                return new StockTransferItemDto
                {
                    Id = i.Id, ProductId = i.ProductId, ProductName = prod?.ProductName ?? "",
                    ProductImage = prod?.Images.FirstOrDefault()?.ImagePath ?? "",
                    Sku = prod?.SKU ?? "", Category = prod?.Category ?? "", Quantity = i.Quantity
                };
            }).ToList()
        }).ToList();
    }

    public async Task<object> CreateAsync(CreateStockTransferDto dto)
    {
        var entity = new StockTransfer
        {
            WarehouseFrom = dto.WarehouseFrom, WarehouseTo = dto.WarehouseTo,
            ReferenceNumber = dto.ReferenceNumber, Notes = dto.Notes,
            Date = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            Items = dto.Items.Select(i => new StockTransferItem
            {
                ProductId = i.ProductId, Quantity = i.Quantity
            }).ToList()
        };
        _db.StockTransfers.Add(entity);
        await _db.SaveChangesAsync();
        return new { entity.Id };
    }

    public async Task<object?> UpdateAsync(int id, CreateStockTransferDto dto)
    {
        var entity = await _db.StockTransfers.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null) return null;

        entity.WarehouseFrom = dto.WarehouseFrom;
        entity.WarehouseTo = dto.WarehouseTo;
        entity.ReferenceNumber = dto.ReferenceNumber;
        entity.Notes = dto.Notes;

        _db.StockTransferItems.RemoveRange(entity.Items);
        entity.Items = dto.Items.Select(i => new StockTransferItem
        {
            StockTransferId = id, ProductId = i.ProductId, Quantity = i.Quantity
        }).ToList();

        await _db.SaveChangesAsync();
        return new { entity.Id };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.StockTransfers.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null) return false;
        _db.StockTransfers.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}

public class StockAdjustmentService : IStockAdjustmentService
{
    private readonly AppDbContext _db;
    public StockAdjustmentService(AppDbContext db) => _db = db;

    public async Task<List<StockAdjustmentDto>> GetAllAsync()
    {
        var entries = await _db.StockAdjustments.OrderByDescending(s => s.Date).ToListAsync();
        var productIds = entries.Select(e => e.ProductId).Distinct().ToList();
        var products = await _db.Products.Include(p => p.Images)
            .Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        return entries.Select(e =>
        {
            products.TryGetValue(e.ProductId, out var prod);
            return new StockAdjustmentDto
            {
                Id = e.Id, Warehouse = e.Warehouse, Store = e.Store, ProductId = e.ProductId,
                ProductName = prod?.ProductName ?? "", ProductImage = prod?.Images.FirstOrDefault()?.ImagePath ?? "",
                Sku = prod?.SKU ?? "", Category = prod?.Category ?? "",
                ReferenceNumber = e.ReferenceNumber, Person = e.Person,
                Quantity = e.Quantity, Notes = e.Notes, Date = e.Date.ToString("dd MMM yyyy")
            };
        }).ToList();
    }

    public async Task<StockAdjustmentDto?> CreateAsync(CreateStockAdjustmentDto dto)
    {
        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null) return null;

        product.Quantity += dto.Quantity;
        var entity = new StockAdjustment
        {
            Warehouse = dto.Warehouse, Store = dto.Store, ProductId = dto.ProductId,
            ReferenceNumber = dto.ReferenceNumber, Person = dto.Person,
            Quantity = dto.Quantity, Notes = dto.Notes, Date = DateTime.UtcNow
        };
        _db.StockAdjustments.Add(entity);
        await _db.SaveChangesAsync();

        return new StockAdjustmentDto
        {
            Id = entity.Id, Warehouse = entity.Warehouse, Store = entity.Store, ProductId = entity.ProductId,
            ProductName = product.ProductName, ProductImage = product.Images.FirstOrDefault()?.ImagePath ?? "",
            Sku = product.SKU ?? "", Category = product.Category ?? "",
            ReferenceNumber = entity.ReferenceNumber, Person = entity.Person,
            Quantity = entity.Quantity, Notes = entity.Notes, Date = entity.Date.ToString("dd MMM yyyy")
        };
    }

    public async Task<StockAdjustmentDto?> UpdateAsync(int id, CreateStockAdjustmentDto dto)
    {
        var entity = await _db.StockAdjustments.FindAsync(id);
        if (entity == null) return null;

        var product = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product == null) return null;

        var oldProduct = await _db.Products.FindAsync(entity.ProductId);
        if (oldProduct != null) oldProduct.Quantity -= entity.Quantity;
        product.Quantity += dto.Quantity;

        entity.Warehouse = dto.Warehouse;
        entity.Store = dto.Store;
        entity.ProductId = dto.ProductId;
        entity.ReferenceNumber = dto.ReferenceNumber;
        entity.Person = dto.Person;
        entity.Quantity = dto.Quantity;
        entity.Notes = dto.Notes;
        await _db.SaveChangesAsync();

        return new StockAdjustmentDto
        {
            Id = entity.Id, Warehouse = entity.Warehouse, Store = entity.Store, ProductId = entity.ProductId,
            ProductName = product.ProductName, ProductImage = product.Images.FirstOrDefault()?.ImagePath ?? "",
            Sku = product.SKU ?? "", Category = product.Category ?? "",
            ReferenceNumber = entity.ReferenceNumber, Person = entity.Person,
            Quantity = entity.Quantity, Notes = entity.Notes, Date = entity.Date.ToString("dd MMM yyyy")
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.StockAdjustments.FindAsync(id);
        if (entity == null) return false;

        var product = await _db.Products.FindAsync(entity.ProductId);
        if (product != null) product.Quantity -= entity.Quantity;

        _db.StockAdjustments.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}
