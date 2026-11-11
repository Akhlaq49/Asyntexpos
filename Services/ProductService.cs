using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Controllers;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;
    private readonly IInventoryService _inventory;
    private readonly IPharmacySettingsService _settings;
    private readonly IAuditService _audit;

    public ProductService(AppDbContext db, IFileService fileService, IInventoryService inventory,
        IPharmacySettingsService settings, IAuditService audit)
    {
        _db = db;
        _fileService = fileService;
        _inventory = inventory;
        _settings = settings;
        _audit = audit;
    }

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, out var d) ? d.Date : null;

    public async Task<PagedResult<ProductDto>> GetAllPagedAsync(PaginationQuery query)
    {
        var q = _db.Products.AsQueryable();

        if (!string.IsNullOrEmpty(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(p => p.ProductName.ToLower().Contains(s) ||
                             (p.SKU != null && p.SKU.ToLower().Contains(s)) ||
                             (p.GenericName != null && p.GenericName.ToLower().Contains(s)) ||
                             (p.ItemBarcode != null && p.ItemBarcode.ToLower().Contains(s)) ||
                             (p.Category != null && p.Category.ToLower().Contains(s)) ||
                             (p.Brand != null && p.Brand.ToLower().Contains(s)));
        }

        q = q.OrderByDescending(p => p.CreatedAt);

        var totalCount = await q.CountAsync();
        var entities = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(p => p.Images)
            .ToListAsync();

        return new PagedResult<ProductDto>
        {
            Items = entities.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<ProductDto>> GetAllAsync()
    {
        var products = await _db.Products
            .Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var product = await _db.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);
        return product == null ? null : MapToDto(product);
    }

    public async Task<List<ProductDto>> GetExpiredAsync()
    {
        var today = DateTime.UtcNow.Date;
        var withExpiredBatches = await _db.ProductBatches
            .Where(b => b.Quantity > 0 && b.ExpiryDate != null && b.ExpiryDate < today)
            .Select(b => b.ProductId).Distinct().ToListAsync();
        var withBatches = await _db.ProductBatches.Select(b => b.ProductId).Distinct().ToListAsync();

        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => withExpiredBatches.Contains(p.Id) ||
                        (!withBatches.Contains(p.Id) && p.ExpiryDate != null && p.ExpiryDate != ""))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        // Products without batches: the stored text date must parse and be in the past.
        return products
            .Where(p => withExpiredBatches.Contains(p.Id) || (ParseDate(p.ExpiryDate) is DateTime d && d < today))
            .Select(MapToDto).ToList();
    }

    public async Task<ProductDto?> UpdateExpiredAsync(int id, UpdateExpiredDto dto)
    {
        var entity = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return null;

        entity.SKU = dto.Sku ?? entity.SKU;
        entity.ProductName = dto.ProductName ?? entity.ProductName;
        entity.ManufacturedDate = dto.ManufacturedDate ?? entity.ManufacturedDate;
        entity.ExpiryDate = dto.ExpiryDate ?? entity.ExpiryDate;
        entity.UpdatedAt = DateTime.UtcNow;

        // A product with a single batch: keep the batch expiry in step with the edited date.
        if (dto.ExpiryDate != null && ParseDate(dto.ExpiryDate) is DateTime newExpiry)
        {
            var batches = await _db.ProductBatches.Where(b => b.ProductId == id).ToListAsync();
            if (batches.Count == 1) { batches[0].ExpiryDate = newExpiry; batches[0].UpdatedAt = DateTime.UtcNow; }
        }

        await _db.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<(bool success, string? error)> DeleteExpiredAsync(int id)
    {
        var entity = await _db.Products.FindAsync(id);
        if (entity == null) return (false, null);

        var installmentCount = await _db.InstallmentPlans.CountAsync(ip => ip.ProductId == id);
        if (installmentCount > 0)
            return (false, $"Cannot delete this product. It is referenced by {installmentCount} installment plan(s).");

        _db.Products.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<ProductDto>> GetLowStocksAsync()
    {
        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.QuantityAlert > 0 && p.Quantity > 0 && p.Quantity <= p.QuantityAlert)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return products.Select(MapToDto).ToList();
    }

    public async Task<List<ProductDto>> GetOutOfStocksAsync()
    {
        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.QuantityAlert > 0 && p.Quantity == 0)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto?> UpdateLowStockAsync(int id, UpdateLowStockDto dto)
    {
        var entity = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return null;

        entity.Warehouse = dto.Warehouse ?? entity.Warehouse;
        entity.Store = dto.Store ?? entity.Store;
        entity.SKU = dto.Sku ?? entity.SKU;
        entity.Category = dto.Category ?? entity.Category;
        entity.ProductName = dto.ProductName ?? entity.ProductName;
        if (dto.Quantity.HasValue && dto.Quantity.Value != entity.Quantity)
        {
            _audit.Log("StockChanged", "Product", entity.Id, new { product = entity.ProductName, from = entity.Quantity, to = dto.Quantity.Value, via = "LowStock" });
            await _inventory.SetTotalAsync(entity, dto.Quantity.Value);
        }
        entity.QuantityAlert = dto.QuantityAlert ?? entity.QuantityAlert;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProductDto> CreateAsync(ProductFormModel form)
    {
        // Auto-generate defaults for fields that may be hidden via field visibility config
        var productName = string.IsNullOrWhiteSpace(form.ProductName) ? $"Product-{DateTime.UtcNow:yyyyMMddHHmmss}" : form.ProductName;
        var slug = string.IsNullOrWhiteSpace(form.Slug)
            ? productName.ToLower().Replace(" ", "-").Replace("--", "-")
            : form.Slug;
        var sku = string.IsNullOrWhiteSpace(form.Sku)
            ? "PT" + Guid.NewGuid().ToString("N")[..6].ToUpper()
            : form.Sku;

        var entity = new Product
        {
            Store = form.Store,
            Warehouse = form.Warehouse,
            ProductName = productName,
            Slug = slug,
            SKU = sku,
            SellingType = form.SellingType,
            Category = form.Category,
            SubCategory = form.SubCategory,
            Brand = form.Brand,
            Unit = form.Unit,
            BarcodeSymbology = form.BarcodeSymbology,
            ItemBarcode = form.ItemBarcode,
            Description = form.Description,
            ProductType = string.IsNullOrWhiteSpace(form.ProductType) ? "single" : form.ProductType,
            Quantity = 0,
            Price = form.Price,
            TaxType = form.TaxType,
            Tax = form.Tax,
            DiscountType = form.DiscountType,
            DiscountValue = form.DiscountValue,
            QuantityAlert = form.QuantityAlert,
            Warranty = form.Warranty,
            Manufacturer = form.Manufacturer,
            ManufacturedDate = form.ManufacturedDate,
            ExpiryDate = form.ExpiryDate
        };
        ApplyPharmacyFields(entity, form);

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Products.Add(entity);
            await _db.SaveChangesAsync();

            if (form.Quantity > 0)
            {
                await _inventory.AddStockAsync(entity, form.Quantity, form.BatchNo, ParseDate(form.ExpiryDate),
                    entity.PurchasePrice, manufacturedDate: ParseDate(form.ManufacturedDate), store: entity.Store,
                    at: new StockRef("Opening", "Opening stock", entity.Id, null, null, "Product"));
                await _db.SaveChangesAsync();
            }

            if (form.Images != null && form.Images.Count > 0)
            {
                foreach (var file in form.Images)
                {
                    var path = await _fileService.SaveFileAsync(file, "products");
                    _db.ProductImages.Add(new ProductImage
                    {
                        ProductId = entity.Id,
                        ImagePath = path
                    });
                }
                await _db.SaveChangesAsync();
            }

            await _db.Entry(entity).Collection(e => e.Images).LoadAsync();
            await transaction.CommitAsync();
            return MapToDto(entity);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ProductDto?> UpdateAsync(int id, ProductFormModel form)
    {
        var entity = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            entity.Store = form.Store;
            entity.Warehouse = form.Warehouse;
            entity.ProductName = form.ProductName ?? entity.ProductName;
            entity.Slug = form.Slug;
            entity.SKU = form.Sku;
            entity.SellingType = form.SellingType;
            entity.Category = form.Category;
            entity.SubCategory = form.SubCategory;
            entity.Brand = form.Brand;
            entity.Unit = form.Unit;
            entity.BarcodeSymbology = form.BarcodeSymbology;
            entity.ItemBarcode = form.ItemBarcode;
            entity.Description = form.Description;
            entity.ProductType = form.ProductType ?? entity.ProductType;
            if (entity.Price != form.Price || (form.PurchasePrice > 0 && entity.PurchasePrice != form.PurchasePrice))
                _audit.Log("PriceChanged", "Product", entity.Id, new
                {
                    product = entity.ProductName, retailFrom = entity.Price, retailTo = form.Price,
                    costFrom = entity.PurchasePrice, costTo = form.PurchasePrice
                });
            if (form.Quantity != entity.Quantity)
            {
                _audit.Log("StockChanged", "Product", entity.Id, new { product = entity.ProductName, from = entity.Quantity, to = form.Quantity, via = "ProductEdit" });
                await _inventory.SetTotalAsync(entity, form.Quantity, form.BatchNo, ParseDate(form.ExpiryDate));
            }
            entity.Price = form.Price;
            entity.TaxType = form.TaxType;
            entity.Tax = form.Tax;
            entity.DiscountType = form.DiscountType;
            entity.DiscountValue = form.DiscountValue;
            entity.QuantityAlert = form.QuantityAlert;
            entity.Warranty = form.Warranty;
            entity.Manufacturer = form.Manufacturer;
            entity.ManufacturedDate = form.ManufacturedDate;
            // When batches carry expiries, the product date follows the nearest batch instead.
            if (!await _db.ProductBatches.AnyAsync(b => b.ProductId == id && b.Quantity > 0 && b.ExpiryDate != null))
                entity.ExpiryDate = form.ExpiryDate;
            ApplyPharmacyFields(entity, form);
            entity.UpdatedAt = DateTime.UtcNow;

            if (form.Images != null && form.Images.Count > 0)
            {
                _db.ProductImages.RemoveRange(entity.Images);
                foreach (var file in form.Images)
                {
                    var path = await _fileService.SaveFileAsync(file, "products");
                    _db.ProductImages.Add(new ProductImage
                    {
                        ProductId = entity.Id,
                        ImagePath = path
                    });
                }
            }

            await _db.SaveChangesAsync();
            await _db.Entry(entity).Collection(e => e.Images).LoadAsync();
            await transaction.CommitAsync();
            return MapToDto(entity);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Products.FindAsync(id);
        if (entity == null) return (false, null);

        var installmentCount = await _db.InstallmentPlans.CountAsync(ip => ip.ProductId == id);
        if (installmentCount > 0)
            return (false, $"Cannot delete this product. It is referenced by {installmentCount} installment plan(s).");

        var soldCount = await _db.SaleItems.CountAsync(si => si.ProductId == id);
        if (soldCount > 0)
            return (false, $"Cannot delete this product. It appears on {soldCount} sale line(s); set its stock to 0 instead.");

        _audit.Log("Deleted", "Product", id, new { product = entity.ProductName });
        _db.Products.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private static void ApplyPharmacyFields(Product entity, ProductFormModel form)
    {
        entity.GenericName = form.GenericName?.Trim();
        entity.Strength = form.Strength?.Trim();
        entity.DosageForm = form.DosageForm?.Trim();
        entity.RackLocation = form.RackLocation?.Trim();
        entity.IsPrescriptionOnly = form.IsPrescriptionOnly || form.IsControlled;
        entity.IsControlled = form.IsControlled;
        if (form.PurchasePrice > 0) entity.PurchasePrice = form.PurchasePrice;
        entity.UnitsPerStrip = Math.Max(1, form.UnitsPerStrip);
        entity.StripsPerBox = Math.Max(1, form.StripsPerBox);
        entity.RefillDays = Math.Max(0, form.RefillDays);
        entity.SellOnline = form.SellOnline;
        entity.ArticleNo = string.IsNullOrWhiteSpace(form.ArticleNo) ? entity.ArticleNo : form.ArticleNo.Trim();
        entity.Season = string.IsNullOrWhiteSpace(form.Season) ? entity.Season : form.Season.Trim();
    }

    // ── Pharmacy: batches, expiry, substitutes ──

    public async Task<List<ProductBatchDto>> GetBatchesAsync(int productId)
    {
        var settings = await _settings.GetAsync();
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return new();
        var batches = await _db.ProductBatches.Where(b => b.ProductId == productId)
            .OrderBy(b => b.ExpiryDate == null).ThenBy(b => b.ExpiryDate).ToListAsync();
        return batches.Select(b => MapBatch(b, product, settings)).ToList();
    }

    public async Task<List<ProductBatchDto>> GetExpiringBatchesAsync(int days, bool includeExpired, string? manufacturer, string? supplier)
    {
        var settings = await _settings.GetAsync();
        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(Math.Max(0, days));

        var q = _db.ProductBatches.Include(b => b.Product)
            .Where(b => b.Quantity > 0 && b.ExpiryDate != null && b.ExpiryDate <= until);
        if (!includeExpired) q = q.Where(b => b.ExpiryDate >= today);
        if (!string.IsNullOrWhiteSpace(manufacturer))
            q = q.Where(b => b.Product != null && b.Product.Manufacturer != null && b.Product.Manufacturer.Contains(manufacturer));
        if (!string.IsNullOrWhiteSpace(supplier))
            q = q.Where(b => b.Supplier != null && b.Supplier.Contains(supplier));

        var rows = await q.OrderBy(b => b.ExpiryDate).ToListAsync();
        return rows.Select(b => MapBatch(b, b.Product!, settings)).ToList();
    }

    public async Task<ProductBatchDto?> UpdateBatchAsync(int batchId, UpdateBatchDto dto)
    {
        var batch = await _db.ProductBatches.Include(b => b.Product).FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch == null) return null;
        _audit.Log("BatchUpdated", "ProductBatch", batchId, new
        {
            product = batch.Product?.ProductName, batchFrom = batch.BatchNo, batchTo = dto.BatchNo,
            expiryFrom = batch.ExpiryDate?.ToString("yyyy-MM-dd"), expiryTo = dto.ExpiryDate
        });
        if (!string.IsNullOrWhiteSpace(dto.BatchNo)) batch.BatchNo = dto.BatchNo.Trim();
        if (dto.ExpiryDate != null) batch.ExpiryDate = ParseDate(dto.ExpiryDate);
        if (dto.ManufacturedDate != null) batch.ManufacturedDate = ParseDate(dto.ManufacturedDate);
        batch.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapBatch(batch, batch.Product!, await _settings.GetAsync());
    }

    public async Task<(bool success, string? error)> WriteOffBatchAsync(int batchId, WriteOffBatchDto dto, string user)
    {
        var batch = await _db.ProductBatches.FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch == null) return (false, null);
        var product = await _db.Products.FirstAsync(p => p.Id == batch.ProductId);
        var qty = dto.Quantity <= 0 ? batch.Quantity : Math.Min(dto.Quantity, batch.Quantity);
        if (qty <= 0) return (false, "This batch has no stock left to write off.");

        var reference = $"WO-{DateTime.UtcNow:yyMMddHHmmss}";
        var taken = await _inventory.DeductFromBatchAsync(product, batch.Id, qty, new StockRef("WriteOff", reference, null, batch.LocationId, batch.VariantId, "StockAdjustment"));
        _db.StockAdjustments.Add(new StockAdjustment
        {
            LocationId = batch.LocationId, VariantId = batch.VariantId,
            ProductId = product.Id, Warehouse = product.Warehouse ?? "", Store = product.Store ?? "",
            ReferenceNumber = reference, Person = user,
            Quantity = -(taken?.Quantity ?? qty), Reason = dto.Reason, BatchId = batch.Id,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? $"Batch {batch.BatchNo} written off" : dto.Notes,
            Date = DateTime.UtcNow
        });
        _audit.Log("WrittenOff", "ProductBatch", batch.Id, new { product = product.ProductName, batch.BatchNo, quantity = qty, dto.Reason });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<ProductDto>> GetSubstitutesAsync(int productId)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId);
        if (p == null || string.IsNullOrWhiteSpace(p.GenericName)) return new();
        var generic = p.GenericName.Trim().ToLower();
        var strength = (p.Strength ?? "").Trim().ToLower();

        var list = await _db.Products.Include(x => x.Images)
            .Where(x => x.Id != productId && x.Quantity > 0 && x.GenericName != null && x.GenericName.ToLower() == generic)
            .ToListAsync();
        return list
            .Where(x => strength == "" || (x.Strength ?? "").Trim().ToLower() == strength)
            .OrderBy(x => x.Price)
            .Select(MapToDto).ToList();
    }

    public static string ExpiryStatusFor(int? days, PharmacySettings s) => days switch
    {
        null => "Ok",
        < 0 => "Expired",
        var d when d <= s.ExpiryAlertDays3 => "Critical",
        var d when d <= s.ExpiryAlertDays2 => "Warning",
        var d when d <= s.ExpiryAlertDays1 => "Watch",
        _ => "Ok"
    };

    private static ProductBatchDto MapBatch(ProductBatch b, Product p, PharmacySettings s)
    {
        int? days = b.ExpiryDate == null ? null : (int)(b.ExpiryDate.Value.Date - DateTime.UtcNow.Date).TotalDays;
        return new ProductBatchDto
        {
            Id = b.Id, ProductId = b.ProductId, ProductName = p.ProductName, GenericName = p.GenericName ?? "",
            Manufacturer = p.Manufacturer ?? "", BatchNo = b.BatchNo,
            ExpiryDate = b.ExpiryDate?.ToString("yyyy-MM-dd"), ManufacturedDate = b.ManufacturedDate?.ToString("yyyy-MM-dd"),
            Quantity = b.Quantity, PurchasePrice = b.PurchasePrice, RetailPrice = p.Price,
            Supplier = b.Supplier, Store = b.Store, DaysToExpiry = days,
            ExpiryStatus = ExpiryStatusFor(days, s),
            StockValue = Math.Round(b.Quantity * (b.PurchasePrice > 0 ? b.PurchasePrice : p.PurchasePrice), 2)
        };
    }

    private static ProductDto MapToDto(Product p) => new()
    {
        Id = p.Id.ToString(),
        Store = p.Store ?? "",
        Warehouse = p.Warehouse ?? "",
        ProductName = p.ProductName,
        Slug = p.Slug ?? "",
        Sku = p.SKU ?? "",
        SellingType = p.SellingType ?? "",
        Category = p.Category ?? "",
        SubCategory = p.SubCategory ?? "",
        Brand = p.Brand ?? "",
        Unit = p.Unit ?? "",
        BarcodeSymbology = p.BarcodeSymbology ?? "",
        ItemBarcode = p.ItemBarcode ?? "",
        Description = p.Description ?? "",
        ProductType = p.ProductType,
        Quantity = p.Quantity,
        Price = p.Price,
        TaxType = p.TaxType ?? "",
        Tax = p.Tax ?? "",
        DiscountType = p.DiscountType ?? "",
        DiscountValue = p.DiscountValue,
        QuantityAlert = p.QuantityAlert,
        Warranty = p.Warranty ?? "",
        Manufacturer = p.Manufacturer ?? "",
        ManufacturedDate = p.ManufacturedDate ?? "",
        ExpiryDate = p.ExpiryDate ?? "",
        Images = p.Images.Select(i => i.ImagePath).ToArray(),
        GenericName = p.GenericName ?? "",
        Strength = p.Strength ?? "",
        DosageForm = p.DosageForm ?? "",
        RackLocation = p.RackLocation ?? "",
        IsPrescriptionOnly = p.IsPrescriptionOnly,
        IsControlled = p.IsControlled,
        PurchasePrice = p.PurchasePrice,
        UnitsPerStrip = p.UnitsPerStrip < 1 ? 1 : p.UnitsPerStrip,
        StripsPerBox = p.StripsPerBox < 1 ? 1 : p.StripsPerBox,
        RefillDays = p.RefillDays,
        SellOnline = p.SellOnline, ArticleNo = p.ArticleNo, Season = p.Season, HasVariants = p.HasVariants,
        IsMenuItem = p.IsMenuItem, KitchenStationId = p.KitchenStationId, MedicineMasterId = p.MedicineMasterId,
        CreatedAt = p.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
        UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
    };
}
