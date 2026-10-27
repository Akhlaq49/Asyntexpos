using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

/// <summary>
/// Buying the goods for a converted quotation: per product the quantity sold, what is in stock,
/// what is already on purchase orders raised for this quotation, a suggested quantity and the last cost.
/// </summary>
[Authorize]
[ApiController]
[Route("api/quotations")]
public class QuotationPurchasePlanController : ControllerBase
{
    private readonly AppDbContext _db;
    public QuotationPurchasePlanController(AppDbContext db) => _db = db;

    public record PlanLine(int ProductId, string ProductName, string? Sku, int Quoted, int InStock, int AlreadyOrdered, int Suggested, decimal LastCost, decimal Price);
    public record LinkedOrder(int Id, string PoNumber, string Status, string SupplierName, decimal Total);

    [HttpGet("{id:int}/purchase-plan")]
    public async Task<IActionResult> Plan(int id)
    {
        var q = await _db.Quotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return NotFound(new { message = "This quotation no longer exists." });
        if (q.SaleId == null) return BadRequest(new { message = $"{q.Reference} hasn't been converted to a sale yet - convert it first." });

        // purchase orders already raised for this quotation (a cancelled one only counts what it delivered)
        var orders = await _db.PurchaseOrders.Include(o => o.Items).Where(o => o.QuotationId == q.Id).OrderBy(o => o.Id).ToListAsync();
        var ordered = orders.SelectMany(o => o.Items.Select(i => new { i.ProductId, Qty = o.Status == PurchaseOrderStatus.Cancelled ? i.ReceivedQuantity : i.Quantity }))
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        var productIds = q.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        // last cost: the latest purchase order line, else the latest received purchase line
        var poCosts = (await _db.PurchaseOrderItems.Where(i => productIds.Contains(i.ProductId)).OrderByDescending(i => i.Id)
            .Select(i => new { i.ProductId, i.UnitCost }).ToListAsync()).GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.First().UnitCost);
        var purchaseCosts = (await (from i in _db.PurchaseItems join p in _db.Purchases on i.PurchaseId equals p.Id
                                    where i.ProductId != null && productIds.Contains(i.ProductId.Value) && p.Status == "Received" && i.Quantity > 0
                                    orderby p.Date descending, i.Id descending
                                    select new { ProductId = i.ProductId!.Value, Cost = i.TotalCost / i.Quantity }).ToListAsync())
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => Math.Round(g.First().Cost, 2));

        var lines = q.Items.GroupBy(i => i.ProductId).Select(g =>
        {
            products.TryGetValue(g.Key, out var p);
            var quoted = g.Sum(i => i.Quantity);
            var already = ordered.TryGetValue(g.Key, out var o) ? o : 0;
            var cost = poCosts.TryGetValue(g.Key, out var c1) ? c1 : purchaseCosts.TryGetValue(g.Key, out var c2) ? c2 : 0;
            // Price = the price set on the product in inventory (used as the purchase order cost)
            return new PlanLine(g.Key, p?.ProductName ?? g.First().ProductName, p?.SKU, quoted, p?.Quantity ?? 0, already, Math.Max(0, quoted - already), cost, p?.Price ?? 0);
        }).ToList();

        return Ok(new
        {
            quotationId = q.Id, reference = q.Reference, saleReference = q.SaleReference, customerName = q.CustomerName,
            lines,
            purchaseOrders = orders.Select(o => new LinkedOrder(o.Id, o.PoNumber, o.Status, o.SupplierName, o.Total)).ToList(),
            missingProducts = lines.Where(l => !products.ContainsKey(l.ProductId)).Select(l => l.ProductName).ToList(),
        });
    }
}
