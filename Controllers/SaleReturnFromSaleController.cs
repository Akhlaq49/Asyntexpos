using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

/// <summary>
/// Return (part of) a POS sale: find it by its reference, pick the quantities that come back.
/// The products go back into stock and the return is valued at the sale's own prices.
/// </summary>
[Authorize]
[ApiController]
[Route("api/salesreturns")]
public class SaleReturnFromSaleController : ControllerBase
{
    private readonly AppDbContext _db;
    public SaleReturnFromSaleController(AppDbContext db) => _db = db;

    public record ReturnableLine(int SaleItemId, int ProductId, string ProductName, int Quantity, decimal UnitPrice, int Returned, int Returnable);
    public record SaleLookup(int SaleId, string Reference, int? CustomerId, string CustomerName, DateTime SaleDate, decimal GrandTotal, decimal Paid,
        decimal OrderTax, decimal Discount, decimal ItemsTotal, List<ReturnableLine> Items, List<string> PreviousReturns);
    public record ReturnLine(int SaleItemId, int Quantity);
    public record CreateFromSale(int SaleId, List<ReturnLine> Lines, decimal Refund, DateTime? ReturnDate);

    /// <summary>The sale with each product's sold / already returned / still returnable quantity</summary>
    [HttpGet("sale-lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string reference)
    {
        var r = (reference ?? "").Trim();
        if (r.Length == 0) return BadRequest(new { message = "Enter the sale reference (e.g. SL001)." });
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Reference == r);
        if (sale == null) return NotFound(new { message = $"No sale with reference {r} was found." });
        if (ReactPosApi.Models.SaleStatus.IsQueued(sale.Status)) return BadRequest(new { message = $"{sale.Reference} is still in the sale queue - it hasn't been sold, so nothing can be returned." });
        return Ok(await BuildLookupAsync(sale));
    }

    [HttpPost("from-sale")]
    public async Task<IActionResult> Create([FromBody] CreateFromSale dto)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == dto.SaleId);
        if (sale == null) return NotFound(new { message = "The sale no longer exists." });
        if (ReactPosApi.Models.SaleStatus.IsQueued(sale.Status)) return BadRequest(new { message = $"{sale.Reference} is still in the sale queue - it hasn't been sold, so nothing can be returned." });
        var lookup = await BuildLookupAsync(sale);
        var lines = (dto.Lines ?? new()).Where(l => l.Quantity != 0).ToList();
        if (lines.Count == 0) return BadRequest(new { message = "Enter the quantity coming back for at least one product." });
        foreach (var l in lines)
        {
            var item = lookup.Items.FirstOrDefault(i => i.SaleItemId == l.SaleItemId);
            if (item == null) return BadRequest(new { message = "One of the lines doesn't belong to this sale." });
            if (l.Quantity < 0) return BadRequest(new { message = $"{item.ProductName}: quantity can't be negative." });
            if (l.Quantity > item.Returnable)
                return BadRequest(new { message = $"{item.ProductName}: only {item.Returnable} can still be returned ({item.Quantity} sold, {item.Returned} already returned)." });
        }

        // value: the sale's own line prices + this part's share of the sale's order tax and discount
        decimal linesValue = 0;
        var returnItems = new List<SalesReturnItem>();
        var productIds = lines.Select(l => lookup.Items.First(i => i.SaleItemId == l.SaleItemId).ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var l in lines)
        {
            var item = lookup.Items.First(i => i.SaleItemId == l.SaleItemId);
            var saleItem = sale.Items.First(i => i.Id == l.SaleItemId);
            var subtotal = Math.Round(item.UnitPrice * l.Quantity, 2);
            linesValue += subtotal;
            products.TryGetValue(item.ProductId, out var product);
            returnItems.Add(new SalesReturnItem
            {
                ProductId = item.ProductId, SaleItemId = item.SaleItemId, ProductName = item.ProductName,
                NetUnitPrice = item.UnitPrice, Stock = product?.Quantity ?? 0, Quantity = l.Quantity,
                Discount = saleItem.Discount, TaxPercent = saleItem.TaxPercent, Subtotal = subtotal,
            });
        }
        var share = lookup.ItemsTotal > 0 ? linesValue / lookup.ItemsTotal : 0;
        var taxShare = Math.Round(sale.OrderTax * share, 2);
        var discountShare = Math.Round(sale.Discount * share, 2);
        var total = Math.Max(0, Math.Round(linesValue + taxShare - discountShare, 2));
        var refund = Math.Min(Math.Max(0, Math.Round(dto.Refund, 2)), total);

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var entity = new SalesReturn
            {
                Reference = await NextReferenceAsync(),
                SaleId = sale.Id, SaleReference = sale.Reference,
                CustomerId = sale.CustomerId, CustomerName = sale.CustomerName, CustomerImage = sale.CustomerImage,
                ProductId = returnItems[0].ProductId, ProductName = returnItems[0].ProductName,
                OrderTax = taxShare, Discount = discountShare, Shipping = 0,
                GrandTotal = total, Paid = refund, Due = total - refund,
                Status = "Received",
                PaymentStatus = refund <= 0 ? "Unpaid" : refund >= total ? "Paid" : "Partial",
                ReturnDate = dto.ReturnDate ?? DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
                Items = returnItems,
            };
            _db.SalesReturns.Add(entity);
            foreach (var ri in returnItems)                       // the goods are back in stock
                if (ri.ProductId != null && products.TryGetValue(ri.ProductId.Value, out var p)) { p.Quantity += ri.Quantity; p.UpdatedAt = DateTime.UtcNow; }
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { entity.Id, entity.Reference, entity.GrandTotal, entity.Paid, entity.Due });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<SaleLookup> BuildLookupAsync(Sale sale)
    {
        var returns = await _db.SalesReturns.Include(r => r.Items).Where(r => r.SaleId == sale.Id).ToListAsync();
        var returnedBySaleItem = returns.SelectMany(r => r.Items).Where(i => i.SaleItemId != null)
            .GroupBy(i => i.SaleItemId!.Value).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var items = sale.Items.OrderBy(i => i.Id).Select(i =>
        {
            var returned = returnedBySaleItem.TryGetValue(i.Id, out var q) ? q : 0;
            var unit = i.Quantity > 0 ? Math.Round(i.TotalCost / i.Quantity, 2) : i.UnitCost;
            return new ReturnableLine(i.Id, i.ProductId, i.ProductName, i.Quantity, unit, returned, Math.Max(0, i.Quantity - returned));
        }).ToList();
        return new SaleLookup(sale.Id, sale.Reference, sale.CustomerId, sale.CustomerName, sale.SaleDate, sale.GrandTotal, sale.Paid,
            sale.OrderTax, sale.Discount, sale.Items.Sum(i => i.TotalCost), items, returns.Select(r => r.Reference).ToList());
    }

    /// <summary>RET-0001, RET-0002 ...</summary>
    private async Task<string> NextReferenceAsync()
    {
        var refs = await _db.SalesReturns.Where(r => r.Reference.StartsWith("RET-")).Select(r => r.Reference).ToListAsync();
        var max = refs.Select(r => System.Text.RegularExpressions.Regex.Match(r, @"^RET-(\d+)$")).Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        return $"RET-{max + 1:D4}";
    }
}
