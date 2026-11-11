using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>Pharmacy reports: expiry, slow-moving stock, credit ageing, margins, daily sales, compliance, branches.</summary>
[ApiController]
[Route("api/pharmacy-reports")]
[Authorize]
public class PharmacyReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPharmacySettingsService _settings;

    public PharmacyReportsController(AppDbContext db, IPharmacySettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    private static (DateTime from, DateTime to) Range(string? from, string? to, int defaultDays = 30)
    {
        var t = DateTime.TryParse(to, out var tt) ? tt.Date.AddDays(1) : DateTime.UtcNow.Date.AddDays(1);
        var f = DateTime.TryParse(from, out var ff) ? ff.Date : t.AddDays(-defaultDays);
        return (f, t);
    }

    /// <summary>Stock value in each expiry bucket, for the dashboard alert.</summary>
    [HttpGet("expiry-summary")]
    public async Task<IActionResult> ExpirySummary()
    {
        var s = await _settings.GetAsync();
        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(s.ExpiryAlertDays1);
        var batches = await _db.ProductBatches.Include(b => b.Product)
            .Where(b => b.Quantity > 0 && b.ExpiryDate != null && b.ExpiryDate <= until)
            .ToListAsync();

        object Bucket(string name, Func<int, bool> match)
        {
            var rows = batches.Where(b => match((int)(b.ExpiryDate!.Value.Date - today).TotalDays)).ToList();
            return new
            {
                name,
                batches = rows.Count,
                products = rows.Select(r => r.ProductId).Distinct().Count(),
                units = rows.Sum(r => r.Quantity),
                costValue = Math.Round(rows.Sum(r => r.Quantity * (r.PurchasePrice > 0 ? r.PurchasePrice : r.Product!.PurchasePrice)), 2),
                retailValue = Math.Round(rows.Sum(r => r.Quantity * r.Product!.Price), 2)
            };
        }

        return Ok(new
        {
            alertDays = new[] { s.ExpiryAlertDays1, s.ExpiryAlertDays2, s.ExpiryAlertDays3 },
            buckets = new[]
            {
                Bucket("Expired", d => d < 0),
                Bucket($"Within {s.ExpiryAlertDays3} days", d => d >= 0 && d <= s.ExpiryAlertDays3),
                Bucket($"{s.ExpiryAlertDays3 + 1}–{s.ExpiryAlertDays2} days", d => d > s.ExpiryAlertDays3 && d <= s.ExpiryAlertDays2),
                Bucket($"{s.ExpiryAlertDays2 + 1}–{s.ExpiryAlertDays1} days", d => d > s.ExpiryAlertDays2 && d <= s.ExpiryAlertDays1)
            }
        });
    }

    /// <summary>Items in stock with no sale in the last N days.</summary>
    [HttpGet("dead-stock")]
    public async Task<IActionResult> DeadStock([FromQuery] int days = 90)
    {
        var since = DateTime.UtcNow.Date.AddDays(-Math.Max(1, days));
        var lastSale = await _db.SaleItems.Join(_db.Sales, i => i.SaleId, s => s.Id, (i, s) => new { i.ProductId, s.SaleDate })
            .GroupBy(x => x.ProductId).Select(g => new { ProductId = g.Key, Last = g.Max(x => x.SaleDate) })
            .ToListAsync();
        var products = await _db.Products.Where(p => p.Quantity > 0).ToListAsync();

        var rows = products
            .Select(p => new { p, last = lastSale.FirstOrDefault(l => l.ProductId == p.Id)?.Last })
            .Where(x => x.last == null || x.last < since)
            .Select(x => new
            {
                productId = x.p.Id, productName = x.p.ProductName, genericName = x.p.GenericName ?? "",
                category = x.p.Category ?? "", manufacturer = x.p.Manufacturer ?? "", stock = x.p.Quantity,
                lastSold = x.last?.ToString("yyyy-MM-dd"),
                daysSinceSale = x.last == null ? (int?)null : (int)(DateTime.UtcNow.Date - x.last.Value.Date).TotalDays,
                stockValue = Math.Round(x.p.Quantity * x.p.PurchasePrice, 2),
                nearestExpiry = x.p.ExpiryDate
            })
            .OrderByDescending(r => r.stockValue)
            .ToList();
        return Ok(new { days, totalValue = rows.Sum(r => r.stockValue), items = rows });
    }

    /// <summary>Unpaid customer balances split by how long they have been outstanding.</summary>
    [HttpGet("credit-ageing")]
    public async Task<IActionResult> CreditAgeing()
    {
        var today = DateTime.UtcNow.Date;
        var open = await _db.Sales.Where(s => s.Due > 0.01m).ToListAsync();
        var customers = await _db.Parties.Where(p => p.Role == "Customer").ToDictionaryAsync(p => p.Id);

        var rows = open.GroupBy(s => new { s.CustomerId, s.CustomerName })
            .Select(g =>
            {
                decimal B(int min, int max) => g.Where(s =>
                {
                    var age = (int)(today - s.SaleDate.Date).TotalDays;
                    return age >= min && age <= max;
                }).Sum(s => s.Due);
                customers.TryGetValue(g.Key.CustomerId ?? 0, out var c);
                return new
                {
                    customerId = g.Key.CustomerId, customerName = g.Key.CustomerName, phone = c?.Phone ?? "",
                    bills = g.Count(), oldestBill = g.Min(s => s.SaleDate).ToString("yyyy-MM-dd"),
                    days0to30 = B(0, 30), days31to60 = B(31, 60), days61to90 = B(61, 90), over90 = B(91, int.MaxValue),
                    total = g.Sum(s => s.Due)
                };
            })
            .OrderByDescending(r => r.over90).ThenByDescending(r => r.total)
            .ToList();
        return Ok(new
        {
            totals = new
            {
                days0to30 = rows.Sum(r => r.days0to30), days31to60 = rows.Sum(r => r.days31to60),
                days61to90 = rows.Sum(r => r.days61to90), over90 = rows.Sum(r => r.over90), total = rows.Sum(r => r.total)
            },
            customers = rows
        });
    }

    /// <summary>Revenue, cost and margin per item, from the batch cost recorded on each sale line.</summary>
    [HttpGet("item-margin")]
    public async Task<IActionResult> ItemMargin([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? store)
    {
        var (f, t) = Range(from, to);
        var q = _db.SaleItems.Join(_db.Sales, i => i.SaleId, s => s.Id, (i, s) => new { i, s })
            .Where(x => x.s.SaleDate >= f && x.s.SaleDate < t);
        if (!string.IsNullOrWhiteSpace(store)) q = q.Where(x => x.s.Store == store);
        var lines = await q.ToListAsync();
        var products = await _db.Products.ToDictionaryAsync(p => p.Id);

        var rows = lines.GroupBy(x => x.i.ProductId).Select(g =>
        {
            products.TryGetValue(g.Key, out var p);
            var qty = g.Sum(x => x.i.Quantity);
            var revenue = g.Sum(x => x.i.TotalCost - x.i.TaxAmount);
            var cost = g.Sum(x => x.i.Quantity * (x.i.CostPrice > 0 ? x.i.CostPrice : p?.PurchasePrice ?? 0));
            var profit = revenue - cost;
            return new
            {
                productId = g.Key, productName = p?.ProductName ?? g.First().i.ProductName, genericName = p?.GenericName ?? "",
                quantity = qty, revenue = Math.Round(revenue, 2), cost = Math.Round(cost, 2), profit = Math.Round(profit, 2),
                marginPercent = revenue > 0 ? Math.Round(profit / revenue * 100, 1) : 0,
                costMissing = g.Any(x => x.i.CostPrice <= 0) && (p?.PurchasePrice ?? 0) <= 0
            };
        }).OrderByDescending(r => r.profit).ToList();

        var totalRevenue = rows.Sum(r => r.revenue);
        var totalProfit = rows.Sum(r => r.profit);
        return Ok(new
        {
            from = f.ToString("yyyy-MM-dd"), to = t.AddDays(-1).ToString("yyyy-MM-dd"),
            totalRevenue, totalCost = rows.Sum(r => r.cost), totalProfit,
            marginPercent = totalRevenue > 0 ? Math.Round(totalProfit / totalRevenue * 100, 1) : 0,
            items = rows
        });
    }

    /// <summary>One row per day: bills, gross, discount, tax, net and how it was paid, less returns.</summary>
    [HttpGet("daily-sales")]
    public async Task<IActionResult> DailySales([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? store)
    {
        var (f, t) = Range(from, to);
        var q = _db.Sales.Include(s => s.Items).Where(s => s.SaleDate >= f && s.SaleDate < t);
        if (!string.IsNullOrWhiteSpace(store)) q = q.Where(s => s.Store == store);
        var sales = await q.ToListAsync();
        var returns = await _db.SalesReturns.Where(r => r.ReturnDate >= f && r.ReturnDate < t).ToListAsync();

        var days = sales.Select(s => s.SaleDate.Date).Union(returns.Select(r => r.ReturnDate.Date)).Distinct().OrderByDescending(d => d);
        var rows = days.Select(d =>
        {
            var ds = sales.Where(s => s.SaleDate.Date == d).ToList();
            var dr = returns.Where(r => r.ReturnDate.Date == d).ToList();
            var net = ds.Sum(s => s.GrandTotal);
            return new
            {
                date = d.ToString("yyyy-MM-dd"), bills = ds.Count,
                gross = ds.Sum(s => s.GrandTotal + s.Discount + s.Items.Sum(i => i.Discount) - s.OrderTax),
                discount = ds.Sum(s => s.Discount + s.Items.Sum(i => i.Discount)),
                tax = ds.Sum(s => s.OrderTax), net,
                cash = ds.Where(s => s.PaymentMethod == "Cash" || (s.PaymentMethod == null && s.Paid >= s.GrandTotal)).Sum(s => s.GrandTotal),
                card = ds.Where(s => s.PaymentMethod == "Card").Sum(s => s.GrandTotal),
                credit = ds.Where(s => s.PaymentMethod == "Credit").Sum(s => s.GrandTotal),
                returns = dr.Sum(r => r.GrandTotal),
                netAfterReturns = net - dr.Sum(r => r.GrandTotal)
            };
        }).ToList();
        return Ok(rows);
    }

    /// <summary>Sale lines of controlled medicines with patient and doctor, for the register.</summary>
    [HttpGet("controlled-register")]
    public async Task<IActionResult> ControlledRegister([FromQuery] string? from, [FromQuery] string? to)
        => Ok(await PrescriptionLinesAsync(from, to, controlledOnly: true));

    /// <summary>Every sale line of a prescription-only medicine.</summary>
    [HttpGet("prescription-log")]
    public async Task<IActionResult> PrescriptionLog([FromQuery] string? from, [FromQuery] string? to)
        => Ok(await PrescriptionLinesAsync(from, to, controlledOnly: false));

    private async Task<object> PrescriptionLinesAsync(string? from, string? to, bool controlledOnly)
    {
        var (f, t) = Range(from, to);
        var ids = await _db.Products.Where(p => controlledOnly ? p.IsControlled : p.IsPrescriptionOnly).Select(p => p.Id).ToListAsync();
        var lines = await _db.SaleItems.Where(i => ids.Contains(i.ProductId))
            .Join(_db.Sales.Where(s => s.SaleDate >= f && s.SaleDate < t), i => i.SaleId, s => s.Id, (i, s) => new { i, s })
            .OrderByDescending(x => x.s.SaleDate)
            .ToListAsync();
        return lines.Select(x => new
        {
            date = DateTime.SpecifyKind(x.s.SaleDate, DateTimeKind.Utc).ToString("o"),
            billNo = x.s.Reference, saleId = x.s.Id,
            customer = x.s.CustomerName, patient = x.s.PatientName ?? x.s.CustomerName,
            doctor = x.s.DoctorName ?? "", prescription = x.s.PrescriptionImage,
            product = x.i.ProductName, quantity = x.i.Quantity, unit = x.i.SaleUnit, unitQuantity = x.i.UnitQuantity,
            batch = x.i.BatchInfo ?? "", soldBy = x.s.Biller
        }).ToList();
    }

    /// <summary>Sales per branch for the period.</summary>
    [HttpGet("branch-sales")]
    public async Task<IActionResult> BranchSales([FromQuery] string? from, [FromQuery] string? to)
    {
        var (f, t) = Range(from, to);
        var sales = await _db.Sales.Include(s => s.Items).Where(s => s.SaleDate >= f && s.SaleDate < t).ToListAsync();
        var rows = sales.GroupBy(s => string.IsNullOrWhiteSpace(s.Store) ? "Main" : s.Store!)
            .Select(g =>
            {
                var revenue = g.Sum(s => s.GrandTotal);
                var cost = g.SelectMany(s => s.Items).Sum(i => i.Quantity * i.CostPrice);
                return new
                {
                    store = g.Key, bills = g.Count(), revenue, due = g.Sum(s => s.Due),
                    averageBill = g.Count() > 0 ? Math.Round(revenue / g.Count(), 2) : 0,
                    grossProfit = Math.Round(g.Sum(s => s.GrandTotal - s.OrderTax) - cost, 2)
                };
            })
            .OrderByDescending(r => r.revenue).ToList();
        return Ok(rows);
    }

    /// <summary>What each customer buys regularly, for the customer profile.</summary>
    [HttpGet("customer-medication/{customerId:int}")]
    public async Task<IActionResult> CustomerMedication(int customerId)
    {
        var lines = await _db.SaleItems
            .Join(_db.Sales.Where(s => s.CustomerId == customerId), i => i.SaleId, s => s.Id, (i, s) => new { i, s })
            .ToListAsync();
        var products = await _db.Products.ToDictionaryAsync(p => p.Id);
        var rows = lines.GroupBy(x => x.i.ProductId).Select(g =>
        {
            products.TryGetValue(g.Key, out var p);
            var last = g.Max(x => x.s.SaleDate);
            var refill = p?.RefillDays ?? 0;
            return new
            {
                productId = g.Key, productName = p?.ProductName ?? g.First().i.ProductName, genericName = p?.GenericName ?? "",
                timesBought = g.Select(x => x.s.Id).Distinct().Count(), totalQuantity = g.Sum(x => x.i.Quantity),
                lastBought = last.ToString("yyyy-MM-dd"),
                nextRefill = refill > 0 ? last.AddDays(refill).ToString("yyyy-MM-dd") : null,
                lastDoctor = g.OrderByDescending(x => x.s.SaleDate).Select(x => x.s.DoctorName).FirstOrDefault(d => !string.IsNullOrEmpty(d))
            };
        }).OrderByDescending(r => r.lastBought).ToList();
        return Ok(rows);
    }
}

/// <summary>Daily job: queue refill reminders by SMS for regular medicines that are about to run out.</summary>
[ApiController]
[Route("api/cron")]
public class PharmacyCronController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public PharmacyCronController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>POST /api/cron/refill-reminders with header X-Cron-Key. Reminds 2 days before the refill date.</summary>
    [HttpPost("refill-reminders")]
    public async Task<IActionResult> RefillReminders()
    {
        var expectedKey = _config["CronJob:ApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
            return StatusCode(500, new { error = "CronJob:ApiKey is not configured." });
        if (!Request.Headers.TryGetValue("X-Cron-Key", out var key) || key.FirstOrDefault() != expectedKey)
            return Unauthorized(new { error = "Invalid or missing cron key." });

        var today = DateTime.UtcNow.Date;
        var queued = 0;
        var tenantIds = await _db.PharmacySettings.IgnoreQueryFilters()
            .Where(s => s.RefillRemindersEnabled).Select(s => s.TenantId).ToListAsync();

        foreach (var tenantId in tenantIds)
        {
            var settings = await _db.PharmacySettings.IgnoreQueryFilters().FirstAsync(s => s.TenantId == tenantId);
            var products = await _db.Products.IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && p.RefillDays > 0).ToDictionaryAsync(p => p.Id);
            if (products.Count == 0) continue;
            var ids = products.Keys.ToList();
            var maxWindow = products.Values.Max(p => p.RefillDays) + 2;

            // Latest purchase of each refill medicine per registered customer.
            var lines = await _db.SaleItems.IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId && ids.Contains(i.ProductId))
                .Join(_db.Sales.IgnoreQueryFilters().Where(s => s.TenantId == tenantId && s.CustomerId != null && s.SaleDate >= today.AddDays(-maxWindow)),
                    i => i.SaleId, s => s.Id, (i, s) => new { i.ProductId, CustomerId = s.CustomerId!.Value, SaleId = s.Id, s.SaleDate })
                .ToListAsync();
            var latest = lines.GroupBy(x => new { x.CustomerId, x.ProductId }).Select(g => g.OrderByDescending(x => x.SaleDate).First());

            var customerIds = lines.Select(l => l.CustomerId).Distinct().ToList();
            var customers = await _db.Parties.IgnoreQueryFilters().Where(p => customerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            var sent = await _db.RefillReminders.IgnoreQueryFilters().Where(r => r.TenantId == tenantId && customerIds.Contains(r.CustomerId))
                .Select(r => new { r.SaleId, r.ProductId }).ToListAsync();

            foreach (var l in latest)
            {
                var due = l.SaleDate.Date.AddDays(products[l.ProductId].RefillDays);
                if (due < today || due > today.AddDays(2)) continue;
                if (sent.Any(s => s.SaleId == l.SaleId && s.ProductId == l.ProductId)) continue;
                if (!customers.TryGetValue(l.CustomerId, out var c) || string.IsNullOrWhiteSpace(c.Phone)) continue;

                _db.SmsMessages.Add(new SmsMessage
                {
                    TenantId = tenantId, To = c.Phone!, Channel = "sms", Status = "pending",
                    Reference = $"REFILL-{l.SaleId}-{l.ProductId}",
                    Message = $"Assalam o Alaikum {c.FullName}, your {products[l.ProductId].ProductName} may run out around {due:dd MMM}. " +
                              $"We have it ready for you at {settings.PharmacyName}.{(string.IsNullOrWhiteSpace(settings.Phone) ? "" : " Call " + settings.Phone + ".")}"
                });
                _db.RefillReminders.Add(new RefillReminder
                {
                    TenantId = tenantId, CustomerId = l.CustomerId, ProductId = l.ProductId, SaleId = l.SaleId, DueDate = due
                });
                queued++;
            }
            await _db.SaveChangesAsync();
        }
        return Ok(new { queued });
    }
}
