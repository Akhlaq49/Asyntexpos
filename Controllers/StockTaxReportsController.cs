using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;

namespace ReactPosApi.Controllers;

/// <summary>Stock History, Sold Stock and Tax reports (dates: yyyy-MM-dd, both inclusive, optional)</summary>
[Authorize]
[ApiController]
[Route("api/reports")]
public class StockTaxReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StockTaxReportsController(AppDbContext db) => _db = db;

    private static (DateTime start, DateTime end) Range(DateTime? from, DateTime? to) =>
        (from?.Date ?? DateTime.MinValue, to.HasValue ? to.Value.Date.AddDays(1) : DateTime.MaxValue);

    public record StockMovement(DateTime Date, int ProductId, string ProductName, string? Sku, string Type, string? Reference, string? Party, decimal QtyIn, decimal QtyOut);
    public record StockProduct(int Id, string Name, string? Sku, int CurrentStock);

    /// <summary>Every stock movement: opening stock, purchases / goods received, stock added (Manage Stock), sales</summary>
    [HttpGet("stock-history")]
    public async Task<IActionResult> StockHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? productId)
    {
        var (start, end) = Range(from, to);
        var products = await _db.Products.Select(p => new StockProduct(p.Id, p.ProductName, p.SKU, p.Quantity)).ToListAsync();
        var names = products.ToDictionary(p => p.Id);
        var rows = new List<StockMovement>();

        // purchases linked to a product (opening stock, purchases, purchase order receipts)
        var purchaseLines = await (from i in _db.PurchaseItems
                                   join p in _db.Purchases on i.PurchaseId equals p.Id
                                   where i.ProductId != null && p.Date >= start && p.Date < end && (productId == null || i.ProductId == productId)
                                   select new { p.Date, ProductId = i.ProductId!.Value, i.ProductName, i.Quantity, p.Reference, p.SupplierName, p.PurchaseOrderId })
                                  .ToListAsync();
        rows.AddRange(purchaseLines.Select(l => new StockMovement(l.Date, l.ProductId, names.TryGetValue(l.ProductId, out var n) ? n.Name : l.ProductName,
            names.TryGetValue(l.ProductId, out var n2) ? n2.Sku : null,
            l.SupplierName == "Opening Stock" ? "Opening stock" : l.PurchaseOrderId != null ? "Goods received" : "Purchase",
            l.Reference, l.SupplierName == "Opening Stock" ? null : l.SupplierName, l.Quantity, 0)));

        // stock added from Manage Stock
        var entries = await _db.StockEntries.Where(e => e.Date >= start && e.Date < end && (productId == null || e.ProductId == productId)).ToListAsync();
        rows.AddRange(entries.Select(e => new StockMovement(e.Date, e.ProductId, names.TryGetValue(e.ProductId, out var n) ? n.Name : $"#{e.ProductId}",
            names.TryGetValue(e.ProductId, out var n2) ? n2.Sku : null, "Stock added", $"STOCK-{e.Id}", e.Person, e.Quantity, 0)));

        // sales
        var saleLines = await (from i in _db.SaleItems
                               join s in _db.Sales on i.SaleId equals s.Id
                               where s.SaleDate >= start && s.SaleDate < end && (productId == null || i.ProductId == productId)
                               select new { s.SaleDate, i.ProductId, i.ProductName, i.Quantity, s.Reference, s.CustomerName })
                              .ToListAsync();
        rows.AddRange(saleLines.Select(l => new StockMovement(l.SaleDate, l.ProductId, names.TryGetValue(l.ProductId, out var n) ? n.Name : l.ProductName,
            names.TryGetValue(l.ProductId, out var n2) ? n2.Sku : null, "Sale", l.Reference, l.CustomerName, 0, l.Quantity)));

        // goods returned by customers (returns of a sale put them back into stock)
        var returnLines = await (from i in _db.SalesReturnItems
                                 join r in _db.SalesReturns on i.SalesReturnId equals r.Id
                                 where i.ProductId != null && r.ReturnDate >= start && r.ReturnDate < end && (productId == null || i.ProductId == productId)
                                 select new { r.ReturnDate, ProductId = i.ProductId!.Value, i.ProductName, i.Quantity, r.Reference, r.CustomerName })
                                .ToListAsync();
        rows.AddRange(returnLines.Select(l => new StockMovement(l.ReturnDate, l.ProductId, names.TryGetValue(l.ProductId, out var n) ? n.Name : l.ProductName,
            names.TryGetValue(l.ProductId, out var n2) ? n2.Sku : null, "Sale return", l.Reference, l.CustomerName, l.Quantity, 0)));

        // goods sent back to suppliers
        var prLines = await (from i in _db.PurchaseReturnItems
                             join r in _db.PurchaseReturns on i.PurchaseReturnId equals r.Id
                             where i.ProductId != null && r.ReturnDate >= start && r.ReturnDate < end && (productId == null || i.ProductId == productId)
                             select new { r.ReturnDate, ProductId = i.ProductId!.Value, i.ProductName, i.Quantity, r.Reference, r.SupplierName })
                            .ToListAsync();
        rows.AddRange(prLines.Select(l => new StockMovement(l.ReturnDate, l.ProductId, names.TryGetValue(l.ProductId, out var n) ? n.Name : l.ProductName,
            names.TryGetValue(l.ProductId, out var n2) ? n2.Sku : null, "Purchase return", l.Reference, l.SupplierName, 0, l.Quantity)));

        return Ok(new
        {
            movements = rows.OrderByDescending(r => r.Date).ThenBy(r => r.ProductName).ToList(),
            products = productId == null ? products : products.Where(p => p.Id == productId).ToList(),
        });
    }

    public record SoldLine(DateTime Date, string SaleReference, string? Customer, int ProductId, string ProductName, string? Sku, int Quantity, decimal UnitPrice, decimal Discount, decimal Total);

    /// <summary>Every product line sold, newest first</summary>
    [HttpGet("sold-stock")]
    public async Task<IActionResult> SoldStock([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (start, end) = Range(from, to);
        var skus = await _db.Products.ToDictionaryAsync(p => p.Id, p => p.SKU);
        var lines = await (from i in _db.SaleItems
                           join s in _db.Sales on i.SaleId equals s.Id
                           where s.SaleDate >= start && s.SaleDate < end
                           orderby s.SaleDate descending, s.Id descending
                           select new { s.SaleDate, s.Reference, s.CustomerName, i.ProductId, i.ProductName, i.Quantity, i.PurchasePrice, i.Discount, i.TotalCost })
                          .ToListAsync();
        return Ok(lines.Select(l => new SoldLine(l.SaleDate, l.Reference, l.CustomerName, l.ProductId, l.ProductName,
            skus.TryGetValue(l.ProductId, out var sku) ? sku : null, l.Quantity, l.PurchasePrice, l.Discount, l.TotalCost)).ToList());
    }

    public record TaxLine(DateTime Date, string Direction, string Document, string? Party, string TaxName, decimal? Rate, decimal Amount);

    /// <summary>
    /// Tax collected on sales (output) and paid on purchases (input), one line per tax.
    /// Sales: the taxes applied (from a converted quotation), otherwise the order tax amount, plus any per-line tax.
    /// Purchases: the order's taxes in proportion to what each receipt received, or the purchase's own tax.
    /// </summary>
    [HttpGet("tax-report")]
    public async Task<IActionResult> TaxReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (start, end) = Range(from, to);
        var rows = new List<TaxLine>();

        var sales = await _db.Sales.Include(s => s.Items).Where(s => s.SaleDate >= start && s.SaleDate < end).ToListAsync();
        foreach (var s in sales)
        {
            var taxes = AppliedTaxes.Parse(s.TaxesJson);
            if (taxes.Count > 0) rows.AddRange(taxes.Select(t => new TaxLine(s.SaleDate, "Collected", s.Reference, s.CustomerName, t.TaxName, t.Percent, t.Amount)));
            else if (s.OrderTax > 0) rows.Add(new TaxLine(s.SaleDate, "Collected", s.Reference, s.CustomerName, "Order tax", null, s.OrderTax));
            foreach (var g in s.Items.Where(i => i.TaxAmount > 0).GroupBy(i => i.TaxPercent))
                rows.Add(new TaxLine(s.SaleDate, "Collected", s.Reference, s.CustomerName, "Item tax", g.Key, Math.Round(g.Sum(i => i.TaxAmount * i.Quantity), 2)));
        }

        var purchases = await _db.Purchases.Include(p => p.Items).Where(p => p.Date >= start && p.Date < end).ToListAsync();
        var poIds = purchases.Where(p => p.PurchaseOrderId != null).Select(p => p.PurchaseOrderId!.Value).Distinct().ToList();
        var orders = await _db.PurchaseOrders.Where(o => poIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
        foreach (var p in purchases)
        {
            var itemTax = p.Items?.Where(i => i.TaxAmount > 0).ToList() ?? new();
            if (p.PurchaseOrderId != null && orders.TryGetValue(p.PurchaseOrderId.Value, out var po) && po.SubTotal > 0)
            {
                // this receipt's share of the order's taxes (by value received)
                var share = (p.Items?.Sum(i => i.TotalCost) ?? 0) / po.SubTotal;
                var taxes = AppliedTaxes.Parse(po.TaxesJson);
                if (taxes.Count > 0) rows.AddRange(taxes.Select(t => new TaxLine(p.Date, "Paid", p.Reference, p.SupplierName, t.TaxName, t.Percent, Math.Round(t.Amount * share, 2))));
                else if (po.OrderTax > 0) rows.Add(new TaxLine(p.Date, "Paid", p.Reference, p.SupplierName, "Order tax", null, Math.Round(po.OrderTax * share, 2)));
            }
            else if (p.OrderTax > 0) rows.Add(new TaxLine(p.Date, "Paid", p.Reference, p.SupplierName, "Order tax", null, p.OrderTax));
            foreach (var g in itemTax.GroupBy(i => i.TaxPercentage))
                rows.Add(new TaxLine(p.Date, "Paid", p.Reference, p.SupplierName, "Item tax", g.Key, Math.Round(g.Sum(i => i.TaxAmount), 2)));
        }

        var ordered = rows.Where(r => r.Amount != 0).OrderByDescending(r => r.Date).ToList();
        var collected = ordered.Where(r => r.Direction == "Collected").Sum(r => r.Amount);
        var paid = ordered.Where(r => r.Direction == "Paid").Sum(r => r.Amount);
        var byTax = ordered.GroupBy(r => r.TaxName).Select(g => new
        {
            taxName = g.Key,
            collected = g.Where(r => r.Direction == "Collected").Sum(r => r.Amount),
            paid = g.Where(r => r.Direction == "Paid").Sum(r => r.Amount),
        }).OrderBy(x => x.taxName).ToList();
        return Ok(new { collected, paid, net = collected - paid, byTax, lines = ordered });
    }
}
