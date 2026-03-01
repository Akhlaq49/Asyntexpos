using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PurchasesController : ControllerBase
{
    private readonly AppDbContext _context;

    public PurchasesController(AppDbContext context)
    {
        _context = context;
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

            var purchaseDtos = purchases.Select(p => MapToPurchaseDto(p)).ToList();
            return Ok(purchaseDtos);
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
            };

            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();

            // Add line items and sync inventory
            if (dto.Items != null && dto.Items.Count > 0)
            {
                foreach (var itemDto in dto.Items)
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
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.PurchaseItems.Add(item);

                    // Sync inventory: increase product quantity when purchase is Received
                    if (dto.Status == "Received" && itemDto.ProductId.HasValue)
                    {
                        var product = await _context.Products.FindAsync(itemDto.ProductId.Value);
                        if (product != null)
                        {
                            product.Quantity += (int)itemDto.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            // Re-fetch with items included
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

            var oldStatus = purchase.Status;
            var oldItems = purchase.Items?.ToList() ?? new List<PurchaseItem>();

            // Revert inventory for old "Received" items
            if (oldStatus == "Received")
            {
                foreach (var oldItem in oldItems)
                {
                    if (oldItem.ProductId.HasValue)
                    {
                        var product = await _context.Products.FindAsync(oldItem.ProductId.Value);
                        if (product != null)
                        {
                            product.Quantity -= (int)oldItem.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
            }

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

            // Remove old line items
            _context.PurchaseItems.RemoveRange(oldItems);

            // Add new line items and sync inventory
            if (dto.Items != null && dto.Items.Count > 0)
            {
                foreach (var itemDto in dto.Items)
                {
                    var item = new PurchaseItem
                    {
                        PurchaseId = id,
                        ProductId = itemDto.ProductId,
                        ProductName = itemDto.ProductName,
                        Quantity = itemDto.Quantity,
                        PurchasePrice = itemDto.PurchasePrice,
                        Discount = itemDto.Discount,
                        TaxPercentage = itemDto.TaxPercentage,
                        TaxAmount = itemDto.TaxAmount,
                        UnitCost = itemDto.UnitCost,
                        TotalCost = itemDto.TotalCost,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.PurchaseItems.Add(item);

                    // Sync inventory: increase product quantity if new status is Received
                    if (dto.Status == "Received" && itemDto.ProductId.HasValue)
                    {
                        var product = await _context.Products.FindAsync(itemDto.ProductId.Value);
                        if (product != null)
                        {
                            product.Quantity += (int)itemDto.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
            }

            _context.Purchases.Update(purchase);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Re-fetch with items
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

            // Revert inventory if purchase was Received
            if (purchase.Status == "Received" && purchase.Items != null)
            {
                foreach (var item in purchase.Items)
                {
                    if (item.ProductId.HasValue)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId.Value);
                        if (product != null)
                        {
                            product.Quantity -= (int)item.Quantity;
                            if (product.Quantity < 0) product.Quantity = 0;
                            product.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
            }

            // Delete related items
            if (purchase.Items != null)
                _context.PurchaseItems.RemoveRange(purchase.Items);

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
            TotalCost = i.TotalCost
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
            CreatedAt = purchase.CreatedAt,
            UpdatedAt = purchase.UpdatedAt
        };
    }
}

