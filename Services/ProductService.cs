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

    public ProductService(AppDbContext db, IFileService fileService)
    {
        _db = db;
        _fileService = fileService;
    }

    public async Task<PagedResult<ProductDto>> GetAllPagedAsync(PaginationQuery query)
    {
        var q = _db.Products.AsQueryable();

        if (!string.IsNullOrEmpty(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(p => p.ProductName.ToLower().Contains(s) ||
                             (p.SKU != null && p.SKU.ToLower().Contains(s)) ||
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
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.ExpiryDate == null || string.Compare(p.ExpiryDate, today) <= 0)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return products.Select(MapToDto).ToList();
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
        entity.Quantity = dto.Quantity ?? entity.Quantity;
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
            Quantity = form.Quantity,
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

        _db.Products.Add(entity);
        await _db.SaveChangesAsync();

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
        return MapToDto(entity);
    }

    public async Task<ProductDto?> UpdateAsync(int id, ProductFormModel form)
    {
        var entity = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) return null;

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
        entity.Quantity = form.Quantity;
        entity.Price = form.Price;
        entity.TaxType = form.TaxType;
        entity.Tax = form.Tax;
        entity.DiscountType = form.DiscountType;
        entity.DiscountValue = form.DiscountValue;
        entity.QuantityAlert = form.QuantityAlert;
        entity.Warranty = form.Warranty;
        entity.Manufacturer = form.Manufacturer;
        entity.ManufacturedDate = form.ManufacturedDate;
        entity.ExpiryDate = form.ExpiryDate;
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
        return MapToDto(entity);
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
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
        CreatedAt = p.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
        UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
    };
}
