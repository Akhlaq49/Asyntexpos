using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Journal vouchers: entries typed in by a person, for what no other document posts (reclass, correction,
/// depreciation / amortization, accrual). Saved = posted to the ledger (LedgerPoster); the lock date applies.
/// Rules: at least two lines, each a debit or a credit, debits = credits; active non-group accounts only, never Stock
/// (its value comes from purchases and sales); receivable lines name a customer, payable / supplier-return lines a
/// supplier, loan lines a lender. A mistake is undone with Reverse (a new voucher with debit and credit swapped).
/// </summary>
[Authorize]
[ApiController]
[Route("api/accounting/vouchers")]
public class JournalVouchersController : ControllerBase
{
    private readonly AppDbContext _db;
    public JournalVouchersController(AppDbContext db) => _db = db;

    public record LineDto(int AccountId, decimal Debit, decimal Credit, int? PartyId, string? PartyName, string? Note);
    public record SaveDto(DateTime? Date, string? Description, List<LineDto>? Lines);
    public record ReverseDto(DateTime? Date, string? Description);

    /// <summary>The party a line on this account must name (null = none)</summary>
    public static string? PartyFor(string? systemKey) => systemKey switch
    {
        LedgerChart.Receivable => LedgerPartyType.Customer,
        LedgerChart.Payable or LedgerChart.SupplierReturns => LedgerPartyType.Supplier,
        LedgerChart.Loans => LedgerPartyType.Lender,
        _ => null,
    };

    // ── read ─────────────────────────────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var q = _db.JournalVouchers.AsNoTracking().AsQueryable();
        if (from != null) q = q.Where(v => v.Date >= from.Value.Date);
        if (to != null) { var end = to.Value.Date.AddDays(1); q = q.Where(v => v.Date < end); }
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(v => v.Reference.Contains(s) || v.Description.Contains(s)); }
        var total = await q.CountAsync(ct);
        pageSize = Math.Clamp(pageSize, 10, 200); page = Math.Max(1, page);
        var list = await q.OrderByDescending(v => v.Date).ThenByDescending(v => v.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(v => v.Lines).ToListAsync(ct);
        return Ok(new { total, page, pageSize, vouchers = await ToDtosAsync(list, ct) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var v = await _db.JournalVouchers.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v == null) return NotFound(new { message = "This voucher no longer exists." });
        return Ok((await ToDtosAsync(new() { v }, ct))[0]);
    }

    /// <summary>Accounts a voucher line may use, with the party each one needs</summary>
    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct)
    {
        await new LedgerPoster(_db).EnsureFreshAsync(ct);        // the chart exists
        var list = await _db.LedgerAccounts.AsNoTracking().Where(a => !a.IsGroup && a.IsActive && a.SystemKey != LedgerChart.Stock)
            .OrderBy(a => a.Code).ToListAsync(ct);
        return Ok(list.Select(a => new { a.Id, a.Code, a.Name, a.RootType, partyType = PartyFor(a.SystemKey) }));
    }

    // ── write ────────────────────────────────────────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create(SaveDto dto, CancellationToken ct)
    {
        var (lines, error) = await CheckAsync(dto, ct);
        if (error != null) return BadRequest(new { message = error });
        var v = new JournalVoucher
        {
            Date = dto.Date!.Value, Reference = await NextReferenceAsync(ct), Description = dto.Description!.Trim(),
            CreatedBy = User?.FindFirstValue(ClaimTypes.Name), Lines = lines!,
        };
        _db.JournalVouchers.Add(v);
        await _db.SaveChangesAsync(ct);
        return await Get(v.Id, ct);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveDto dto, CancellationToken ct)
    {
        var v = await _db.JournalVouchers.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v == null) return NotFound(new { message = "This voucher no longer exists." });
        if (v.ReversalOfId != null) return BadRequest(new { message = "A reversal can't be edited. Delete it instead." });
        if (await _db.JournalVouchers.AnyAsync(x => x.ReversalOfId == id, ct))
            return BadRequest(new { message = "This voucher has been reversed. Delete its reversal first to edit it." });
        var (lines, error) = await CheckAsync(dto, ct);
        if (error != null) return BadRequest(new { message = error });
        v.Date = dto.Date!.Value;
        v.Description = dto.Description!.Trim();
        v.UpdatedAt = DateTime.UtcNow;
        _db.JournalVoucherLines.RemoveRange(v.Lines);
        foreach (var l in lines!) v.Lines.Add(l);
        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var v = await _db.JournalVouchers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v == null) return NotFound(new { message = "This voucher no longer exists." });
        var reversal = await _db.JournalVouchers.Where(x => x.ReversalOfId == id).Select(x => x.Reference).FirstOrDefaultAsync(ct);
        if (reversal != null) return BadRequest(new { message = $"This voucher has been reversed by {reversal}. Delete {reversal} first." });
        _db.JournalVouchers.Remove(v);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>A new voucher with the same lines, debit and credit swapped (undoes the voucher from its date on)</summary>
    [HttpPost("{id:int}/reverse")]
    public async Task<IActionResult> Reverse(int id, ReverseDto dto, CancellationToken ct)
    {
        var v = await _db.JournalVouchers.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v == null) return NotFound(new { message = "This voucher no longer exists." });
        if (v.ReversalOfId != null) return BadRequest(new { message = "A reversal can't be reversed. Delete it instead." });
        var existing = await _db.JournalVouchers.Where(x => x.ReversalOfId == id).Select(x => x.Reference).FirstOrDefaultAsync(ct);
        if (existing != null) return BadRequest(new { message = $"This voucher has already been reversed by {existing}." });
        var date = (dto.Date ?? DateTime.UtcNow).Date;
        if (date < v.Date.Date) return BadRequest(new { message = "The reversal can't be dated before the voucher it reverses." });
        var description = string.IsNullOrWhiteSpace(dto.Description) ? $"Reversal of {v.Reference}" : dto.Description.Trim();
        var r = new JournalVoucher
        {
            Date = date, Reference = await NextReferenceAsync(ct), Description = Trim(description, 300)!, ReversalOfId = v.Id,
            CreatedBy = User?.FindFirstValue(ClaimTypes.Name),
            Lines = v.Lines.OrderBy(l => l.LineNo).Select(l => new JournalVoucherLine
            {
                LineNo = l.LineNo, AccountId = l.AccountId, Debit = l.Credit, Credit = l.Debit,
                PartyType = l.PartyType, PartyId = l.PartyId, PartyName = l.PartyName, Note = l.Note,
            }).ToList(),
        };
        _db.JournalVouchers.Add(r);
        await _db.SaveChangesAsync(ct);
        return await Get(r.Id, ct);
    }

    // ── rules ────────────────────────────────────────────────────────────────────────────────────────────────
    private async Task<(List<JournalVoucherLine>? Lines, string? Error)> CheckAsync(SaveDto dto, CancellationToken ct)
    {
        if (dto.Date == null) return (null, "Choose the voucher date.");
        if (string.IsNullOrWhiteSpace(dto.Description)) return (null, "Enter a description (why this entry is made).");
        if (dto.Description.Trim().Length > 300) return (null, "The description can be up to 300 characters.");
        var input = (dto.Lines ?? new()).Where(l => l.AccountId > 0 || l.Debit != 0 || l.Credit != 0).ToList();
        if (input.Count < 2) return (null, "Add at least two lines.");
        if (input.Count > 200) return (null, "A voucher can have up to 200 lines.");

        var ids = input.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _db.LedgerAccounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var customers = await PartyNamesAsync(input, accounts, LedgerPartyType.Customer, ct);
        var suppliers = await PartyNamesAsync(input, accounts, LedgerPartyType.Supplier, ct);

        var lines = new List<JournalVoucherLine>();
        for (var i = 0; i < input.Count; i++)
        {
            var l = input[i];
            var n = i + 1;
            var debit = Math.Round(l.Debit, 2, MidpointRounding.AwayFromZero);
            var credit = Math.Round(l.Credit, 2, MidpointRounding.AwayFromZero);
            if (!accounts.TryGetValue(l.AccountId, out var a)) return (null, $"Line {n}: choose an account.");
            if (a.IsGroup) return (null, $"Line {n}: {a.Name} is a group. Choose an account under it.");
            if (!a.IsActive) return (null, $"Line {n}: {a.Name} is inactive.");
            if (a.SystemKey == LedgerChart.Stock) return (null, $"Line {n}: {a.Name} can't be used. Stock value comes from purchases, sales and stock entries.");
            if (debit < 0 || credit < 0) return (null, $"Line {n}: amounts can't be negative.");
            if (debit > 0 == credit > 0) return (null, $"Line {n}: enter either a debit or a credit.");

            var line = new JournalVoucherLine { LineNo = n, AccountId = a.Id, Debit = debit, Credit = credit, Note = Trim(l.Note?.Trim(), 300) };
            switch (PartyFor(a.SystemKey))
            {
                case LedgerPartyType.Customer:
                    if (l.PartyId == null || !customers.TryGetValue(l.PartyId.Value, out var customer)) return (null, $"Line {n}: choose the customer.");
                    (line.PartyType, line.PartyId, line.PartyName) = (LedgerPartyType.Customer, l.PartyId, customer);
                    break;
                case LedgerPartyType.Supplier:
                    // purchases keep suppliers by name, so the voucher does too (the same supplier ledger)
                    if (l.PartyId == null || !suppliers.TryGetValue(l.PartyId.Value, out var supplier)) return (null, $"Line {n}: choose the supplier.");
                    (line.PartyType, line.PartyName) = (LedgerPartyType.Supplier, supplier);
                    break;
                case LedgerPartyType.Lender:
                    if (string.IsNullOrWhiteSpace(l.PartyName)) return (null, $"Line {n}: enter the lender (bank or person).");
                    (line.PartyType, line.PartyName) = (LedgerPartyType.Lender, Trim(l.PartyName.Trim(), 150));
                    break;
            }
            lines.Add(line);
        }
        var debits = lines.Sum(l => l.Debit);
        var credits = lines.Sum(l => l.Credit);
        if (debits != credits) return (null, $"Debits (Rs {debits:N2}) and credits (Rs {credits:N2}) must be equal. Difference: Rs {Math.Abs(debits - credits):N2}.");
        return (lines, null);
    }

    private async Task<Dictionary<int, string>> PartyNamesAsync(List<LineDto> input, Dictionary<int, LedgerAccount> accounts, string type, CancellationToken ct)
    {
        var ids = input.Where(l => l.PartyId != null && accounts.TryGetValue(l.AccountId, out var a) && PartyFor(a.SystemKey) == type)
            .Select(l => l.PartyId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new();
        var list = await _db.Parties.AsNoTracking().Where(p => ids.Contains(p.Id) && p.Role == type).Select(p => new { p.Id, p.FullName }).ToListAsync(ct);
        return list.ToDictionary(p => p.Id, p => Trim(p.FullName.Trim(), 200)!);
    }

    /// <summary>JV-0001, JV-0002 ... per company; a deleted voucher keeps its number (soft delete), so it is never reused</summary>
    private async Task<string> NextReferenceAsync(CancellationToken ct)
    {
        var tenant = _db.CurrentTenantId;
        var refs = await _db.JournalVouchers.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.TenantId == tenant && v.Reference.StartsWith("JV-")).Select(v => v.Reference).ToListAsync(ct);
        var last = refs.Select(r => int.TryParse(r[3..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"JV-{last + 1:0000}";
    }

    private async Task<List<object>> ToDtosAsync(List<JournalVoucher> list, CancellationToken ct)
    {
        var accountIds = list.SelectMany(v => v.Lines).Select(l => l.AccountId).Distinct().ToList();
        var names = await _db.LedgerAccounts.AsNoTracking().Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => new { a.Code, a.Name }, ct);
        var ids = list.Select(v => v.Id).ToList();
        var reversedBy = await _db.JournalVouchers.AsNoTracking().Where(v => v.ReversalOfId != null && ids.Contains(v.ReversalOfId.Value))
            .Select(v => new { Of = v.ReversalOfId!.Value, v.Id, v.Reference }).ToListAsync(ct);
        var originalIds = list.Where(v => v.ReversalOfId != null).Select(v => v.ReversalOfId!.Value).ToList();
        var originals = await _db.JournalVouchers.AsNoTracking().Where(v => originalIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Reference, ct);
        return list.Select(v =>
        {
            var by = reversedBy.FirstOrDefault(r => r.Of == v.Id);
            return (object)new
            {
                v.Id, v.Date, v.Reference, v.Description, v.CreatedBy, v.CreatedAt, v.UpdatedAt,
                v.ReversalOfId, reversalOfReference = v.ReversalOfId != null ? originals.GetValueOrDefault(v.ReversalOfId.Value) : null,
                reversedById = by?.Id, reversedByReference = by?.Reference,
                total = v.Lines.Sum(l => l.Debit),
                lines = v.Lines.OrderBy(l => l.LineNo).Select(l => new
                {
                    l.Id, l.LineNo, l.AccountId, code = names.GetValueOrDefault(l.AccountId)?.Code, account = names.GetValueOrDefault(l.AccountId)?.Name,
                    l.Debit, l.Credit, l.PartyType, l.PartyId, l.PartyName, l.Note,
                }),
            };
        }).ToList();
    }

    private static string? Trim(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
}
