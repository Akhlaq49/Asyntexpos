using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class StorefrontService : IStorefrontService
{
    private readonly AppDbContext _db;

    public StorefrontService(AppDbContext db) => _db = db;

    // ──────────────────────────────────────────
    // CATEGORIES
    // ──────────────────────────────────────────

    public async Task<List<StorefrontCategoryDto>> GetAllCategoriesAsync()
    {
        var categories = await _db.Categories
            .Where(c => c.Status == "active")
            .Include(c => c.SubCategories)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return categories.Select(c => MapCategory(c)).ToList();
    }

    public async Task<StorefrontCategoryDto?> GetCategoryByIdAsync(int id)
    {
        var c = await _db.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id);

        return c == null ? null : MapCategory(c);
    }

    // ──────────────────────────────────────────
    // PRODUCTS
    // ──────────────────────────────────────────

    public async Task<List<StorefrontProductDto>> GetAllProductsAsync()
    {
        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.Quantity > 0 && !p.IsRawMaterial)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return products.Select(p => MapProduct(p)).ToList();
    }

    public async Task<StorefrontProductDto?> GetProductByIdAsync(int id)
    {
        var p = await _db.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        return p == null ? null : MapProduct(p);
    }

    public async Task<List<StorefrontProductDto>> GetProductsByCategoryAsync(int categoryId)
    {
        var category = await _db.Categories.FindAsync(categoryId);
        if (category == null) return new List<StorefrontProductDto>();

        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.Category == category.Name && p.Quantity > 0 && !p.IsRawMaterial)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return products.Select(p => MapProduct(p)).ToList();
    }

    public async Task<List<StorefrontProductDto>> GetProductsBySubCategoryAsync(int subCategoryId)
    {
        var subCategory = await _db.SubCategories.FindAsync(subCategoryId);
        if (subCategory == null) return new List<StorefrontProductDto>();

        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.SubCategory == subCategory.SubCategoryName && p.Quantity > 0 && !p.IsRawMaterial)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return products.Select(p => MapProduct(p)).ToList();
    }

    public async Task<List<StorefrontProductDto>> SearchProductsAsync(string query)
    {
        var lowerQuery = query.ToLower();

        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.Quantity > 0 && !p.IsRawMaterial && (
                p.ProductName.ToLower().Contains(lowerQuery) ||
                (p.Description != null && p.Description.ToLower().Contains(lowerQuery)) ||
                (p.Brand != null && p.Brand.ToLower().Contains(lowerQuery)) ||
                (p.Category != null && p.Category.ToLower().Contains(lowerQuery)) ||
                (p.SKU != null && p.SKU.ToLower().Contains(lowerQuery))
            ))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return products.Select(p => MapProduct(p)).ToList();
    }

    // ──────────────────────────────────────────
    // ORDERS
    // ──────────────────────────────────────────

    public async Task<StorefrontOrderResultDto> CreateOrderAsync(StorefrontOrderDto dto)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. Find or create Party (customer)
            Party? party = null;
            if (dto.CustomerId.HasValue)
                party = await _db.Parties.FindAsync(dto.CustomerId.Value);

            if (party == null && !string.IsNullOrWhiteSpace(dto.CustomerEmail))
                party = await _db.Parties.FirstOrDefaultAsync(p => p.Email == dto.CustomerEmail && p.Role == "Customer");

            if (party == null)
            {
                party = new Party
                {
                    FullName = dto.CustomerName,
                    Email = dto.CustomerEmail,
                    Phone = dto.CustomerPhone,
                    Role = "Customer",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                if (dto.CreateAccount && !string.IsNullOrWhiteSpace(dto.AccountPassword))
                    party.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.AccountPassword);

                _db.Parties.Add(party);
                await _db.SaveChangesAsync();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(party.Phone) && !string.IsNullOrWhiteSpace(dto.CustomerPhone))
                    party.Phone = dto.CustomerPhone;
            }

            // 2. Create PartyAddress records
            PartyAddress? billingAddr = null;
            if (dto.BillingAddress != null)
            {
                billingAddr = new PartyAddress
                {
                    PartyId = party.Id,
                    AddressType = "Billing",
                    FirstName = dto.BillingAddress.FirstName,
                    LastName = dto.BillingAddress.LastName,
                    AddressLine1 = dto.BillingAddress.AddressLine1,
                    AddressLine2 = dto.BillingAddress.AddressLine2,
                    City = dto.BillingAddress.City,
                    State = dto.BillingAddress.State,
                    PostalCode = dto.BillingAddress.PostalCode,
                    Country = dto.BillingAddress.Country,
                    CompanyName = dto.BillingAddress.CompanyName,
                    Email = dto.BillingAddress.Email,
                    Phone = dto.BillingAddress.Phone,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.PartyAddresses.Add(billingAddr);
            }

            PartyAddress? shippingAddr = null;
            if (dto.ShippingAddress != null)
            {
                shippingAddr = new PartyAddress
                {
                    PartyId = party.Id,
                    AddressType = "Shipping",
                    FirstName = dto.ShippingAddress.FirstName,
                    LastName = dto.ShippingAddress.LastName,
                    AddressLine1 = dto.ShippingAddress.AddressLine1,
                    AddressLine2 = dto.ShippingAddress.AddressLine2,
                    City = dto.ShippingAddress.City,
                    State = dto.ShippingAddress.State,
                    PostalCode = dto.ShippingAddress.PostalCode,
                    Country = dto.ShippingAddress.Country,
                    CompanyName = dto.ShippingAddress.CompanyName,
                    Email = dto.ShippingAddress.Email,
                    Phone = dto.ShippingAddress.Phone,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.PartyAddresses.Add(shippingAddr);
            }

            if (billingAddr != null || shippingAddr != null)
                await _db.SaveChangesAsync();

            // 3. Generate next Sale reference
            var lastRef = await _db.Sales
                .OrderByDescending(s => s.Id)
                .Select(s => s.Reference)
                .FirstOrDefaultAsync();

            int nextNum = 1;
            if (!string.IsNullOrEmpty(lastRef) && lastRef.StartsWith("SL"))
            {
                int.TryParse(lastRef.Substring(2), out nextNum);
                nextNum++;
            }
            var reference = $"SL{nextNum:D3}";

            var orderNumber = $"ONL-{Guid.NewGuid().ToString("N")[..7].ToUpper()}";

            // 4. Build SaleItems
            var saleItems = new List<SaleItem>();
            foreach (var i in dto.Items)
            {
                var productName = i.ProductName ?? "";
                if (string.IsNullOrEmpty(productName))
                {
                    var product = await _db.Products.FindAsync(i.ProductId);
                    if (product != null) productName = product.ProductName;
                }
                saleItems.Add(new SaleItem
                {
                    ProductId = i.ProductId,
                    ProductName = productName,
                    Quantity = i.Quantity,
                    PurchasePrice = i.Price,
                    Discount = 0,
                    TaxPercent = dto.Tax > 0 && dto.SubTotal > 0
                        ? Math.Round((dto.Tax / dto.SubTotal) * 100, 2) : 0,
                    TaxAmount = dto.Items.Count > 0
                        ? Math.Round(dto.Tax / dto.Items.Count, 2) : 0,
                    UnitCost = i.Price,
                    TotalCost = i.Price * i.Quantity
                });
            }

            // 5. Create Sale (Source = "online")
            var sale = new Sale
            {
                Reference = reference,
                OrderNumber = orderNumber,
                CustomerId = party.Id,
                CustomerName = party.FullName,
                Biller = "Online Store",
                Source = "online",
                GrandTotal = dto.GrandTotal,
                Paid = 0,
                Due = dto.GrandTotal,
                OrderTax = dto.Tax,
                Discount = dto.Discount,
                Shipping = dto.Shipping,
                Status = "Pending",
                PaymentStatus = "Unpaid",
                Notes = dto.Notes,
                BillingAddressId = billingAddr?.Id,
                ShippingAddressId = shippingAddr?.Id,
                SaleDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                Items = saleItems
            };

            _db.Sales.Add(sale);

            // Deduct product quantities
            foreach (var item in saleItems)
            {
                var prod = await _db.Products.FindAsync(item.ProductId);
                if (prod != null)
                    prod.Quantity -= item.Quantity;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new StorefrontOrderResultDto { Id = sale.Id, OrderNumber = sale.OrderNumber ?? reference };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<StorefrontOrderSummaryDto>> GetOrdersByEmailAsync(string email)
    {
        return await _db.Sales
            .Include(s => s.Items)
            .Include(s => s.Customer)
            .Where(s => s.Source == "online" && s.Customer != null && s.Customer.Email == email)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new StorefrontOrderSummaryDto
            {
                Id = s.Id,
                OrderNumber = s.OrderNumber ?? s.Reference,
                CustomerName = s.CustomerName,
                Amount = s.GrandTotal,
                TotalAmount = s.GrandTotal,
                Status = s.Status,
                OrderSource = "Online",
                PaymentStatus = s.PaymentStatus,
                OrderDate = s.SaleDate.ToString("dd MMM yyyy, hh:mm tt"),
                CreatedAt = s.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                ItemCount = s.Items.Count
            })
            .ToListAsync();
    }

    public async Task<StorefrontOrderDetailDto?> GetOrderByIdAsync(int id)
    {
        var s = await _db.Sales
            .Include(s => s.Items)
            .Include(s => s.BillingAddress)
            .Include(s => s.ShippingAddress)
            .FirstOrDefaultAsync(s => s.Id == id && s.Source == "online");

        return s == null ? null : MapOrderDetail(s);
    }

    public async Task<StorefrontOrderDetailDto?> GetOrderByOrderNumberAsync(string orderNumber)
    {
        var s = await _db.Sales
            .Include(s => s.Items)
            .Include(s => s.BillingAddress)
            .Include(s => s.ShippingAddress)
            .FirstOrDefaultAsync(s =>
                (s.OrderNumber == orderNumber || s.Reference == orderNumber) && s.Source == "online");

        return s == null ? null : MapOrderDetail(s);
    }

    // ──────────────────────────────────────────
    // AUTH
    // ──────────────────────────────────────────

    public async Task<StorefrontAuthResultDto> RegisterAsync(StorefrontRegisterDto dto)
    {
        if (await _db.Parties.IgnoreQueryFilters().AnyAsync(p => p.Email == dto.Email))
            return new StorefrontAuthResultDto
            {
                Success = false,
                StatusCode = 409,
                ErrorMessage = "Email already registered"
            };

        var party = new Party
        {
            FullName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Customer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Parties.Add(party);
        await _db.SaveChangesAsync();

        return new StorefrontAuthResultDto
        {
            Success = true,
            Customer = MapCustomer(party)
        };
    }

    public async Task<StorefrontAuthResultDto> LoginAsync(StorefrontLoginDto dto)
    {
        var party = await _db.Parties
            .FirstOrDefaultAsync(p => p.Email == dto.Email && p.Role == "Customer");

        if (party == null || string.IsNullOrEmpty(party.PasswordHash)
                         || !BCrypt.Net.BCrypt.Verify(dto.Password, party.PasswordHash))
            return new StorefrontAuthResultDto
            {
                Success = false,
                StatusCode = 401,
                ErrorMessage = "Invalid email or password"
            };

        return new StorefrontAuthResultDto
        {
            Success = true,
            Customer = MapCustomer(party)
        };
    }

    // ──────────────────────────────────────────
    // CUSTOMER / ACCOUNT
    // ──────────────────────────────────────────

    public async Task<StorefrontCustomerDto?> GetCustomerByEmailAsync(string email)
    {
        var party = await _db.Parties
            .FirstOrDefaultAsync(p => p.Email == email && p.Role == "Customer");
        return party == null ? null : MapCustomer(party);
    }

    public async Task<StorefrontCustomerDto?> GetCustomerByIdAsync(int id)
    {
        var party = await _db.Parties.FindAsync(id);
        if (party == null || party.Role != "Customer") return null;
        return MapCustomer(party);
    }

    public async Task<StorefrontCustomerDto?> UpdateCustomerAsync(int customerId, StorefrontUpdateCustomerDto dto)
    {
        var party = await _db.Parties.FindAsync(customerId);
        if (party == null || party.Role != "Customer") return null;

        if (!string.IsNullOrWhiteSpace(dto.CustomerName))
            party.FullName = dto.CustomerName;
        if (dto.CustomerPhone != null)
            party.Phone = dto.CustomerPhone;

        await _db.SaveChangesAsync();
        return MapCustomer(party);
    }

    public async Task<bool> ChangePasswordAsync(int customerId, StorefrontChangePasswordDto dto)
    {
        var party = await _db.Parties.FindAsync(customerId);
        if (party == null || party.Role != "Customer") return false;

        if (string.IsNullOrEmpty(party.PasswordHash)
            || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, party.PasswordHash))
            return false;

        party.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<StorefrontAddressResultDto>> GetCustomerAddressesAsync(int customerId)
    {
        var addresses = await _db.PartyAddresses
            .Where(a => a.PartyId == customerId)
            .OrderBy(a => a.AddressType)
            .ToListAsync();

        return addresses.Select(MapAddress).ToList();
    }

    public async Task<StorefrontAddressResultDto?> CreateOrUpdateAddressAsync(int customerId, StorefrontSaveAddressDto dto)
    {
        // Verify customer exists
        var party = await _db.Parties.FindAsync(customerId);
        if (party == null || party.Role != "Customer") return null;

        PartyAddress? addr;

        if (dto.AddressId.HasValue && dto.AddressId.Value > 0)
        {
            addr = await _db.PartyAddresses
                .FirstOrDefaultAsync(a => a.Id == dto.AddressId.Value && a.PartyId == customerId);
            if (addr == null) return null;
        }
        else
        {
            // Try to find existing address of same type
            addr = await _db.PartyAddresses
                .FirstOrDefaultAsync(a => a.PartyId == customerId && a.AddressType == dto.AddressType);
        }

        if (addr == null)
        {
            addr = new PartyAddress
            {
                PartyId = customerId,
                AddressType = dto.AddressType,
                CreatedAt = DateTime.UtcNow
            };
            _db.PartyAddresses.Add(addr);
        }

        addr.FirstName = dto.FirstName;
        addr.LastName = dto.LastName;
        addr.AddressLine1 = dto.AddressLine1;
        addr.AddressLine2 = dto.AddressLine2;
        addr.City = dto.City;
        addr.State = dto.State;
        addr.PostalCode = dto.PostalCode;
        addr.Country = dto.Country;
        addr.CompanyName = dto.CompanyName;
        addr.Email = dto.Email;
        addr.Phone = dto.Phone;
        addr.IsDefault = dto.IsDefault;
        addr.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapAddress(addr);
    }

    public async Task<bool> DeleteAddressAsync(int customerId, int addressId)
    {
        var addr = await _db.PartyAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.PartyId == customerId);
        if (addr == null) return false;

        _db.PartyAddresses.Remove(addr);
        await _db.SaveChangesAsync();
        return true;
    }

    // ──────────────────────────────────────────
    // MAPPING HELPERS
    // ──────────────────────────────────────────

    private StorefrontCategoryDto MapCategory(Category c)
    {
        return new StorefrontCategoryDto
        {
            Id = c.Id.ToString(),
            CategoryName = c.Name,
            CategorySlug = c.Slug,
            IsActive = c.Status == "active",
            SubCategories = c.SubCategories
                .Where(sc => sc.Status == "active")
                .Select(sc => new StorefrontSubCategoryDto
                {
                    Id = sc.Id.ToString(),
                    SubCategoryName = sc.SubCategoryName,
                    CategoryCode = sc.CategoryCode ?? "",
                    IsActive = sc.Status == "active",
                    SubCategory1s = new List<object>()
                }).ToList()
        };
    }

    private StorefrontProductDto MapProduct(Product p)
    {
        var categoryId = _db.Categories
            .Where(c => c.Name == p.Category)
            .Select(c => c.Id)
            .FirstOrDefault();

        var subCategoryId = _db.SubCategories
            .Where(sc => sc.SubCategoryName == p.SubCategory)
            .Select(sc => sc.Id)
            .FirstOrDefault();

        var images = p.Images?.Select(i => i.ImagePath).ToArray() ?? Array.Empty<string>();

        return new StorefrontProductDto
        {
            Id = p.Id.ToString(),
            ProductName = p.ProductName,
            Price = p.Price,
            Description = p.Description ?? "",
            Sku = p.SKU ?? "",
            Stock = p.Quantity,
            ImagePath = images.Length > 0 ? images[0] : null,
            ImagePaths = images,
            CategoryId = categoryId > 0 ? categoryId.ToString() : null,
            SubCategoryId = subCategoryId > 0 ? subCategoryId.ToString() : null,
            Category = new StorefrontProductCategoryRef
            {
                Id = categoryId.ToString(),
                CategoryName = p.Category ?? ""
            },
            Brand = new StorefrontProductBrandRef { BrandName = p.Brand ?? "" },
            Unit = p.Unit ?? "",
            DiscountType = p.DiscountType ?? "",
            DiscountValue = p.DiscountValue,
            CreatedOn = p.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
        };
    }

    private static StorefrontCustomerDto MapCustomer(Party p) => new()
    {
        Id = p.Id,
        CustomerName = p.FullName,
        CustomerEmail = p.Email,
        CustomerPhone = p.Phone,
        Address = p.Address,
        City = p.City
    };

    private static StorefrontAddressResultDto MapAddress(PartyAddress a) => new()
    {
        Id = a.Id,
        AddressType = a.AddressType,
        FirstName = a.FirstName,
        LastName = a.LastName,
        AddressLine1 = a.AddressLine1,
        AddressLine2 = a.AddressLine2,
        City = a.City,
        State = a.State,
        PostalCode = a.PostalCode,
        Country = a.Country,
        CompanyName = a.CompanyName,
        Email = a.Email,
        Phone = a.Phone,
        IsDefault = a.IsDefault
    };

    private StorefrontOrderDetailDto MapOrderDetail(Sale s)
    {
        string? FormatAddr(PartyAddress? a) =>
            a == null ? null : string.Join(", ",
                new[] { a.AddressLine1, a.AddressLine2, a.City, a.State, a.PostalCode, a.Country }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

        // Resolve product images for items
        var productIds = s.Items.Select(i => i.ProductId).Distinct().ToList();
        var productImages = _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Images)
            .ToDictionary(
                p => p.Id,
                p => p.Images.FirstOrDefault()?.ImagePath);

        var productSkus = _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.SKU);

        return new StorefrontOrderDetailDto
        {
            Id = s.Id,
            OrderNumber = s.OrderNumber ?? s.Reference,
            CustomerName = s.CustomerName,
            TotalAmount = s.GrandTotal,
            SubTotal = s.GrandTotal - s.Shipping + s.Discount - s.OrderTax,
            Shipping = s.Shipping,
            Discount = s.Discount,
            Tax = s.OrderTax,
            Status = s.Status,
            PaymentStatus = s.PaymentStatus,
            Notes = s.Notes,
            CreatedAt = s.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            BillingAddress = FormatAddr(s.BillingAddress),
            ShippingAddress = FormatAddr(s.ShippingAddress),
            Items = s.Items.Select(i => new StorefrontOrderItemDetailDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ProductSku = productSkus.GetValueOrDefault(i.ProductId),
                ImagePath = productImages.GetValueOrDefault(i.ProductId),
                Quantity = i.Quantity,
                Price = i.UnitCost,
                UnitPrice = i.UnitCost,
                TotalCost = i.TotalCost
            }).ToList()
        };
    }
}
