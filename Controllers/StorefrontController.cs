using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

/// <summary>
/// Public storefront API — no authentication required.
/// Used by the online store (nest-react-frontend).
/// </summary>
[ApiController]
[Route("api/storefront")]
public class StorefrontController : ControllerBase
{
    private readonly AppDbContext _db;

    public StorefrontController(AppDbContext db) => _db = db;

    // ──────────────────────────────────────────
    // CATEGORIES
    // ──────────────────────────────────────────

    /// <summary>
    /// GET /api/storefront/categories/all
    /// Returns categories with nested subcategories (hierarchical).
    /// Shape matches what nest-react-frontend expects.
    /// </summary>
    [HttpGet("categories/all")]
    public async Task<IActionResult> GetAllCategories()
    {
        var categories = await _db.Categories
            .Where(c => c.Status == "active")
            .Include(c => c.SubCategories)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var result = categories.Select(c => new
        {
            id = c.Id.ToString(),
            categoryName = c.Name,
            categorySlug = c.Slug,
            isActive = c.Status == "active",
            subCategories = c.SubCategories
                .Where(sc => sc.Status == "active")
                .Select(sc => new
                {
                    id = sc.Id.ToString(),
                    subCategoryName = sc.SubCategoryName,
                    categoryCode = sc.CategoryCode ?? "",
                    isActive = sc.Status == "active",
                    subCategory1s = new object[] { } // no third level in ReactPosApi
                }).ToList()
        }).ToList();

        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// GET /api/storefront/categories/:id
    /// </summary>
    [HttpGet("categories/{id}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var c = await _db.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (c == null) return NotFound(new { success = false, message = "Category not found" });

        var result = new
        {
            id = c.Id.ToString(),
            categoryName = c.Name,
            categorySlug = c.Slug,
            isActive = c.Status == "active",
            subCategories = c.SubCategories.Where(sc => sc.Status == "active").Select(sc => new
            {
                id = sc.Id.ToString(),
                subCategoryName = sc.SubCategoryName,
                categoryCode = sc.CategoryCode ?? "",
                isActive = sc.Status == "active"
            }).ToList()
        };

        return Ok(new { success = true, data = result });
    }

    // ──────────────────────────────────────────
    // PRODUCTS
    // ──────────────────────────────────────────

    /// <summary>
    /// GET /api/storefront/products/all
    /// Returns all active products in the shape nest-react-frontend expects.
    /// </summary>
    [HttpGet("products/all")]
    public async Task<IActionResult> GetAllProducts()
    {
        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.Quantity > 0)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var result = products.Select((p, idx) => MapProduct(p)).ToList();
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// GET /api/storefront/products/:id
    /// </summary>
    [HttpGet("products/{id}")]
    public async Task<IActionResult> GetProductById(int id)
    {
        var p = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound(new { success = false, message = "Product not found" });
        return Ok(new { success = true, data = MapProduct(p) });
    }

    /// <summary>
    /// GET /api/storefront/products/category/:categoryId
    /// Filter products by category name (resolved from category ID).
    /// </summary>
    [HttpGet("products/category/{categoryId}")]
    public async Task<IActionResult> GetProductsByCategory(int categoryId)
    {
        var category = await _db.Categories.FindAsync(categoryId);
        if (category == null) return Ok(new { success = true, data = new List<object>() });

        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.Category == category.Name && p.Quantity > 0)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var result = products.Select(p => MapProduct(p)).ToList();
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// GET /api/storefront/products/subcategory/:subCategoryId
    /// Filter products by subcategory name (resolved from subcategory ID).
    /// </summary>
    [HttpGet("products/subcategory/{subCategoryId}")]
    public async Task<IActionResult> GetProductsBySubCategory(int subCategoryId)
    {
        var subCategory = await _db.SubCategories.FindAsync(subCategoryId);
        if (subCategory == null) return Ok(new { success = true, data = new List<object>() });

        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.SubCategory == subCategory.SubCategoryName && p.Quantity > 0)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var result = products.Select(p => MapProduct(p)).ToList();
        return Ok(new { success = true, data = result });
    }

    // ──────────────────────────────────────────
    // ORDERS (Online)
    // ──────────────────────────────────────────

    /// <summary>
    /// POST /api/storefront/orders
    /// Creates a Sale with Source = "online" (unified Sale table).
    /// Uses Party for customer info, PartyAddress for addresses.
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] StorefrontOrderDto dto)
    {
        // 1. Find or create Party (customer)
        Party? party = null;
        if (dto.CustomerId.HasValue)
        {
            party = await _db.Parties.FindAsync(dto.CustomerId.Value);
        }

        if (party == null && !string.IsNullOrWhiteSpace(dto.CustomerEmail))
        {
            party = await _db.Parties.FirstOrDefaultAsync(p => p.Email == dto.CustomerEmail && p.Role == "Customer");
        }

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
            {
                party.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.AccountPassword);
            }

            _db.Parties.Add(party);
            await _db.SaveChangesAsync();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(party.Phone) && !string.IsNullOrWhiteSpace(dto.CustomerPhone))
            {
                party.Phone = dto.CustomerPhone;
            }
        }

        // 2. Create PartyAddress records for billing and shipping
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
        var lastRef = await _db.Sales.OrderByDescending(s => s.Id).Select(s => s.Reference).FirstOrDefaultAsync();
        int nextNum = 1;
        if (!string.IsNullOrEmpty(lastRef) && lastRef.StartsWith("SL"))
        { int.TryParse(lastRef.Substring(2), out nextNum); nextNum++; }
        var reference = $"SL{nextNum:D3}";

        var orderNumber = $"ONL-{new Random().Next(1000000, 9999999)}";

        // 4. Build SaleItems (resolve product names if missing)
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
                TaxPercent = dto.Tax > 0 && dto.SubTotal > 0 ? Math.Round((dto.Tax / dto.SubTotal) * 100, 2) : 0,
                TaxAmount = dto.Items.Count > 0 ? Math.Round(dto.Tax / dto.Items.Count, 2) : 0,
                UnitCost = i.Price,
                TotalCost = i.Price * i.Quantity
            });
        }

        // 5. Create unified Sale record (Source = "online")
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
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            data = new { id = sale.Id, orderNumber = sale.OrderNumber },
            message = "Order placed successfully"
        });
    }

    /// <summary>
    /// GET /api/storefront/orders/email/:email
    /// Get orders (Sales with Source="online") by customer email.
    /// </summary>
    [HttpGet("orders/email/{email}")]
    public async Task<IActionResult> GetOrdersByEmail(string email)
    {
        var orders = await _db.Sales
            .Include(s => s.Items)
            .Include(s => s.Customer)
            .Where(s => s.Source == "online" && s.Customer != null && s.Customer.Email == email)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                id = s.Id,
                orderNumber = s.OrderNumber ?? s.Reference,
                customerName = s.CustomerName,
                amount = s.GrandTotal,
                status = s.Status,
                orderSource = "Online",
                paymentStatus = s.PaymentStatus,
                orderDate = s.SaleDate.ToString("dd MMM yyyy, hh:mm tt"),
                itemCount = s.Items.Count
            })
            .ToListAsync();

        return Ok(new { success = true, data = orders });
    }

    // ──────────────────────────────────────────
    // AUTH (for online customers)
    // ──────────────────────────────────────────

    /// <summary>
    /// POST /api/storefront/auth/register
    /// Register a new online customer.
    /// </summary>
    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] StorefrontRegisterDto dto)
    {
        if (await _db.Parties.AnyAsync(p => p.Email == dto.Email))
            return Conflict(new { success = false, message = "Email already registered" });

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

        return Ok(new
        {
            success = true,
            data = new
            {
                id = party.Id,
                customerName = party.FullName,
                customerEmail = party.Email,
                customerPhone = party.Phone
            },
            message = "Registration successful"
        });
    }

    /// <summary>
    /// POST /api/storefront/auth/login
    /// Login an online customer.
    /// </summary>
    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] StorefrontLoginDto dto)
    {
        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Email == dto.Email && p.Role == "Customer");
        if (party == null || !BCrypt.Net.BCrypt.Verify(dto.Password, party.PasswordHash))
            return Unauthorized(new { success = false, message = "Invalid email or password" });

        return Ok(new
        {
            success = true,
            data = new
            {
                id = party.Id,
                customerName = party.FullName,
                customerEmail = party.Email,
                customerPhone = party.Phone
            },
            message = "Login successful"
        });
    }

    /// <summary>
    /// GET /api/storefront/customers/email/:email
    /// Get customer by email for checkout pre-fill.
    /// </summary>
    [HttpGet("customers/email/{email}")]
    public async Task<IActionResult> GetCustomerByEmail(string email)
    {
        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Email == email && p.Role == "Customer");
        if (party == null) return NotFound(new { success = false, message = "Customer not found" });

        return Ok(new
        {
            success = true,
            data = new
            {
                id = party.Id,
                customerName = party.FullName,
                customerEmail = party.Email,
                customerPhone = party.Phone,
                address = party.Address,
                city = party.City
            }
        });
    }

    // ──────────────────────────────────────────
    // HELPER
    // ──────────────────────────────────────────

    private object MapProduct(Product p)
    {
        // Resolve categoryId from category name
        var categoryId = _db.Categories
            .Where(c => c.Name == p.Category)
            .Select(c => c.Id)
            .FirstOrDefault();

        var subCategoryId = _db.SubCategories
            .Where(sc => sc.SubCategoryName == p.SubCategory)
            .Select(sc => sc.Id)
            .FirstOrDefault();

        var images = p.Images?.Select(i => i.ImagePath).ToArray() ?? Array.Empty<string>();

        return new
        {
            id = p.Id.ToString(),
            productName = p.ProductName,
            price = p.Price,
            description = p.Description ?? "",
            sku = p.SKU ?? "",
            stock = p.Quantity,
            imagePath = images.Length > 0 ? images[0] : null,
            imagePaths = images,
            categoryId = categoryId > 0 ? categoryId.ToString() : null,
            subCategoryId = subCategoryId > 0 ? subCategoryId.ToString() : null,
            category = new { id = categoryId.ToString(), categoryName = p.Category ?? "" },
            brand = new { brandName = p.Brand ?? "" },
            unit = p.Unit ?? "",
            discountType = p.DiscountType ?? "",
            discountValue = p.DiscountValue,
            createdOn = p.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
        };
    }
}

// ──────────────────────────────────────────
// DTOs for the storefront
// ──────────────────────────────────────────

public class StorefrontOrderDto
{
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public StorefrontAddressDto? BillingAddress { get; set; }
    public StorefrontAddressDto? ShippingAddress { get; set; }
    public List<StorefrontOrderItemDto> Items { get; set; } = new();
    public string? PaymentMethod { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Shipping { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public string? AdditionalInfo { get; set; }
    public bool CreateAccount { get; set; }
    public string? AccountPassword { get; set; }
}

public class StorefrontOrderItemDto
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
}

public class StorefrontAddressDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class StorefrontRegisterDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class StorefrontLoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
