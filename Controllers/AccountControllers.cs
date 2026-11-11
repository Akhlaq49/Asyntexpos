using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>Customer credit accounts: balances, limits, statements and manual adjustments.</summary>
[Authorize]
[ApiController]
[Route("api/customer-accounts")]
public class CustomerAccountsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICustomerAccountService _accounts;
    private readonly IAuditService _audit;

    public CustomerAccountsController(AppDbContext db, ICustomerAccountService accounts, IAuditService audit)
    {
        _db = db;
        _accounts = accounts;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Balances([FromQuery] string? search, [FromQuery] bool dueOnly = false)
        => Ok(await _accounts.BalancesAsync(search, dueOnly));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await _db.Parties.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var status = await _accounts.CreditStatusAsync(id);
        var level = p.PriceLevelId.HasValue ? await _db.PriceLevels.Where(l => l.Id == p.PriceLevelId).Select(l => l.Name).FirstOrDefaultAsync() : null;
        return Ok(new
        {
            p.Id, p.FullName, p.Phone, p.CustomerType, p.PriceLevelId, priceLevel = level, p.CreditLimit, p.CreditDays,
            p.OpeningBalance, p.Ntn, p.Strn, p.CreditBlocked,
            balance = status.Balance, status.OverdueBills, status.OverdueAmount,
            available = p.CreditLimit.HasValue ? Math.Max(0, p.CreditLimit.Value - status.Balance) : (decimal?)null
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerAccountDto dto)
    {
        if (!_audit.IsAdmin) return Forbid();
        var p = await _db.Parties.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        if (dto.CreditLimit < 0 || dto.CreditDays < 0) throw new BusinessRuleException("Limits cannot be negative.");
        _audit.Log("CreditTermsChanged", "Party", id, new
        {
            from = new { p.CreditLimit, p.CreditDays, p.PriceLevelId, p.OpeningBalance, p.CreditBlocked },
            to = new { dto.CreditLimit, dto.CreditDays, dto.PriceLevelId, dto.OpeningBalance, dto.CreditBlocked }
        });
        p.CustomerType = dto.CustomerType;
        p.PriceLevelId = dto.PriceLevelId;
        p.CreditLimit = dto.CreditLimit;
        p.CreditDays = dto.CreditDays;
        p.OpeningBalance = dto.OpeningBalance;
        p.Ntn = dto.Ntn;
        p.Strn = dto.Strn;
        p.CreditBlocked = dto.CreditBlocked;
        await _db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpGet("{id:int}/statement")]
    public async Task<IActionResult> Statement(int id, [FromQuery] string? from, [FromQuery] string? to)
        => Ok(await _accounts.StatementAsync(id, from, to));

    [HttpGet("{id:int}/open-bills")]
    public async Task<IActionResult> OpenBills(int id)
    {
        var today = DateTime.UtcNow.Date;
        var rows = await _db.Sales.Where(s => s.CustomerId == id && s.Due > 0).OrderBy(s => s.SaleDate)
            .Select(s => new { s.Id, s.Reference, s.SaleDate, s.GrandTotal, s.Paid, s.Due }).ToListAsync();
        return Ok(rows.Select(r => new { r.Id, r.Reference, saleDate = r.SaleDate.ToString("yyyy-MM-dd"), r.GrandTotal, r.Paid, r.Due, days = (int)(today - r.SaleDate.Date).TotalDays }));
    }

    [HttpGet("{id:int}/credit-check")]
    public async Task<IActionResult> CreditCheck(int id, [FromQuery] decimal amount)
    {
        var s = await _accounts.CreditStatusAsync(id);
        var problems = new List<string>();
        if (s.Blocked) problems.Add("Credit is on hold for this customer.");
        if (s.Limit.HasValue && s.Balance + amount > s.Limit.Value) problems.Add($"Balance would be Rs {s.Balance + amount:N0} against a limit of Rs {s.Limit:N0}.");
        if (s.OverdueBills > 0) problems.Add($"{s.OverdueBills} bill(s) overdue, Rs {s.OverdueAmount:N0}.");
        return Ok(new { s.Balance, s.Limit, s.OverdueBills, s.OverdueAmount, s.Blocked, ok = problems.Count == 0, problems });
    }

    [HttpGet("adjustments")]
    public async Task<IActionResult> Adjustments([FromQuery] int? customerId)
    {
        var q = _db.CustomerLedgerEntries.AsQueryable();
        if (customerId.HasValue) q = q.Where(e => e.CustomerId == customerId);
        return Ok(await q.OrderByDescending(e => e.EntryDate).Take(500).ToListAsync());
    }

    [HttpPost("adjustments")]
    public async Task<IActionResult> AddAdjustment([FromBody] CustomerAdjustmentDto dto)
    {
        if (!_audit.IsAdmin) return Forbid();
        if (dto.Debit < 0 || dto.Credit < 0 || dto.Debit + dto.Credit <= 0) throw new BusinessRuleException("Enter a debit or a credit amount.");
        if (!await _db.Parties.AnyAsync(p => p.Id == dto.CustomerId)) return NotFound();
        var entry = new CustomerLedgerEntry
        {
            CustomerId = dto.CustomerId, EntryType = dto.EntryType is "Opening" or "WriteOff" ? dto.EntryType : "Adjustment",
            Debit = dto.Debit, Credit = dto.Credit, Narration = dto.Narration, CreatedBy = _audit.CurrentUser,
            EntryDate = DateTime.TryParse(dto.EntryDate, out var d) ? d : DateTime.UtcNow,
            Reference = $"ADJ{DateTime.UtcNow:yyMMddHHmmss}"
        };
        _db.CustomerLedgerEntries.Add(entry);
        _audit.Log("AccountAdjusted", "Party", dto.CustomerId, new { entry.EntryType, dto.Debit, dto.Credit, dto.Narration });
        await _db.SaveChangesAsync();
        return Ok(entry);
    }

    [HttpDelete("adjustments/{id:long}")]
    public async Task<IActionResult> DeleteAdjustment(long id)
    {
        if (!_audit.IsAdmin) return Forbid();
        var e = await _db.CustomerLedgerEntries.FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        _db.CustomerLedgerEntries.Remove(e);
        _audit.Log("Deleted", "CustomerLedgerEntry", id, new { e.CustomerId, e.Debit, e.Credit });
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>On-account payments from customers, spread over their oldest bills.</summary>
[Authorize]
[ApiController]
[Route("api/customer-receipts")]
public class CustomerReceiptsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICustomerAccountService _accounts;
    private readonly IAuditService _audit;

    public CustomerReceiptsController(AppDbContext db, ICustomerAccountService accounts, IAuditService audit)
    {
        _db = db;
        _accounts = accounts;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? customerId, [FromQuery] string? from, [FromQuery] string? to)
    {
        var q = _db.CustomerReceipts.AsQueryable();
        if (customerId.HasValue) q = q.Where(r => r.CustomerId == customerId);
        if (DateTime.TryParse(from, out var f)) q = q.Where(r => r.ReceiptDate >= f.Date);
        if (DateTime.TryParse(to, out var t)) q = q.Where(r => r.ReceiptDate < t.Date.AddDays(1));
        var rows = await (from r in q
                          join p in _db.Parties on r.CustomerId equals p.Id into pj
                          from p in pj.DefaultIfEmpty()
                          orderby r.ReceiptDate descending
                          select new
                          {
                              r.Id, r.Reference, r.CustomerId, customerName = p != null ? p.FullName : "", r.Amount, r.PaymentMethod,
                              r.ChequeNo, r.ChequeDate, r.Notes, r.ReceivedBy, r.ReceiptDate, r.SalesReturnId,
                              allocated = _db.SalePayments.Where(sp => sp.CustomerReceiptId == r.Id).Sum(sp => (decimal?)sp.PayingAmount) ?? 0
                          }).Take(1000).ToListAsync();
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var r = await _db.CustomerReceipts.FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();
        var bills = await (from sp in _db.SalePayments
                           join s in _db.Sales on sp.SaleId equals s.Id
                           where sp.CustomerReceiptId == id
                           select new { s.Id, s.Reference, amount = sp.PayingAmount, s.Due }).ToListAsync();
        var customer = await _db.Parties.Where(p => p.Id == r.CustomerId).Select(p => new { p.Id, p.FullName, p.Phone }).FirstOrDefaultAsync();
        return Ok(new { receipt = r, customer, bills, balance = await _accounts.BalanceAsync(r.CustomerId) });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerReceiptDto dto) => Ok(await _accounts.CreateReceiptAsync(dto));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_audit.IsAdmin) return Forbid();
        await _accounts.DeleteReceiptAsync(id);
        return NoContent();
    }
}

/// <summary>Retail / wholesale / distributor price lists.</summary>
[Authorize]
[ApiController]
[Route("api/price-levels")]
public class PriceLevelsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public PriceLevelsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (!await _db.PriceLevels.AnyAsync())
        {
            _db.PriceLevels.AddRange(
                new PriceLevel { Name = "Retail", IsDefault = true },
                new PriceLevel { Name = "Wholesale" },
                new PriceLevel { Name = "Distributor" });
            await _db.SaveChangesAsync();
        }
        var levels = await _db.PriceLevels.OrderByDescending(l => l.IsDefault).ThenBy(l => l.Name).ToListAsync();
        var counts = await _db.ProductPrices.GroupBy(p => p.PriceLevelId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync();
        var customers = await _db.Parties.Where(p => p.PriceLevelId != null).GroupBy(p => p.PriceLevelId!.Value).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync();
        return Ok(levels.Select(l => new
        {
            l.Id, l.Name, l.IsDefault,
            prices = counts.FirstOrDefault(c => c.Id == l.Id)?.N ?? 0,
            customers = customers.FirstOrDefault(c => c.Id == l.Id)?.N ?? 0
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PriceLevelDto dto) => Ok(await SaveAsync(null, dto));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PriceLevelDto dto) => Ok(await SaveAsync(id, dto));

    private async Task<PriceLevel> SaveAsync(int? id, PriceLevelDto dto)
    {
        if (!_audit.IsAdmin) throw new BusinessRuleException("Only a manager can change price levels.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new BusinessRuleException("Enter a name.");
        var level = id.HasValue ? await _db.PriceLevels.FirstOrDefaultAsync(l => l.Id == id) ?? throw new KeyNotFoundException("Price level not found") : new PriceLevel();
        level.Name = dto.Name.Trim();
        if (dto.IsDefault)
        {
            await _db.PriceLevels.Where(l => l.IsDefault && l.Id != level.Id).ForEachAsync(l => l.IsDefault = false);
            level.IsDefault = true;
        }
        if (!id.HasValue) _db.PriceLevels.Add(level);
        await _db.SaveChangesAsync();
        return level;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_audit.IsAdmin) return Forbid();
        var level = await _db.PriceLevels.FirstOrDefaultAsync(l => l.Id == id);
        if (level == null) return NotFound();
        if (await _db.Parties.AnyAsync(p => p.PriceLevelId == id)) throw new BusinessRuleException("Customers are on this price level. Move them first.");
        _db.ProductPrices.RemoveRange(_db.ProductPrices.Where(p => p.PriceLevelId == id));
        _db.PriceLevels.Remove(level);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>All level prices for one product.</summary>
    [HttpGet("prices/{productId:int}")]
    public async Task<IActionResult> Prices(int productId)
        => Ok(await _db.ProductPrices.Where(p => p.ProductId == productId).OrderBy(p => p.PriceLevelId).ThenBy(p => p.SaleUnit).ThenBy(p => p.MinQty).ToListAsync());

    /// <summary>Replaces the level prices of one product.</summary>
    [HttpPut("prices/{productId:int}")]
    public async Task<IActionResult> SavePrices(int productId, [FromBody] List<ProductPriceDto> rows)
    {
        if (!_audit.IsAdmin) return Forbid();
        if (rows.Any(r => r.Price < 0 || r.MinQty < 1)) throw new BusinessRuleException("Prices cannot be negative and quantities start at 1.");
        if (rows.GroupBy(r => new { r.VariantId, r.PriceLevelId, r.SaleUnit, r.MinQty }).Any(g => g.Count() > 1))
            throw new BusinessRuleException("The same level, unit and quantity appears twice.");
        _db.ProductPrices.RemoveRange(_db.ProductPrices.Where(p => p.ProductId == productId));
        foreach (var r in rows)
            _db.ProductPrices.Add(new ProductPrice
            {
                ProductId = productId, VariantId = r.VariantId, PriceLevelId = r.PriceLevelId,
                SaleUnit = r.SaleUnit is "Strip" or "Box" ? r.SaleUnit : "Unit", MinQty = r.MinQty, Price = r.Price
            });
        _audit.Log("LevelPricesChanged", "Product", productId, new { rows = rows.Count });
        await _db.SaveChangesAsync();
        return Ok(new { saved = rows.Count });
    }

    /// <summary>Counter lookup: the level prices that apply to a customer for a set of products.</summary>
    [HttpGet("quote")]
    public async Task<IActionResult> Quote([FromQuery] int? customerId, [FromQuery] int? priceLevelId, [FromQuery] string? productIds)
    {
        var levelId = priceLevelId;
        if (levelId == null && customerId.HasValue)
            levelId = await _db.Parties.Where(p => p.Id == customerId).Select(p => p.PriceLevelId).FirstOrDefaultAsync();
        if (levelId == null) return Ok(new { priceLevelId = (int?)null, prices = Array.Empty<object>() });
        var ids = (productIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
        var q = _db.ProductPrices.Where(p => p.PriceLevelId == levelId);
        if (ids.Count > 0) q = q.Where(p => ids.Contains(p.ProductId));
        var prices = await q.Select(p => new { p.ProductId, p.VariantId, p.SaleUnit, p.MinQty, p.Price }).ToListAsync();
        var name = await _db.PriceLevels.Where(l => l.Id == levelId).Select(l => l.Name).FirstOrDefaultAsync();
        return Ok(new { priceLevelId = levelId, priceLevel = name, prices });
    }
}
