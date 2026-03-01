using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class FormFieldConfigService : IFormFieldConfigService
{
    private readonly AppDbContext _db;

    public FormFieldConfigService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<FormFieldConfig>> GetAllAsync()
    {
        return await _db.FormFieldConfigs
            .OrderBy(f => f.FormName)
            .ThenBy(f => f.FieldName)
            .ToListAsync();
    }

    public async Task<List<FormFieldConfig>> GetByFormAsync(string formName)
    {
        return await _db.FormFieldConfigs
            .Where(f => f.FormName == formName)
            .OrderBy(f => f.FieldName)
            .ToListAsync();
    }

    public async Task<FormFieldConfig?> GetByIdAsync(int id)
    {
        return await _db.FormFieldConfigs.FindAsync(id);
    }

    public async Task<FormFieldConfig> UpsertAsync(string formName, string fieldName, string fieldLabel, bool isVisible)
    {
        var existing = await _db.FormFieldConfigs
            .FirstOrDefaultAsync(f => f.FormName == formName && f.FieldName == fieldName);

        if (existing != null)
        {
            existing.FieldLabel = fieldLabel;
            existing.IsVisible = isVisible;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new FormFieldConfig
            {
                FormName = formName,
                FieldName = fieldName,
                FieldLabel = fieldLabel,
                IsVisible = isVisible,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.FormFieldConfigs.Add(existing);
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<List<FormFieldConfig>> BulkUpsertAsync(List<FormFieldConfig> configs)
    {
        var result = new List<FormFieldConfig>();

        foreach (var cfg in configs)
        {
            var existing = await _db.FormFieldConfigs
                .FirstOrDefaultAsync(f => f.FormName == cfg.FormName && f.FieldName == cfg.FieldName);

            if (existing != null)
            {
                existing.IsVisible = cfg.IsVisible;
                existing.FieldLabel = cfg.FieldLabel;
                existing.UpdatedAt = DateTime.UtcNow;
                result.Add(existing);
            }
            else
            {
                cfg.CreatedAt = DateTime.UtcNow;
                cfg.UpdatedAt = DateTime.UtcNow;
                _db.FormFieldConfigs.Add(cfg);
                result.Add(cfg);
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.FormFieldConfigs.FindAsync(id);
        if (entity == null) return false;
        _db.FormFieldConfigs.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task SeedDefaultsAsync()
    {
          var anyExist = await _db.FormFieldConfigs.AnyAsync();
        if (anyExist) return; // already seeded

        var defaults = new List<FormFieldConfig>
        {
            // Customer form fields
            new() { FormName = "Customer", FieldName = "name", FieldLabel = "Full Name", IsVisible = true },
            new() { FormName = "Customer", FieldName = "so", FieldLabel = "S/O (Father's Name)", IsVisible = true },
            new() { FormName = "Customer", FieldName = "cnic", FieldLabel = "CNIC", IsVisible = true },
            new() { FormName = "Customer", FieldName = "phone", FieldLabel = "Phone", IsVisible = true },
            new() { FormName = "Customer", FieldName = "email", FieldLabel = "Email", IsVisible = true },
            new() { FormName = "Customer", FieldName = "city", FieldLabel = "City", IsVisible = true },
            new() { FormName = "Customer", FieldName = "status", FieldLabel = "Status", IsVisible = true },
            new() { FormName = "Customer", FieldName = "picture", FieldLabel = "Photo", IsVisible = true },
            new() { FormName = "Customer", FieldName = "address", FieldLabel = "Address", IsVisible = true },

            // AddProduct form fields
            new() { FormName = "AddProduct", FieldName = "store", FieldLabel = "Store", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "warehouse", FieldLabel = "Warehouse", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "productName", FieldLabel = "Product Name", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "slug", FieldLabel = "Slug", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "sku", FieldLabel = "SKU", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "sellingType", FieldLabel = "Selling Type", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "category", FieldLabel = "Category", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "subCategory", FieldLabel = "Sub Category", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "brand", FieldLabel = "Brand", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "unit", FieldLabel = "Unit", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "barcodeSymbology", FieldLabel = "Barcode Symbology", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "itemBarcode", FieldLabel = "Item Barcode", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "description", FieldLabel = "Description", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "quantity", FieldLabel = "Quantity", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "price", FieldLabel = "Price", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "taxType", FieldLabel = "Tax Type", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "tax", FieldLabel = "Tax", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "discountType", FieldLabel = "Discount Type", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "discountValue", FieldLabel = "Discount Value", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "quantityAlert", FieldLabel = "Quantity Alert", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "warranty", FieldLabel = "Warranty", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "manufacturer", FieldLabel = "Manufacturer", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "manufacturedDate", FieldLabel = "Manufactured Date", IsVisible = true },
            new() { FormName = "AddProduct", FieldName = "expiryDate", FieldLabel = "Expiry Date", IsVisible = true },

            // CreateInstallment form fields
            new() { FormName = "CreateInstallment", FieldName = "customerId", FieldLabel = "Customer", IsVisible = true },
            new() { FormName = "CreateInstallment", FieldName = "productId", FieldLabel = "Product", IsVisible = true },
            new() { FormName = "CreateInstallment", FieldName = "financeAmount", FieldLabel = "Finance Amount", IsVisible = true },
            new() { FormName = "CreateInstallment", FieldName = "downPayment", FieldLabel = "Down Payment", IsVisible = true },
            new() { FormName = "CreateInstallment", FieldName = "interestRate", FieldLabel = "Interest Rate", IsVisible = true },
            new() { FormName = "CreateInstallment", FieldName = "tenure", FieldLabel = "Tenure (Months)", IsVisible = true },
            new() { FormName = "CreateInstallment", FieldName = "startDate", FieldLabel = "Start Date", IsVisible = true },

            // POS form fields
            new() { FormName = "POS", FieldName = "customer", FieldLabel = "Customer", IsVisible = true },
            new() { FormName = "POS", FieldName = "orderTax", FieldLabel = "Order Tax", IsVisible = true },
            new() { FormName = "POS", FieldName = "discount", FieldLabel = "Discount", IsVisible = true },
            new() { FormName = "POS", FieldName = "shipping", FieldLabel = "Shipping", IsVisible = true },
        };

        foreach (var d in defaults)
        {
            d.CreatedAt = DateTime.UtcNow;
            d.UpdatedAt = DateTime.UtcNow;
        }

        _db.FormFieldConfigs.AddRange(defaults);
        await _db.SaveChangesAsync();
    }
}
