using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class StockEntryService : IStockEntryService
{
    private readonly AppDbContext _db;
    public StockEntryService(AppDbContext db) => _db = db;

    // Each stock entry has a matching purchase (Reference "STOCK-<entry id>") worth product price x quantity,
    // so added stock shows in the dashboard's Total Purchase. The line item is not linked to the product,
    // because the stock entry itself already changes the product quantity.
    private static string PurchaseRef(int entryId) => $"STOCK-{entryId}";

    private async Task SyncPurchaseAsync(StockEntry entry, Product product)
    {
        var reference = PurchaseRef(entry.Id);
        var purchase = await _db.Purchases.Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Reference == reference && p.SupplierName == "Stock Entry");
        var total = entry.Quantity * product.Price;
        if (purchase == null)
        {
            purchase = new Purchase
            {
                SupplierName = "Stock Entry",
                Reference = reference,
                Date = entry.Date,
                Status = "Received",
                PaymentStatus = "Paid",
                Items = new List<PurchaseItem>()
            };
            _db.Purchases.Add(purchase);
        }
        purchase.Total = total;
        purchase.Paid = total;
        purchase.UpdatedAt = DateTime.UtcNow;
        purchase.Notes = $"Stock added for {product.ProductName}" + (string.IsNullOrWhiteSpace(entry.Person) ? "" : $" by {entry.Person}");
        if (purchase.Items != null && purchase.Items.Count > 0) _db.PurchaseItems.RemoveRange(purchase.Items);
        purchase.Items = new List<PurchaseItem>
        {
            new()
            {
                ProductName = product.ProductName,
                Quantity = entry.Quantity,
                PurchasePrice = product.Price,
                UnitCost = product.Price,
                TotalCost = total
            }
        };
    }

    private async Task RemovePurchaseAsync(int entryId)
    {
        var reference = PurchaseRef(entryId);
        var purchase = await _db.Purchases.Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Reference == reference && p.SupplierName == "Stock Entry");
        if (purchase == null) return;
        if (purchase.Items != null) _db.PurchaseItems.RemoveRange(purchase.Items);
        _db.Purchases.Remove(purchase);
    }

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
                Id = e.Id, ProductId = e.ProductId,
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
            product.Quantity += dto.Quantity;
            var entity = new StockEntry
            {
                ProductId = dto.ProductId,
                Person = dto.Person, Quantity = dto.Quantity, Date = DateTime.UtcNow
            };
            _db.StockEntries.Add(entity);
            await _db.SaveChangesAsync();
            await SyncPurchaseAsync(entity, product);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new StockEntryDto
            {
                Id = entity.Id, ProductId = entity.ProductId,
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
            var oldProduct = await _db.Products.FindAsync(entity.ProductId);
            if (oldProduct != null) oldProduct.Quantity -= entity.Quantity;
            product.Quantity += dto.Quantity;

            entity.ProductId = dto.ProductId;
            if (!string.IsNullOrWhiteSpace(dto.Person)) entity.Person = dto.Person;
            entity.Quantity = dto.Quantity;
            await _db.SaveChangesAsync();
            await SyncPurchaseAsync(entity, product);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new StockEntryDto
            {
                Id = entity.Id, ProductId = entity.ProductId,
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
            var product = await _db.Products.FindAsync(entity.ProductId);
            if (product != null) product.Quantity -= entity.Quantity;

            await RemovePurchaseAsync(entity.Id);
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

