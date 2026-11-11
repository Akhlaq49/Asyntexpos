using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

// ── DTOs ──

public class CreatePurchaseReturnItemDto
{
    public int? VariantId { get; set; }
    public int ProductId { get; set; }
    /// <summary>Batch to take stock from. Empty = earliest-expiring stock.</summary>
    public int? BatchId { get; set; }
    /// <summary>Base units.</summary>
    public int Quantity { get; set; }
    /// <summary>Credit per base unit; 0 = the batch cost.</summary>
    public decimal UnitCost { get; set; }
    public string Reason { get; set; } = "Expired";
}

public class CreatePurchaseReturnDto
{
    public int? LocationId { get; set; }
    public int? PurchaseId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? Date { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseReturnItemDto> Items { get; set; } = new();
}

public class CreateSupplierPaymentDto
{
    public string SupplierName { get; set; } = string.Empty;
    /// <summary>Pay one purchase. Empty = settle the supplier's oldest unpaid purchases first.</summary>
    public int? PurchaseId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? PaymentDate { get; set; }
}

// ── Returns to supplier ──

[ApiController]
[Route("api/purchase-returns")]
[Authorize]
public class PurchaseReturnsController : ControllerBase
{
    private readonly ILocationService _locations;
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IAuditService _audit;

    public PurchaseReturnsController(AppDbContext db, IInventoryService inventory, IAuditService audit, ILocationService locations)
    {
        _locations = locations;
        _db = db;
        _inventory = inventory;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? supplier, [FromQuery] string? from, [FromQuery] string? to)
    {
        var q = _db.PurchaseReturns.Include(r => r.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(supplier)) q = q.Where(r => r.SupplierName.Contains(supplier));
        if (DateTime.TryParse(from, out var f)) q = q.Where(r => r.Date >= f.Date);
        if (DateTime.TryParse(to, out var t)) q = q.Where(r => r.Date < t.Date.AddDays(1));
        var list = await q.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var r = await _db.PurchaseReturns.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        return r == null ? NotFound() : Ok(Map(r));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseReturnDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SupplierName)) return BadRequest(new { message = "Choose the supplier." });
        if (dto.Items.Count == 0 || dto.Items.Any(i => i.Quantity <= 0))
            return BadRequest(new { message = "Add at least one item with a quantity above zero." });

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var count = await _db.PurchaseReturns.CountAsync();
            var ret = new PurchaseReturn
            {
                Reference = $"PR{count + 1:D4}",
                PurchaseId = dto.PurchaseId,
                SupplierName = dto.SupplierName.Trim(),
                Date = DateTime.TryParse(dto.Date, out var d) ? d : DateTime.UtcNow,
                Notes = dto.Notes,
                CreatedBy = _audit.CurrentUser,
                LocationId = await _locations.PickAsync(dto.LocationId)
            };
            var at = new StockRef("PurchaseReturn", ret.Reference, null, ret.LocationId, null, "PurchaseReturn");

            foreach (var line in dto.Items)
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId);
                if (product == null) return BadRequest(new { message = $"Product {line.ProductId} was not found." });

                List<BatchAllocation> taken;
                if (line.BatchId.HasValue)
                {
                    var batch = await _db.ProductBatches.FirstOrDefaultAsync(b => b.Id == line.BatchId && b.ProductId == product.Id);
                    if (batch == null) return BadRequest(new { message = $"Batch not found for {product.ProductName}." });
                    if (batch.Quantity < line.Quantity)
                        return BadRequest(new { message = $"Batch {batch.BatchNo} of {product.ProductName} has only {batch.Quantity} in stock." });
                    var one = await _inventory.DeductFromBatchAsync(product, batch.Id, line.Quantity, at with { VariantId = batch.VariantId });
                    taken = one == null ? new() : new() { one };
                }
                else
                {
                    var here = await _inventory.AvailableAsync(product, ret.LocationId, line.VariantId, sellableOnly: false);
                    if (here < line.Quantity)
                        return BadRequest(new { message = $"{product.ProductName} has only {here} in stock at this location." });
                    taken = await _inventory.DeductFefoAsync(product, line.Quantity, at with { VariantId = line.VariantId });
                }

                var batchRow = taken.FirstOrDefault();
                var unitCost = line.UnitCost > 0 ? line.UnitCost : _inventory.AverageCost(taken, product.PurchasePrice);
                ret.Items.Add(new PurchaseReturnItem
                {
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    VariantId = line.VariantId,
                    BatchId = batchRow?.BatchId,
                    BatchNo = string.Join(", ", taken.Select(t => t.BatchNo).Distinct()),
                    ExpiryDate = batchRow?.ExpiryDate,
                    Quantity = line.Quantity,
                    UnitCost = unitCost,
                    Total = Math.Round(unitCost * line.Quantity, 2),
                    Reason = string.IsNullOrWhiteSpace(line.Reason) ? "Other" : line.Reason
                });
            }

            ret.Total = ret.Items.Sum(i => i.Total);
            _db.PurchaseReturns.Add(ret);
            _audit.Log("Created", "PurchaseReturn", ret.Reference, new { ret.SupplierName, ret.Total, items = ret.Items.Count });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(Map(ret));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ret = await _db.PurchaseReturns.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (ret == null) return NotFound();

        using var tx = await _db.Database.BeginTransactionAsync();
        var at = new StockRef("PurchaseReturn", ret.Reference + " deleted", ret.Id, ret.LocationId, null, "PurchaseReturn");
        foreach (var item in ret.Items)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product == null) continue;
            if (item.BatchId.HasValue)
                await _inventory.RestoreAsync(product, new[] { new BatchAllocation(item.BatchId.Value, item.Quantity, item.UnitCost, item.BatchNo ?? "", item.ExpiryDate) }, 0, at with { VariantId = item.VariantId });
            else
                await _inventory.AddStockAsync(product, item.Quantity, "RETURN", null, item.UnitCost, at: at with { VariantId = item.VariantId });
        }
        _audit.Log("Deleted", "PurchaseReturn", ret.Reference, new { ret.SupplierName, ret.Total });
        _db.PurchaseReturns.Remove(ret);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    private static object Map(PurchaseReturn r) => new
    {
        r.Id, r.Reference, r.PurchaseId, r.SupplierName,
        date = r.Date.ToString("yyyy-MM-dd"), r.Total, r.Notes, r.CreatedBy,
        items = r.Items.Select(i => new
        {
            i.Id, i.ProductId, i.ProductName, i.BatchId, i.BatchNo,
            expiryDate = i.ExpiryDate?.ToString("yyyy-MM-dd"), i.Quantity, i.UnitCost, i.Total, i.Reason
        })
    };
}

// ── Supplier payments, ledger and dues ──

[ApiController]
[Route("api/supplier-payments")]
[Authorize]
public class SupplierPaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public SupplierPaymentsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private async Task<int> SupplierIdAsync(string name)
        => await _db.Parties.Where(p => p.Role == "Supplier" && (p.FullName == name || p.CompanyName == name))
            .Select(p => p.Id).FirstOrDefaultAsync();

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? supplier)
    {
        var q = _db.SupplierPayments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(supplier))
        {
            var sid = await SupplierIdAsync(supplier);
            q = q.Where(p => p.SupplierName == supplier || (sid > 0 && p.SupplierId == sid));
        }
        var list = await q.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id).ToListAsync();
        return Ok(list.Select(p => new
        {
            p.Id, p.SupplierName, p.PurchaseId, p.Amount, p.PaymentType, p.Reference, p.Notes,
            paymentDate = p.PaymentDate.ToString("yyyy-MM-dd"), p.CreatedBy
        }));
    }

    /// <summary>Records a payment and applies it to one purchase or to the oldest unpaid purchases first.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierPaymentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SupplierName)) return BadRequest(new { message = "Choose the supplier." });
        if (dto.Amount <= 0) return BadRequest(new { message = "Enter an amount above zero." });

        var date = DateTime.TryParse(dto.PaymentDate, out var d) ? d : DateTime.UtcNow;
        var supplierId = await SupplierIdAsync(dto.SupplierName);
        var purchases = await _db.Purchases
            .Where(p => p.SupplierName == dto.SupplierName && p.Paid < p.Total && (dto.PurchaseId == null || p.Id == dto.PurchaseId))
            .OrderBy(p => p.Date).ThenBy(p => p.Id)
            .ToListAsync();

        var remaining = dto.Amount;
        var applied = new List<object>();
        using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var p in purchases)
        {
            if (remaining <= 0) break;
            var pay = Math.Min(remaining, p.Total - p.Paid);
            p.Paid += pay;
            p.PaymentStatus = p.Paid >= p.Total ? "Paid" : "Partial";
            p.UpdatedAt = DateTime.UtcNow;
            remaining -= pay;
            _db.SupplierPayments.Add(new SupplierPayment
            {
                SupplierId = supplierId, SupplierName = dto.SupplierName, PurchaseId = p.Id, Amount = pay, PaymentType = dto.PaymentType,
                Reference = dto.Reference ?? p.Reference, Notes = dto.Notes, PaymentDate = date, CreatedBy = _audit.CurrentUser
            });
            applied.Add(new { purchaseId = p.Id, p.Reference, amount = pay });
        }

        // Any amount beyond the open purchases stays as an advance against future bills.
        if (remaining > 0)
        {
            _db.SupplierPayments.Add(new SupplierPayment
            {
                SupplierId = supplierId, SupplierName = dto.SupplierName, Amount = remaining, PaymentType = dto.PaymentType,
                Reference = dto.Reference ?? "ADVANCE", Notes = string.IsNullOrWhiteSpace(dto.Notes) ? "Advance" : dto.Notes + " (advance)",
                PaymentDate = date, CreatedBy = _audit.CurrentUser
            });
        }
        _audit.Log("Payment", "Supplier", dto.SupplierName, new { dto.Amount, dto.PaymentType, advance = remaining });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { applied, advance = remaining });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var pay = await _db.SupplierPayments.FirstOrDefaultAsync(p => p.Id == id);
        if (pay == null) return NotFound();
        if (pay.PurchaseId.HasValue)
        {
            var p = await _db.Purchases.FirstOrDefaultAsync(x => x.Id == pay.PurchaseId);
            if (p != null)
            {
                p.Paid = Math.Max(0, p.Paid - pay.Amount);
                p.PaymentStatus = p.Paid <= 0 ? "Unpaid" : p.Paid >= p.Total ? "Paid" : "Partial";
            }
        }
        _audit.Log("Deleted", "SupplierPayment", id, new { pay.SupplierName, pay.Amount });
        _db.SupplierPayments.Remove(pay);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Balance per supplier: purchases − payments − returns.</summary>
    [HttpGet("dues")]
    public async Task<IActionResult> Dues()
    {
        var purchases = await _db.Purchases.GroupBy(p => p.SupplierName)
            .Select(g => new { Supplier = g.Key, Total = g.Sum(x => x.Total), Paid = g.Sum(x => x.Paid), Count = g.Count(), Last = g.Max(x => x.Date) })
            .ToListAsync();
        var advances = await _db.SupplierPayments.Where(p => p.PurchaseId == null && p.SupplierName != null)
            .GroupBy(p => p.SupplierName!).Select(g => new { Supplier = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
        var returns = await _db.PurchaseReturns.GroupBy(r => r.SupplierName)
            .Select(g => new { Supplier = g.Key, Amount = g.Sum(x => x.Total) }).ToListAsync();

        var names = purchases.Select(p => p.Supplier).Union(advances.Select(a => a.Supplier)).Union(returns.Select(r => r.Supplier)).Distinct();
        var rows = names.Select(n =>
        {
            var p = purchases.FirstOrDefault(x => x.Supplier == n);
            var adv = advances.FirstOrDefault(x => x.Supplier == n)?.Amount ?? 0;
            var ret = returns.FirstOrDefault(x => x.Supplier == n)?.Amount ?? 0;
            var total = p?.Total ?? 0;
            var paid = (p?.Paid ?? 0) + adv;
            return new
            {
                supplier = n, purchases = p?.Count ?? 0, totalPurchased = total, paid, returns = ret,
                balance = Math.Round(total - paid - ret, 2), lastPurchase = p?.Last.ToString("yyyy-MM-dd")
            };
        }).OrderByDescending(r => r.balance).ToList();
        return Ok(rows);
    }

    /// <summary>Dated ledger for one supplier with a running balance.</summary>
    [HttpGet("ledger")]
    public async Task<IActionResult> Ledger([FromQuery] string supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier)) return BadRequest(new { message = "Choose the supplier." });

        var entries = new List<(DateTime date, string type, string reference, decimal debit, decimal credit, string? notes)>();
        foreach (var p in await _db.Purchases.Where(p => p.SupplierName == supplier).ToListAsync())
            entries.Add((p.Date, "Purchase", p.Reference, p.Total, 0, p.Notes));
        var sid = await SupplierIdAsync(supplier);
        foreach (var pay in await _db.SupplierPayments.Where(p => p.SupplierName == supplier || (sid > 0 && p.SupplierName == null && p.SupplierId == sid)).ToListAsync())
            entries.Add((pay.PaymentDate, "Payment", string.IsNullOrEmpty(pay.Reference) ? $"PAY-{pay.Id}" : pay.Reference, 0, pay.Amount, $"{pay.PaymentType} {pay.Notes}".Trim()));
        foreach (var r in await _db.PurchaseReturns.Where(r => r.SupplierName == supplier).ToListAsync())
            entries.Add((r.Date, "Return", r.Reference, 0, r.Total, r.Notes));

        decimal running = 0;
        var rows = entries.OrderBy(e => e.date).ThenBy(e => e.type == "Purchase" ? 0 : 1).Select(e =>
        {
            running += e.debit - e.credit;
            return new
            {
                date = e.date.ToString("yyyy-MM-dd"), e.type, e.reference, e.debit, e.credit,
                balance = Math.Round(running, 2), e.notes
            };
        }).ToList();
        return Ok(new { supplier, balance = Math.Round(running, 2), entries = rows });
    }
}
