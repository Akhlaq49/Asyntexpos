using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Purchase returns: send (part of) a received purchase back to the supplier.
/// The goods leave stock; the value is deducted from what is still owed on the purchase (credit)
/// and/or paid back by the supplier (refund, now or later).
/// </summary>
[Authorize]
[ApiController]
[Route("api/purchase-returns")]
public class PurchaseReturnsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PurchaseReturnsController(AppDbContext db) => _db = db;

    public record ReturnableLine(int PurchaseItemId, int? ProductId, string ProductName, int Quantity, decimal UnitCost, int Returned, int? InStock, int Returnable);
    public record PurchaseLookup(int PurchaseId, string Reference, int? PurchaseOrderId, string SupplierName, DateTime Date, decimal Total, decimal Paid, decimal Due,
        decimal ItemsTotal, decimal ExtrasRate, List<ReturnableLine> Items, List<string> PreviousReturns);
    public record ReturnLineDto(int PurchaseItemId, int Quantity);
    public record CreateReturnDto(int PurchaseId, List<ReturnLineDto> Lines, DateTime? ReturnDate, decimal Credit, decimal Refund, string? RefundMethod, string? Notes);
    public record RefundDto(decimal Amount, string? Method, DateTime? Date, string? Reference, string? Notes);

    public record ReturnItemDto(int Id, int PurchaseItemId, int? ProductId, string ProductName, int Quantity, decimal UnitCost, decimal Total);
    public record ReturnRefundDto(int Id, decimal Amount, string Method, string? Reference, DateTime Date, string? Notes, string? CreatedBy);
    public record ReturnDto(int Id, string Reference, int PurchaseId, string PurchaseReference, int? PurchaseOrderId, string SupplierName, DateTime ReturnDate,
        decimal Total, decimal Credited, decimal Refunded, decimal Due, string Status, string? Notes, string? CreatedBy, DateTime CreatedAt,
        List<ReturnItemDto> Items, List<ReturnRefundDto> Refunds);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.PurchaseReturns.Include(r => r.Items).Include(r => r.Refunds)
            .OrderByDescending(r => r.ReturnDate).ThenByDescending(r => r.Id).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var r = await _db.PurchaseReturns.Include(x => x.Items).Include(x => x.Refunds).FirstOrDefaultAsync(x => x.Id == id);
        return r == null ? NotFound(new { message = "This return no longer exists." }) : Ok(ToDto(r));
    }

    /// <summary>A received purchase with each product's received / already returned / in stock / returnable quantity</summary>
    [HttpGet("purchase-lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string reference)
    {
        var r = (reference ?? "").Trim();
        if (r.Length == 0) return BadRequest(new { message = "Enter the purchase reference." });
        var purchase = await _db.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.Reference == r);
        if (purchase == null) return NotFound(new { message = $"No purchase with reference {r} was found." });
        if (purchase.Status != "Received") return BadRequest(new { message = $"{purchase.Reference} is {purchase.Status}, not received - only received goods can be returned." });
        return Ok(await BuildLookupAsync(purchase));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnDto dto)
    {
        var purchase = await _db.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == dto.PurchaseId);
        if (purchase == null) return NotFound(new { message = "The purchase no longer exists." });
        if (purchase.Status != "Received") return BadRequest(new { message = $"{purchase.Reference} is not received - only received goods can be returned." });
        var lookup = await BuildLookupAsync(purchase);

        var lines = (dto.Lines ?? new()).Where(l => l.Quantity != 0).ToList();
        if (lines.Count == 0) return BadRequest(new { message = "Enter the quantity going back for at least one product." });
        if (lines.Select(l => l.PurchaseItemId).Distinct().Count() != lines.Count) return BadRequest(new { message = "Each product can only be listed once." });
        foreach (var l in lines)
        {
            var item = lookup.Items.FirstOrDefault(i => i.PurchaseItemId == l.PurchaseItemId);
            if (item == null) return BadRequest(new { message = "One of the lines doesn't belong to this purchase." });
            if (l.Quantity < 0) return BadRequest(new { message = $"{item.ProductName}: quantity can't be negative." });
            if (l.Quantity > item.Returnable)
                return BadRequest(new
                {
                    message = item.InStock != null && item.InStock < item.Quantity - item.Returned
                        ? $"{item.ProductName}: only {item.Returnable} can go back ({item.InStock} in stock)."
                        : $"{item.ProductName}: only {item.Returnable} can still be returned ({item.Quantity} received, {item.Returned} already returned).",
                });
        }

        // value: the purchase's own unit cost + this part's share of its tax and discount (shipping is not returned)
        var items = lines.Select(l =>
        {
            var item = lookup.Items.First(i => i.PurchaseItemId == l.PurchaseItemId);
            return new PurchaseReturnItem
            {
                PurchaseItemId = item.PurchaseItemId, ProductId = item.ProductId, ProductName = item.ProductName,
                Quantity = l.Quantity, UnitCost = item.UnitCost, Total = Math.Round(item.UnitCost * l.Quantity, 2),
            };
        }).ToList();
        var linesValue = items.Sum(i => i.Total);
        var total = Math.Max(0, Math.Round(linesValue * (1 + lookup.ExtrasRate), 2));

        var credit = Math.Round(Math.Max(0, dto.Credit), 2);
        var refund = Math.Round(Math.Max(0, dto.Refund), 2);
        var maxCredit = Math.Min(total, Math.Max(0, lookup.Due));
        if (credit > maxCredit)
            return BadRequest(new { message = maxCredit <= 0 ? $"Nothing is owed on {purchase.Reference}, so nothing can be deducted from it." : $"At most Rs {maxCredit:N2} can be deducted from what is owed on {purchase.Reference}." });
        if (credit + refund > total)
            return BadRequest(new { message = $"Deducted + refunded can't be more than the value returned (Rs {total:N2})." });

        var user = User.FindFirstValue(ClaimTypes.Name);
        var date = dto.ReturnDate ?? DateTime.UtcNow;
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var entity = new PurchaseReturn
            {
                Reference = await NextReferenceAsync(),
                PurchaseId = purchase.Id, PurchaseReference = purchase.Reference, PurchaseOrderId = purchase.PurchaseOrderId,
                SupplierName = purchase.SupplierName, ReturnDate = date,
                Total = total, Credited = credit, Refunded = refund,
                Notes = Clip(dto.Notes, 500), CreatedBy = user, CreatedAt = DateTime.UtcNow,
                Items = items,
            };
            if (refund > 0)
                entity.Refunds.Add(new PurchaseReturnRefund
                {
                    Amount = refund, Method = Clip(dto.RefundMethod, 30) ?? "Cash", Date = date,
                    Notes = "Refunded when the goods were returned", CreatedBy = user, CreatedAt = DateTime.UtcNow,
                });
            _db.PurchaseReturns.Add(entity);

            // the goods leave stock
            var productIds = items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).Distinct().ToList();
            var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            foreach (var i in items)
                if (i.ProductId != null && products.TryGetValue(i.ProductId.Value, out var p)) { p.Quantity -= i.Quantity; p.UpdatedAt = DateTime.UtcNow; }

            // the credit lowers what is owed on the purchase
            if (credit > 0)
            {
                _db.PurchasePayments.Add(new PurchasePayment
                {
                    PurchaseId = purchase.Id, PurchaseOrderId = purchase.PurchaseOrderId, Type = PurchasePaymentType.Payment, Amount = credit,
                    Method = PurchasePaymentRules.ReturnCredit, Reference = entity.Reference, Date = date,
                    Notes = $"Goods returned ({entity.Reference}) - deducted from the amount owed", CreatedBy = user, CreatedAt = DateTime.UtcNow,
                });
                await _db.SaveChangesAsync();
                await RecalculatePaidAsync(purchase);
            }
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(ToDto(entity));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>Money the supplier pays back later</summary>
    [HttpPost("{id:int}/refunds")]
    public async Task<IActionResult> AddRefund(int id, [FromBody] RefundDto dto)
    {
        var r = await _db.PurchaseReturns.Include(x => x.Items).Include(x => x.Refunds).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound(new { message = "This return no longer exists." });
        var amount = Math.Round(dto.Amount, 2);
        var due = r.Total - r.Credited - r.Refunded;
        if (amount <= 0) return BadRequest(new { message = "Enter an amount greater than 0." });
        if (amount > due) return BadRequest(new { message = due <= 0 ? $"{r.Reference} is already settled." : $"Only Rs {due:N2} is still to come back on {r.Reference}." });
        r.Refunds.Add(new PurchaseReturnRefund
        {
            Amount = amount, Method = Clip(dto.Method, 30) ?? "Cash", Reference = Clip(dto.Reference, 60), Date = dto.Date ?? DateTime.UtcNow,
            Notes = Clip(dto.Notes, 500), CreatedBy = User.FindFirstValue(ClaimTypes.Name), CreatedAt = DateTime.UtcNow,
        });
        r.Refunded += amount;
        await _db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    /// <summary>Undo a return: the goods go back into stock and its credit on the purchase is removed</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _db.PurchaseReturns.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound(new { message = "This return no longer exists." });
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var productIds = r.Items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).Distinct().ToList();
            var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            foreach (var i in r.Items)
                if (i.ProductId != null && products.TryGetValue(i.ProductId.Value, out var p)) { p.Quantity += i.Quantity; p.UpdatedAt = DateTime.UtcNow; }

            var credits = await _db.PurchasePayments
                .Where(p => p.PurchaseId == r.PurchaseId && p.Method == PurchasePaymentRules.ReturnCredit && p.Reference == r.Reference).ToListAsync();
            _db.PurchasePayments.RemoveRange(credits);
            _db.PurchaseReturns.Remove(r);
            await _db.SaveChangesAsync();
            var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.Id == r.PurchaseId);
            if (purchase != null && credits.Count > 0) { await RecalculatePaidAsync(purchase); await _db.SaveChangesAsync(); }
            await transaction.CommitAsync();
            return NoContent();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task RecalculatePaidAsync(Purchase purchase)
    {
        var records = await _db.PurchasePayments.Where(p => p.PurchaseId == purchase.Id).ToListAsync();
        purchase.Paid = PurchasePaymentRules.NetPaid(records);
        purchase.PaymentStatus = PurchasePaymentRules.Status(purchase.Paid, purchase.Total);
        purchase.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<PurchaseLookup> BuildLookupAsync(Purchase purchase)
    {
        var returns = await _db.PurchaseReturns.Include(r => r.Items).Where(r => r.PurchaseId == purchase.Id).ToListAsync();
        var returned = returns.SelectMany(r => r.Items).GroupBy(i => i.PurchaseItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var productIds = purchase.Items.Where(i => i.ProductId != null).Select(i => i.ProductId!.Value).Distinct().ToList();
        var stock = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Quantity);

        var items = purchase.Items.OrderBy(i => i.Id).Select(i =>
        {
            var qty = (int)Math.Round(i.Quantity);
            var back = returned.TryGetValue(i.Id, out var q) ? q : 0;
            int? inStock = i.ProductId != null && stock.TryGetValue(i.ProductId.Value, out var s) ? s : null;
            var left = Math.Max(0, qty - back);
            var returnable = inStock == null ? left : Math.Max(0, Math.Min(left, inStock.Value));
            var unit = i.Quantity > 0 ? Math.Round(i.TotalCost / i.Quantity, 2) : i.UnitCost;
            return new ReturnableLine(i.Id, i.ProductId, i.ProductName, qty, unit, back, inStock, returnable);
        }).ToList();

        // order-level tax and discount, as a rate on the item value (shipping is not part of a return)
        var itemsTotal = purchase.Items.Sum(i => i.TotalCost);
        decimal rate = 0;
        if (purchase.PurchaseOrderId != null)
        {
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == purchase.PurchaseOrderId);
            if (po != null && po.SubTotal > 0) rate = (po.OrderTax - po.Discount) / po.SubTotal;
        }
        else if (itemsTotal > 0) rate = (purchase.OrderTax - purchase.Discount) / itemsTotal;

        return new PurchaseLookup(purchase.Id, purchase.Reference, purchase.PurchaseOrderId, purchase.SupplierName, purchase.Date,
            purchase.Total, purchase.Paid, Math.Round(purchase.Total - purchase.Paid, 2), itemsTotal, Math.Round(rate, 6), items,
            returns.Select(r => r.Reference).ToList());
    }

    /// <summary>PR-0001, PR-0002 ...</summary>
    private async Task<string> NextReferenceAsync()
    {
        var refs = await _db.PurchaseReturns.Where(r => r.Reference.StartsWith("PR-")).Select(r => r.Reference).ToListAsync();
        var max = refs.Select(r => Regex.Match(r, @"^PR-(\d+)$")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        return $"PR-{max + 1:D4}";
    }

    private static string? Clip(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    private static ReturnDto ToDto(PurchaseReturn r)
    {
        var due = Math.Max(0, r.Total - r.Credited - r.Refunded);
        var status = due <= 0 ? "Settled" : r.Credited + r.Refunded > 0 ? "Partly settled" : "Refund pending";
        return new ReturnDto(r.Id, r.Reference, r.PurchaseId, r.PurchaseReference, r.PurchaseOrderId, r.SupplierName, r.ReturnDate,
            r.Total, r.Credited, r.Refunded, due, status, r.Notes, r.CreatedBy, r.CreatedAt,
            r.Items.OrderBy(i => i.Id).Select(i => new ReturnItemDto(i.Id, i.PurchaseItemId, i.ProductId, i.ProductName, i.Quantity, i.UnitCost, i.Total)).ToList(),
            r.Refunds.OrderBy(f => f.Date).ThenBy(f => f.Id).Select(f => new ReturnRefundDto(f.Id, f.Amount, f.Method, f.Reference, f.Date, f.Notes, f.CreatedBy)).ToList());
    }
}
