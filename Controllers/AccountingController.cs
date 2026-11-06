using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Double-entry accounting: chart of accounts, journal, account ledgers, trial balance, profit and loss, balance sheet,
/// lock date. Every report first makes sure the company's ledger is built (LedgerPoster.EnsureFreshAsync).
/// </summary>
[Authorize]
[ApiController]
[Route("api/accounting")]
public class AccountingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LedgerReports _reports;
    public AccountingController(AppDbContext db) { _db = db; _reports = new LedgerReports(db); }

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts(CancellationToken ct)
    {
        await _reports.EnsureFreshAsync(ct);
        var balances = await _db.JournalLines.AsNoTracking().GroupBy(l => l.AccountId)
            .Select(g => new { g.Key, Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) }).ToDictionaryAsync(x => x.Key, ct);
        var used = balances.Keys.ToHashSet();
        return Ok((await _reports.AccountsAsync(ct)).Select(a => new
        {
            a.Id, a.Code, a.Name, a.RootType, a.ParentId, a.IsGroup, a.SystemKey, a.IsActive,
            balance = balances.TryGetValue(a.Id, out var b) ? (LedgerRootType.IsDebitNormal(a.RootType) ? b.Debit - b.Credit : b.Credit - b.Debit) : 0,
            hasEntries = used.Contains(a.Id),
        }));
    }

    public record AccountDto(string Code, string Name, int? ParentId, bool? IsActive);

    /// <summary>An account of the company's own (under a group); entries are only posted to it by later features</summary>
    [HttpPost("accounts")]
    public async Task<IActionResult> AddAccount(AccountDto dto, CancellationToken ct)
    {
        var parent = await _db.LedgerAccounts.FirstOrDefaultAsync(a => a.Id == dto.ParentId && a.IsGroup, ct);
        if (parent == null) return BadRequest(new { message = "Choose the group the account belongs to." });
        var error = await ValidateAsync(dto, null, ct);
        if (error != null) return BadRequest(new { message = error });
        _db.LedgerAccounts.Add(new LedgerAccount { TenantId = _db.CurrentTenantId, Code = dto.Code.Trim(), Name = dto.Name.Trim(), RootType = parent.RootType, ParentId = parent.Id });
        await _db.SaveChangesAsync(ct);
        return await Accounts(ct);
    }

    [HttpPut("accounts/{id:int}")]
    public async Task<IActionResult> UpdateAccount(int id, AccountDto dto, CancellationToken ct)
    {
        var account = await _db.LedgerAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account == null) return NotFound(new { message = "Account not found." });
        var error = await ValidateAsync(dto, id, ct);
        if (error != null) return BadRequest(new { message = error });
        account.Code = dto.Code.Trim();
        account.Name = dto.Name.Trim();
        if (dto.IsActive != null && account.SystemKey == null) account.IsActive = dto.IsActive.Value;   // the posting rules' accounts stay active
        await _db.SaveChangesAsync(ct);
        return await Accounts(ct);
    }

    [HttpDelete("accounts/{id:int}")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken ct)
    {
        var account = await _db.LedgerAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account == null) return NotFound(new { message = "Account not found." });
        if (account.SystemKey != null) return BadRequest(new { message = "This account is used by the posting rules and can't be deleted (rename it instead)." });
        if (await _db.JournalLines.AnyAsync(l => l.AccountId == id, ct) || await _db.LedgerAccounts.AnyAsync(a => a.ParentId == id, ct))
            return BadRequest(new { message = "This account has entries or accounts under it." });
        _db.LedgerAccounts.Remove(account);
        await _db.SaveChangesAsync(ct);
        return await Accounts(ct);
    }

    private async Task<string?> ValidateAsync(AccountDto dto, int? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || dto.Code.Trim().Length > 20) return "Enter a code (up to 20 characters).";
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 150) return "Enter a name (up to 150 characters).";
        var code = dto.Code.Trim();
        if (await _db.LedgerAccounts.AnyAsync(a => a.Code == code && a.Id != id, ct)) return $"Code {code} is already used.";
        return null;
    }

    [HttpGet("journal")]
    public async Task<IActionResult> Journal([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? voucherType,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        await _reports.EnsureFreshAsync(ct);
        return Ok(await _reports.JournalAsync(from, to, voucherType, search, page, pageSize, ct));
    }

    [HttpGet("ledger/{accountId:int}")]
    public async Task<IActionResult> Ledger(int accountId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? party, CancellationToken ct)
    {
        await _reports.EnsureFreshAsync(ct);
        try { return Ok(await _reports.AccountLedgerAsync(accountId, from, to, party, ct)); }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
    }

    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        await _reports.EnsureFreshAsync(ct);
        return Ok(await _reports.TrialBalanceAsync(from, to, ct));
    }

    [HttpGet("profit-and-loss")]
    public async Task<IActionResult> ProfitAndLoss([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string basis = "accrual", CancellationToken ct = default)
    {
        await _reports.EnsureFreshAsync(ct);
        var end = (to ?? DateTime.UtcNow).Date;
        var start = (from ?? new DateTime(end.Year, end.Month, 1)).Date;
        return Ok(await _reports.ProfitAndLossAsync(start, end, basis, ct));
    }

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> BalanceSheet([FromQuery] DateTime? asOf, CancellationToken ct)
    {
        await _reports.EnsureFreshAsync(ct);
        return Ok(await _reports.BalanceSheetAsync((asOf ?? DateTime.UtcNow).Date, ct));
    }

    [HttpGet("parties")]
    public async Task<IActionResult> Parties([FromQuery] string type = LedgerPartyType.Customer, [FromQuery] DateTime? asOf = null, CancellationToken ct = default)
    {
        await _reports.EnsureFreshAsync(ct);
        return Ok(await _reports.PartiesAsync(type, asOf, ct));
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        await _reports.EnsureFreshAsync(ct);
        return Ok(await _reports.CheckAsync(ct));
    }

    public record SettingsDto(DateTime? LockDate);

    /// <summary>Lock date: entries on or before it can't change (null = nothing locked)</summary>
    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(SettingsDto dto, CancellationToken ct)
    {
        await _reports.EnsureFreshAsync(ct);
        var s = await _db.AccountingSettings.FirstAsync(ct);
        s.LockDate = dto.LockDate?.Date;
        await _db.SaveChangesAsync(ct);
        return Ok(await _reports.CheckAsync(ct));
    }

    /// <summary>Posts every document again (after a failure, or to check the ledger against the documents)</summary>
    [HttpPost("rebuild")]
    public async Task<IActionResult> Rebuild(CancellationToken ct)
    {
        await new LedgerPoster(_db).RebuildAsync(ct);
        return Ok(await _reports.CheckAsync(ct));
    }
}
