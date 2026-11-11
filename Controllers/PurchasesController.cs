using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchasesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IInventoryService _inventory;
    private readonly IAuditService _audit;
    private readonly ILocationService _locations;

    public PurchasesController(AppDbContext context, IInventoryService inventory, IAuditService audit, ILocationService locations)
    {
        _locations = locations;
        _context = context;
        _inventory = inventory;
        _audit = audit;
    }

    // GET: api/purchases
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PurchaseDto>>> GetAllPurchases()
    {
        try
        {
            var purchases = await _context.Purchases
                .Include(p => p.Items)
                .OrderByDescending(p => p.Date)
                .ToListAsync();

            return Ok(purchases.Select(MapToPurchaseDto).ToList());
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET: api/purchases/:id
    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseDto>> GetPurchaseById(int id)
    {
        try
        {
            var purchase = await _context.Purchases
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
                return NotFound(new { error = "Purchase not found" });

            return Ok(MapToPurchaseDto(purchase));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Low and out-of-stock items with a suggested order quantity and the supplier they were last bought from.
    /// The screen groups these by supplier and posts each group as an "Ordered" purchase.
    /// </summary>
    [HttpGet("reorder-suggestions")]
    public async Task<IActionResult> GetReorderSuggestions()
    {
        var products = await _context.Products
            .Where(p => p.QuantityAlert > 0 && p.Quantity <= p.QuantityAlert)
            .OrderBy(p => p.ProductName)
            .ToListAsync();
        var ids = products.Select(p => p.Id).ToList();

        var lastBuys = await _context.PurchaseItems
            .Where(i => i.ProductId != null && ids.Contains(i.ProductId.Value))
            .Join(_context.Purchases, i => i.PurchaseId, p => p.Id, (i, p) => new { ProductId = i.ProductId!.Value, p.SupplierName, p.Date, i.PurchasePrice, i.PurchaseUnit })
            .ToListAsync();

        // Items already on an open order are shown so nobody orders them twice.
        var onOrder = await _context.PurchaseItems
            .Where(i => i.ProductId != null && ids.Contains(i.ProductId.Value))
            .Join(_context.Purchases.Where(p => p.Status == "Ordered" || p.Status == "Pending"), i => i.PurchaseId, p => p.Id,
                (i, p) => new { ProductId = i.ProductId!.Value, i.Quantity })
            .ToListAsync();

        var result = products.Select(p =>
        {
            var last = lastBuys.Where(b => b.ProductId == p.Id).OrderByDescending(b => b.Date).FirstOrDefault();
            var target = p.QuantityAlert * 2;
            var suggested = Math.Max(target - p.Quantity, p.QuantityAlert);
            return new
            {
                productId = p.Id,
                productName = p.ProductName,
                genericName = p.GenericName ?? "",
                strength = p.Strength ?? "",
                currentStock = p.Quantity,
                quantityAlert = p.QuantityAlert,
                suggestedQuantity = suggested,
                unitsPerBox = Math.Max(1, p.UnitsPerStrip) * Math.Max(1, p.StripsPerBox),
                onOrder = onOrder.Where(o => o.ProductId == p.Id).Sum(o => o.Quantity),
                lastSupplier = last?.SupplierName ?? "",
                lastPurchasePrice = last?.PurchasePrice ?? p.PurchasePrice,
                lastPurchaseUnit = last?.PurchaseUnit ?? "Unit",
                lastPurchaseDate = last?.Date.ToString("yyyy-MM-dd")
            };
        }).ToList();
        return Ok(result);
    }

    // POST: api/purchases
    [HttpPost]
    public async Task<ActionResult<PurchaseDto>> CreatePurchase([FromBody] CreatePurchaseDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var purchase = new Purchase
            {
                SupplierName = dto.SupplierName,
                SupplierRef = dto.SupplierRef,
                Reference = dto.Reference,
                Date = dto.Date,
                Status = dto.Status,
                OrderTax = dto.OrderTax,
                Discount = dto.Discount,
                Shipping = dto.Shipping,
                Total = dto.Total,
                Paid = dto.Paid,
                Notes = dto.Notes,
                PaymentStatus = CalculatePaymentStatus(dto.Paid, dto.Total),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LocationId = await _locations.PickAsync(dto.LocationId),
            };

            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();

            await AddItemsAsync(purchase, dto.Items, dto.Status == "Received");

            if (dto.Paid > 0)
            {
                _context.SupplierPayments.Add(new SupplierPayment
                {
                    SupplierId = await SupplierIdAsync(purchase.SupplierName),
                    SupplierName = purchase.SupplierName, PurchaseId = purchase.Id, Amount = dto.Paid,
                    PaymentType = "Cash", Reference = purchase.Reference, Notes = "Paid on purchase entry",
                    PaymentDate = purchase.Date, CreatedBy = _audit.CurrentUser
                });
            }
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var createdPurchase = await _context.Purchases
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == purchase.Id);

            return CreatedAtAction(nameof(GetPurchaseById), new { id = purchase.Id },
                MapToPurchaseDto(createdPurchase!));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT: api/purchases/:id
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePurchase(int id, [FromBody] UpdatePurchaseDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var purchase = await _context.Purchases
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
                return NotFound(new { error = "Purchase not found" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var oldItems = purchase.Items?.ToList() ?? new List<PurchaseItem>();
            if (purchase.Status == "Received")
                await RevertItemsAsync(oldItems);

            var paidDelta = dto.Paid - purchase.Paid;

            purchase.SupplierName = dto.SupplierName;
            purchase.SupplierRef = dto.SupplierRef;
            purchase.Reference = dto.Reference;
            purchase.Date = dto.Date;
            purchase.Status = dto.Status;
            purchase.OrderTax = dto.OrderTax;
            purchase.Discount = dto.Discount;
            purchase.Shipping = dto.Shipping;
            purchase.Total = dto.Total;
            purchase.Paid = dto.Paid;
            purchase.Notes = dto.Notes;
            purchase.PaymentStatus = CalculatePaymentStatus(dto.Paid, dto.Total);
            purchase.UpdatedAt = DateTime.UtcNow;
            if (dto.LocationId.HasValue) purchase.LocationId = await _locations.PickAsync(dto.LocationId);

            _context.PurchaseItems.RemoveRange(oldItems);
            await _context.SaveChangesAsync();

            await AddItemsAsync(purchase, dto.Items, dto.Status == "Received");

            if (paidDelta != 0)
            {
                _context.SupplierPayments.Add(new SupplierPayment
                {
                    SupplierId = await SupplierIdAsync(purchase.SupplierName),
                    SupplierName = purchase.SupplierName, PurchaseId = purchase.Id, Amount = paidDelta,
                    PaymentType = "Cash", Reference = purchase.Reference,
                    Notes = paidDelta > 0 ? "Paid amount increased on purchase edit" : "Paid amount reduced on purchase edit",
                    PaymentDate = DateTime.UtcNow, CreatedBy = _audit.CurrentUser
                });
            }
            _audit.Log("Updated", "Purchase", purchase.Id, new { purchase.Reference, purchase.Total, purchase.Status });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var updatedPurchase = await _context.Purchases
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            return Ok(new
            {
                message = "Purchase updated successfully",
                purchase = MapToPurchaseDto(updatedPurchase!)
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    // DELETE: api/purchases/:id
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePurchase(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var purchase = await _context.Purchases
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
                return NotFound(new { error = "Purchase not found" });

            if (await _context.PurchaseReturns.AnyAsync(r => r.PurchaseId == id))
                return BadRequest(new { error = "This purchase has supplier returns recorded against it. Delete those returns first." });

            if (purchase.Status == "Received" && purchase.Items != null)
                await RevertItemsAsync(purchase.Items.ToList());

            if (purchase.Items != null)
                _context.PurchaseItems.RemoveRange(purchase.Items);

            var payments = await _context.SupplierPayments.Where(p => p.PurchaseId == id).ToListAsync();
            _context.SupplierPayments.RemoveRange(payments);

            _audit.Log("Deleted", "Purchase", purchase.Id, new { purchase.Reference, purchase.SupplierName, purchase.Total });
            _context.Purchases.Remove(purchase);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Purchase deleted successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── helpers ──

    /// <summary>Base units in one purchase unit of this product (Unit = 1, Strip, Box).</summary>
    public static int UnitMultiplier(Product product, string? unit) => (unit ?? "Unit").ToLowerInvariant() switch
    {
        "strip" => Math.Max(1, product.UnitsPerStrip),
        "box" => Math.Max(1, product.UnitsPerStrip) * Math.Max(1, product.StripsPerBox),
        _ => 1
    };

    private async Task AddItemsAsync(Purchase purchase, List<CreatePurchaseItemDto>? items, bool received)
    {
        if (items == null || items.Count == 0) return;

        foreach (var itemDto in items)
        {
            var item = new PurchaseItem
            {
                PurchaseId = purchase.Id,
                ProductId = itemDto.ProductId,
                ProductName = itemDto.ProductName,
                Quantity = itemDto.Quantity,
                PurchasePrice = itemDto.PurchasePrice,
                Discount = itemDto.Discount,
                TaxPercentage = itemDto.TaxPercentage,
                TaxAmount = itemDto.TaxAmount,
                UnitCost = itemDto.UnitCost,
                TotalCost = itemDto.TotalCost,
                BatchNo = string.IsNullOrWhiteSpace(itemDto.BatchNo) ? null : itemDto.BatchNo.Trim(),
                ExpiryDate = ParseDate(itemDto.ExpiryDate),
                ManufacturedDate = ParseDate(itemDto.ManufacturedDate),
                BonusQuantity = Math.Max(0, itemDto.BonusQuantity),
                PurchaseUnit = string.IsNullOrWhiteSpace(itemDto.PurchaseUnit) ? "Unit" : itemDto.PurchaseUnit,
                VariantId = itemDto.VariantId,
                CreatedAt = DateTime.UtcNow
            };
            _context.PurchaseItems.Add(item);
            await _context.SaveChangesAsync(); // need item.Id for the batch link

            if (!received || !itemDto.ProductId.HasValue) continue;

            var product = await _context.Products.FindAsync(itemDto.ProductId.Value);
            if (product == null) continue;

            var multiplier = UnitMultiplier(product, item.PurchaseUnit);
            var paidUnits = (int)Math.Round(item.Quantity * multiplier);
            var freeUnits = (int)Math.Round(item.BonusQuantity * multiplier);
            var baseUnits = paidUnits + freeUnits;
            if (baseUnits <= 0) continue;

            // Cost per base unit spreads the line total over paid + free units.
            var lineTotal = item.TotalCost > 0 ? item.TotalCost : item.PurchasePrice * item.Quantity;
            var unitCost = lineTotal > 0 ? Math.Round(lineTotal / baseUnits, 4) : 0;

            await _inventory.AddStockAsync(product, baseUnits, item.BatchNo, item.ExpiryDate, unitCost,
                supplier: purchase.SupplierName, purchaseId: purchase.Id, purchaseItemId: item.Id,
                manufacturedDate: item.ManufacturedDate,
                at: new StockRef("Purchase", purchase.Reference, purchase.Id, purchase.LocationId, item.VariantId, "Purchase"));
        }
        await _context.SaveChangesAsync();
    }

    private async Task RevertItemsAsync(List<PurchaseItem> items)
    {
        foreach (var oldItem in items)
        {
            if (!oldItem.ProductId.HasValue) continue;
            var product = await _context.Products.FindAsync(oldItem.ProductId.Value);
            if (product == null) continue;
            var multiplier = UnitMultiplier(product, oldItem.PurchaseUnit);
            var baseUnits = (int)Math.Round((oldItem.Quantity + oldItem.BonusQuantity) * multiplier);
            await _inventory.RemovePurchaseItemStockAsync(product, oldItem.Id, baseUnits,
                new StockRef("Purchase", "Purchase edit", oldItem.PurchaseId, null, oldItem.VariantId, "Purchase"));
        }
    }

    private static DateTime? ParseDate(string? value) => DateTime.TryParse(value, out var d) ? d.Date : null;

    private async Task<int> SupplierIdAsync(string name)
        => await _context.Parties.Where(p => p.Role == "Supplier" && (p.FullName == name || p.CompanyName == name))
            .Select(p => p.Id).FirstOrDefaultAsync();

    private string CalculatePaymentStatus(decimal paid, decimal total)
    {
        if (paid >= total)
            return "Paid";
        else if (paid == 0)
            return "Unpaid";
        else if (paid > 0 && paid < total)
            return "Partial";
        else
            return "Overdue";
    }

    private PurchaseDto MapToPurchaseDto(Purchase purchase)
    {
        var items = purchase.Items?.Select(i => new PurchaseItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            PurchasePrice = i.PurchasePrice,
            Discount = i.Discount,
            TaxPercentage = i.TaxPercentage,
            TaxAmount = i.TaxAmount,
            UnitCost = i.UnitCost,
            TotalCost = i.TotalCost,
            BatchNo = i.BatchNo,
            ExpiryDate = i.ExpiryDate?.ToString("yyyy-MM-dd"),
            ManufacturedDate = i.ManufacturedDate?.ToString("yyyy-MM-dd"),
            BonusQuantity = i.BonusQuantity,
            PurchaseUnit = i.PurchaseUnit ?? "Unit",
            VariantId = i.VariantId
        }).ToList() ?? new List<PurchaseItemDto>();

        return new PurchaseDto
        {
            Id = purchase.Id,
            SupplierName = purchase.SupplierName,
            SupplierRef = purchase.SupplierRef,
            Reference = purchase.Reference,
            Date = purchase.Date,
            Status = purchase.Status,
            PaymentStatus = purchase.PaymentStatus,
            OrderTax = purchase.OrderTax,
            Discount = purchase.Discount,
            Shipping = purchase.Shipping,
            Total = purchase.Total,
            Paid = purchase.Paid,
            Notes = purchase.Notes,
            Items = items,
            LocationId = purchase.LocationId,
            CreatedAt = purchase.CreatedAt,
            UpdatedAt = purchase.UpdatedAt
        };
    }
}
