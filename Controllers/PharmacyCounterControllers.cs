using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

// ── DTOs ──

public class HoldBillDto
{
    public string? Label { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string CartJson { get; set; } = "[]";
    public decimal Total { get; set; }
}

public class OpenTillDto
{
    public decimal OpeningCash { get; set; }
    public string? Store { get; set; }
    public int? LocationId { get; set; }
}

public class CloseTillDto
{
    public decimal CountedCash { get; set; }
    public string? Notes { get; set; }
}

public class PharmacySettingsDto
{
    public string PharmacyName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? DrugLicenceNo { get; set; }
    public string? Ntn { get; set; }
    public string? Strn { get; set; }
    public string? ReceiptFooter { get; set; }
    public decimal DefaultTaxPercent { get; set; }
    public decimal MaxCashierDiscountPercent { get; set; }
    public int ExpiryAlertDays1 { get; set; }
    public int ExpiryAlertDays2 { get; set; }
    public int ExpiryAlertDays3 { get; set; }
    public bool RequirePrescription { get; set; }
    public bool RefillRemindersEnabled { get; set; }
    public bool FbrEnabled { get; set; }
    public string? FbrPosId { get; set; }
    /// <summary>Masked on read; send it back unchanged to keep the stored token.</summary>
    public string? FbrToken { get; set; }
    public string FbrEnvironment { get; set; } = "Sandbox";
    public string ReceiptLanguage { get; set; } = "en";
    public bool BlockOverCreditLimit { get; set; } = true;
}

// ── Held bills ──

[ApiController]
[Route("api/held-bills")]
[Authorize]
public class HeldBillsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public HeldBillsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.HeldBills.OrderByDescending(h => h.CreatedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Hold([FromBody] HoldBillDto dto)
    {
        var count = await _db.HeldBills.CountAsync();
        var bill = new HeldBill
        {
            Label = string.IsNullOrWhiteSpace(dto.Label) ? $"Bill {count + 1}" : dto.Label.Trim(),
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CartJson = string.IsNullOrWhiteSpace(dto.CartJson) ? "[]" : dto.CartJson,
            Total = dto.Total,
            UserName = _audit.CurrentUser
        };
        _db.HeldBills.Add(bill);
        await _db.SaveChangesAsync();
        return Ok(bill);
    }

    /// <summary>Returns the held bill and removes it from the list (it is back on the counter).</summary>
    [HttpPost("{id:int}/recall")]
    public async Task<IActionResult> Recall(int id)
    {
        var bill = await _db.HeldBills.FirstOrDefaultAsync(h => h.Id == id);
        if (bill == null) return NotFound();
        _db.HeldBills.Remove(bill);
        await _db.SaveChangesAsync();
        return Ok(bill);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var bill = await _db.HeldBills.FirstOrDefaultAsync(h => h.Id == id);
        if (bill == null) return NotFound();
        _db.HeldBills.Remove(bill);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

// ── Till sessions (shift open / close) ──

[ApiController]
[Route("api/till")]
[Authorize]
public class TillController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILocationService _locations;

    public TillController(AppDbContext db, IAuditService audit, ILocationService locations)
    {
        _db = db;
        _audit = audit;
        _locations = locations;
    }

    /// <summary>The signed-in user's open till, or 204 when none is open.</summary>
    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        var user = _audit.CurrentUser;
        var till = await _db.TillSessions.Where(t => t.Status == "Open" && t.UserName == user)
            .OrderByDescending(t => t.OpenedAt).FirstOrDefaultAsync();
        if (till == null) return NoContent();
        return Ok(await SummaryAsync(till));
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenTillDto dto)
    {
        var user = _audit.CurrentUser;
        if (await _db.TillSessions.AnyAsync(t => t.Status == "Open" && t.UserName == user))
            return BadRequest(new { message = "You already have an open till. Close it before opening a new one." });
        var locationId = await _locations.PickAsync(dto.LocationId, dto.Store);
        var locationName = await _db.Locations.Where(l => l.Id == locationId).Select(l => l.Name).FirstAsync();
        var till = new TillSession { UserName = user, Store = string.IsNullOrWhiteSpace(dto.Store) ? locationName : dto.Store, LocationId = locationId, OpeningCash = Math.Max(0, dto.OpeningCash) };
        _db.TillSessions.Add(till);
        _audit.Log("TillOpened", "TillSession", null, new { till.OpeningCash, till.Store });
        await _db.SaveChangesAsync();
        return Ok(await SummaryAsync(till));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var till = await _db.TillSessions.FirstOrDefaultAsync(t => t.Id == id);
        return till == null ? NotFound() : Ok(await SummaryAsync(till));
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, [FromBody] CloseTillDto dto)
    {
        var till = await _db.TillSessions.FirstOrDefaultAsync(t => t.Id == id);
        if (till == null) return NotFound();
        if (till.Status == "Closed") return BadRequest(new { message = "This till is already closed." });
        if (till.UserName != _audit.CurrentUser && !_audit.IsAdmin)
            return BadRequest(new { message = "Only the cashier who opened this till, or an admin, can close it." });

        var totals = await TotalsAsync(till, DateTime.UtcNow);
        till.CashSales = totals.cash;
        till.CardSales = totals.card;
        till.CreditSales = totals.credit;
        till.CashRefunds = totals.refunds;
        till.ExpectedCash = till.OpeningCash + totals.cash - totals.refunds;
        till.CountedCash = dto.CountedCash;
        till.Notes = dto.Notes;
        till.ClosedAt = DateTime.UtcNow;
        till.Status = "Closed";
        _audit.Log("TillClosed", "TillSession", till.Id, new { till.ExpectedCash, till.CountedCash, difference = dto.CountedCash - till.ExpectedCash });
        await _db.SaveChangesAsync();
        return Ok(await SummaryAsync(till));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? user)
    {
        var q = _db.TillSessions.AsQueryable();
        if (DateTime.TryParse(from, out var f)) q = q.Where(t => t.OpenedAt >= f.Date);
        if (DateTime.TryParse(to, out var t2)) q = q.Where(t => t.OpenedAt < t2.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(t => t.UserName == user);
        var list = await q.OrderByDescending(t => t.OpenedAt).Take(500).ToListAsync();
        var result = new List<object>();
        foreach (var till in list) result.Add(await SummaryAsync(till));
        return Ok(result);
    }

    /// <summary>Cash and card are what was collected at the counter; credit is what was billed on account.</summary>
    private async Task<(decimal cash, decimal card, decimal credit, decimal refunds, int bills)> TotalsAsync(TillSession till, DateTime until)
    {
        var sales = await _db.Sales.Include(s => s.Payments).Where(s => s.TillSessionId == till.Id).ToListAsync();
        // Payments that belong to an on-account receipt or a credit note are counted through the receipt, not here.
        var direct = sales.SelectMany(s => s.Payments).Where(p => p.CustomerReceiptId == null && p.PaymentDate <= until).ToList();
        var cash = direct.Where(p => p.PaymentType == "Cash").Sum(p => p.PayingAmount);
        var card = direct.Where(p => p.PaymentType != "Cash").Sum(p => p.PayingAmount);
        // Money customers paid on account at this till.
        var receipts = await _db.CustomerReceipts.Where(r => r.TillSessionId == till.Id && r.ReceiptDate <= until).ToListAsync();
        cash += receipts.Where(r => r.PaymentMethod == "Cash").Sum(r => r.Amount);
        card += receipts.Where(r => r.PaymentMethod != "Cash" && r.PaymentMethod != "Credit note").Sum(r => r.Amount);
        var credit = sales.Where(s => s.PaymentMethod == "Credit").Sum(s => s.GrandTotal);

        // Cash refunds given at the counter while the till was open.
        var refunds = await _db.SalesReturns
            .Where(r => r.RefundMethod == "Cash" && r.CreatedAt >= till.OpenedAt && r.CreatedAt <= until)
            .SumAsync(r => (decimal?)r.Paid) ?? 0;
        return (cash, card, credit, refunds, sales.Count);
    }

    private async Task<object> SummaryAsync(TillSession till)
    {
        var (cash, card, credit, refunds, bills) = till.Status == "Closed"
            ? (till.CashSales, till.CardSales, till.CreditSales, till.CashRefunds, await _db.Sales.CountAsync(s => s.TillSessionId == till.Id))
            : await TotalsAsync(till, DateTime.UtcNow);
        var expected = till.OpeningCash + cash - refunds;
        return new
        {
            till.Id, till.UserName, till.Store, till.Status,
            openedAt = DateTime.SpecifyKind(till.OpenedAt, DateTimeKind.Utc).ToString("o"),
            closedAt = till.ClosedAt == null ? null : DateTime.SpecifyKind(till.ClosedAt.Value, DateTimeKind.Utc).ToString("o"),
            till.OpeningCash, cashSales = cash, cardSales = card, creditSales = credit, cashRefunds = refunds,
            expectedCash = expected, till.CountedCash,
            difference = till.CountedCash == null ? (decimal?)null : till.CountedCash - expected,
            bills, till.Notes
        };
    }
}

// ── Pharmacy settings ──

[ApiController]
[Route("api/pharmacy-settings")]
[Authorize]
public class PharmacySettingsController : ControllerBase
{
    private readonly IPharmacySettingsService _service;
    private readonly IAuditService _audit;

    public PharmacySettingsController(IPharmacySettingsService service, IAuditService audit)
    {
        _service = service;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(Map(await _service.GetAsync()));

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] PharmacySettingsDto dto)
    {
        if (!_audit.IsAdmin) return Forbid();
        var saved = await _service.SaveAsync(new PharmacySettings
        {
            PharmacyName = dto.PharmacyName, Address = dto.Address, Phone = dto.Phone, DrugLicenceNo = dto.DrugLicenceNo,
            Ntn = dto.Ntn, Strn = dto.Strn, ReceiptFooter = dto.ReceiptFooter, DefaultTaxPercent = dto.DefaultTaxPercent,
            MaxCashierDiscountPercent = dto.MaxCashierDiscountPercent, ExpiryAlertDays1 = dto.ExpiryAlertDays1,
            ExpiryAlertDays2 = dto.ExpiryAlertDays2, ExpiryAlertDays3 = dto.ExpiryAlertDays3,
            RequirePrescription = dto.RequirePrescription, RefillRemindersEnabled = dto.RefillRemindersEnabled,
            FbrEnabled = dto.FbrEnabled, FbrPosId = dto.FbrPosId, FbrToken = dto.FbrToken, FbrEnvironment = dto.FbrEnvironment,
            ReceiptLanguage = dto.ReceiptLanguage, BlockOverCreditLimit = dto.BlockOverCreditLimit
        });
        return Ok(Map(saved));
    }

    private static PharmacySettingsDto Map(PharmacySettings s) => new()
    {
        PharmacyName = s.PharmacyName, Address = s.Address, Phone = s.Phone, DrugLicenceNo = s.DrugLicenceNo,
        Ntn = s.Ntn, Strn = s.Strn, ReceiptFooter = s.ReceiptFooter, DefaultTaxPercent = s.DefaultTaxPercent,
        MaxCashierDiscountPercent = s.MaxCashierDiscountPercent, ExpiryAlertDays1 = s.ExpiryAlertDays1,
        ExpiryAlertDays2 = s.ExpiryAlertDays2, ExpiryAlertDays3 = s.ExpiryAlertDays3,
        RequirePrescription = s.RequirePrescription, RefillRemindersEnabled = s.RefillRemindersEnabled,
        FbrEnabled = s.FbrEnabled, FbrPosId = s.FbrPosId,
        FbrToken = string.IsNullOrEmpty(s.FbrToken) ? "" : "••••••" + s.FbrToken[^Math.Min(4, s.FbrToken.Length)..],
        FbrEnvironment = s.FbrEnvironment,
        ReceiptLanguage = s.ReceiptLanguage,
        BlockOverCreditLimit = s.BlockOverCreditLimit
    };
}

// ── Counter extras: coupon check, prescription upload, FBR resend, returnable lines ──

[ApiController]
[Route("api/counter")]
[Authorize]
public class CounterController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileService _files;
    private readonly ISaleService _sales;
    private readonly ISalesReturnService _returns;

    public CounterController(AppDbContext db, IFileService files, ISaleService sales, ISalesReturnService returns)
    {
        _db = db;
        _files = files;
        _sales = sales;
        _returns = returns;
    }

    [HttpGet("coupon")]
    public async Task<IActionResult> CheckCoupon([FromQuery] string code, [FromQuery] decimal amount, [FromQuery] int? customerId)
    {
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { message = "Enter a coupon code." });
        var (ok, error, discount) = await CouponRules.CheckAsync(_db, code, customerId, amount);
        return ok ? Ok(new { code = code.Trim().ToUpperInvariant(), discount }) : BadRequest(new { message = error });
    }

    [HttpPost("prescription-image")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadPrescription(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "Choose an image or PDF of the prescription." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".pdf"))
            return BadRequest(new { message = "Use a JPG, PNG, WEBP or PDF file." });
        var path = await _files.SaveFileAsync(file, "prescriptions");
        return Ok(new { path });
    }

    [HttpPost("sales/{id:int}/fbr")]
    public async Task<IActionResult> ResendFbr(int id)
    {
        var (ok, invoice, error) = await _sales.ResendToFbrAsync(id);
        return ok ? Ok(new { fbrInvoiceNumber = invoice }) : BadRequest(new { message = error });
    }

    [HttpGet("sales/{id:int}/returnable")]
    public async Task<IActionResult> Returnable(int id)
    {
        var result = await _returns.GetReturnableAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Find a bill by its reference (SL001) for returns and reprints.</summary>
    [HttpGet("sales/by-reference/{reference}")]
    public async Task<IActionResult> ByReference(string reference)
    {
        var id = await _db.Sales.Where(s => s.Reference == reference.Trim().ToUpper()).Select(s => (int?)s.Id).FirstOrDefaultAsync();
        return id == null ? NotFound(new { message = $"No bill {reference}." }) : Ok(new { id });
    }
}

// ── Activity log ──

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public AuditLogsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? user,
        [FromQuery] string? action, [FromQuery] string? entityType, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!_audit.IsAdmin) return Forbid();
        var q = _db.AuditLogs.AsQueryable();
        if (DateTime.TryParse(from, out var f)) q = q.Where(a => a.CreatedAt >= f.Date);
        if (DateTime.TryParse(to, out var t)) q = q.Where(a => a.CreatedAt < t.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(a => a.UserName == user);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);

        var total = await q.CountAsync();
        pageSize = Math.Clamp(pageSize, 1, 200);
        var items = await q.OrderByDescending(a => a.CreatedAt).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new
        {
            totalCount = total, page, pageSize,
            items = items.Select(a => new
            {
                a.Id, a.UserName, a.Action, a.EntityType, a.EntityId, a.Details,
                createdAt = DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc).ToString("o")
            })
        });
    }
}
