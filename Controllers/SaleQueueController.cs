using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

/// <summary>
/// The sale queue: quotations converted to an order that waits for its goods.
/// Per product: what the customer paid, whether it is on a purchase order, and whether it has arrived.
/// </summary>
[Authorize]
[ApiController]
[Route("api/sales/queue")]
public class SaleQueueController : ControllerBase
{
    private readonly AppDbContext _db;
    public SaleQueueController(AppDbContext db) => _db = db;

    public record QueueLine(int ProductId, string ProductName, int Needed, int Ordered, int Received, int InStock, string Stage);
    public record QueuePo(int Id, string PoNumber, string Status, string SupplierName);
    public record QueueOrder(int SaleId, string Reference, int? QuotationId, string? QuotationReference, string CustomerName, DateTime QueuedAt,
        decimal GrandTotal, decimal Paid, decimal Due, string PaymentStatus, string Stage, List<QueueLine> Lines, List<QueuePo> PurchaseOrders);
    public record SoldOrder(int SaleId, string Reference, string? QuotationReference, string CustomerName, DateTime SaleDate, decimal GrandTotal, decimal Paid, decimal Due);

    // stages, per product line and per order
    public const string NotOrdered = "NotOrdered", Waiting = "Waiting", Ready = "Ready";

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var queued = await _db.Sales.Include(s => s.Items).Where(s => s.Status == SaleStatus.Queued).OrderBy(s => s.CreatedAt).ToListAsync();
        var quotations = await _db.Quotations.Where(q => q.SaleId != null).Select(q => new { q.Id, q.Reference, SaleId = q.SaleId!.Value }).ToListAsync();
        var quotationBySale = quotations.GroupBy(q => q.SaleId).ToDictionary(g => g.Key, g => g.First());
        var quotationIds = queued.Where(s => quotationBySale.ContainsKey(s.Id)).Select(s => quotationBySale[s.Id].Id).ToList();
        var orders = await _db.PurchaseOrders.Include(o => o.Items).Where(o => o.QuotationId != null && quotationIds.Contains(o.QuotationId.Value)).ToListAsync();
        var productIds = queued.SelectMany(s => s.Items.Select(i => i.ProductId)).Distinct().ToList();
        var stock = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Quantity);

        var result = queued.Select(s =>
        {
            quotationBySale.TryGetValue(s.Id, out var q);
            var pos = q == null ? new List<PurchaseOrder>() : orders.Where(o => o.QuotationId == q.Id).OrderBy(o => o.Id).ToList();
            var lines = s.Items.GroupBy(i => i.ProductId).Select(g =>
            {
                var needed = g.Sum(i => i.Quantity);
                // a cancelled purchase order only counts what it delivered
                var ordered = pos.SelectMany(o => o.Items.Where(i => i.ProductId == g.Key)
                    .Select(i => o.Status == PurchaseOrderStatus.Cancelled ? i.ReceivedQuantity : i.Quantity)).Sum();
                var received = pos.SelectMany(o => o.Items.Where(i => i.ProductId == g.Key).Select(i => i.ReceivedQuantity)).Sum();
                var inStock = stock.TryGetValue(g.Key, out var st) ? st : 0;
                var stage = inStock >= needed ? Ready : ordered > 0 ? Waiting : NotOrdered;
                return new QueueLine(g.Key, g.First().ProductName, needed, ordered, received, inStock, stage);
            }).ToList();
            var orderStage = lines.All(l => l.Stage == Ready) ? Ready : lines.Any(l => l.Stage == NotOrdered) ? NotOrdered : Waiting;
            return new QueueOrder(s.Id, s.Reference, q?.Id, q?.Reference, s.CustomerName, s.CreatedAt, s.GrandTotal, s.Paid, s.Due,
                s.PaymentStatus, orderStage, lines, pos.Select(o => new QueuePo(o.Id, o.PoNumber, o.Status, o.SupplierName)).ToList());
        }).ToList();

        // sold from the queue: sales made from a quotation, most recent first
        var fromQuotation = quotationBySale.Keys.ToList();
        var sold = await _db.Sales.Where(s => fromQuotation.Contains(s.Id) && s.Status != SaleStatus.Queued && s.Status != "Cancelled")
            .OrderByDescending(s => s.SaleDate).Take(100)
            .Select(s => new { s.Id, s.Reference, s.CustomerName, s.SaleDate, s.GrandTotal, s.Paid, s.Due }).ToListAsync();

        return Ok(new
        {
            queue = result,
            sold = sold.Select(s => new SoldOrder(s.Id, s.Reference, quotationBySale.TryGetValue(s.Id, out var q) ? q.Reference : null,
                s.CustomerName, s.SaleDate, s.GrandTotal, s.Paid, s.Due)).ToList(),
        });
    }
}
