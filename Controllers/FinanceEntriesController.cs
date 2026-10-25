using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

/// <summary>Capital &amp; Loans: owner's investment / drawings, loans, fixed assets and depreciation</summary>
[Authorize]
[ApiController]
[Route("api/finance-entries")]
public class FinanceEntriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public FinanceEntriesController(AppDbContext db) => _db = db;

    public record SaveEntryDto(string Type, string? Name, decimal Amount, string? Method, DateTime? Date, string? Reference, string? Notes);
    public record EntryDto(int Id, string Type, string TypeLabel, string? Direction, string Name, decimal Amount, string Method, DateTime Date,
        string? Reference, string? Notes, string? CreatedBy, DateTime CreatedAt);

    private static EntryDto ToDto(FinanceEntry f) => new(f.Id, f.Type, FinanceEntryType.Label(f.Type), FinanceEntryType.Direction(f.Type), f.Name, f.Amount,
        f.Method, f.Date, f.Reference, f.Notes, f.CreatedBy, f.CreatedAt);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.FinanceEntries.OrderByDescending(f => f.Date).ThenByDescending(f => f.Id).ToListAsync();
        // current balance of each loan and fixed asset, for the form's suggestions and checks
        var loans = list.Where(f => f.Type is FinanceEntryType.LoanReceived or FinanceEntryType.LoanRepayment)
            .GroupBy(f => f.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new { name = g.First().Name.Trim(), balance = g.Sum(f => f.Type == FinanceEntryType.LoanReceived ? f.Amount : -f.Amount) })
            .OrderBy(x => x.name).ToList();
        var assets = list.Where(f => f.Type is FinanceEntryType.AssetPurchase or FinanceEntryType.Depreciation)
            .GroupBy(f => f.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new { name = g.First().Name.Trim(), cost = g.Where(f => f.Type == FinanceEntryType.AssetPurchase).Sum(f => f.Amount),
                bookValue = g.Sum(f => f.Type == FinanceEntryType.AssetPurchase ? f.Amount : -f.Amount) })
            .OrderBy(x => x.name).ToList();
        return Ok(new { entries = list.Select(ToDto), loans, assets });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveEntryDto dto)
    {
        var type = FinanceEntryType.All.FirstOrDefault(t => string.Equals(t, dto.Type, StringComparison.OrdinalIgnoreCase));
        if (type == null) return BadRequest(new { message = "Choose what kind of entry this is." });
        var amount = Math.Round(dto.Amount, 2);
        if (amount <= 0) return BadRequest(new { message = "Enter an amount greater than 0." });
        var name = (dto.Name ?? "").Trim();
        if (name.Length == 0)
            return BadRequest(new { message = type switch
            {
                FinanceEntryType.LoanReceived or FinanceEntryType.LoanRepayment => "Enter the lender (bank or person).",
                FinanceEntryType.AssetPurchase or FinanceEntryType.Depreciation => "Enter the asset (e.g. Shop furniture, Delivery van).",
                _ => "Enter the owner's name.",
            } });
        if (name.Length > 150) name = name[..150];

        // a loan can't be repaid beyond what is owed, an asset can't lose more value than it has
        if (type is FinanceEntryType.LoanRepayment or FinanceEntryType.Depreciation)
        {
            var related = await _db.FinanceEntries.Where(f => f.Name == name &&
                (type == FinanceEntryType.LoanRepayment ? f.Type == FinanceEntryType.LoanReceived || f.Type == FinanceEntryType.LoanRepayment
                                                        : f.Type == FinanceEntryType.AssetPurchase || f.Type == FinanceEntryType.Depreciation)).ToListAsync();
            var plus = type == FinanceEntryType.LoanRepayment ? FinanceEntryType.LoanReceived : FinanceEntryType.AssetPurchase;
            var balance = related.Sum(f => f.Type == plus ? f.Amount : -f.Amount);
            if (related.Count == 0)
                return BadRequest(new { message = type == FinanceEntryType.LoanRepayment ? $"No loan from \"{name}\" has been recorded." : $"No fixed asset called \"{name}\" has been recorded." });
            if (amount > balance)
                return BadRequest(new { message = type == FinanceEntryType.LoanRepayment ? $"Only Rs {balance:N2} is still owed to {name}." : $"{name} is only worth Rs {balance:N2} now." });
        }

        var entry = new FinanceEntry
        {
            Type = type, Name = name, Amount = amount,
            Method = type == FinanceEntryType.Depreciation ? "-" : string.IsNullOrWhiteSpace(dto.Method) ? "Cash" : dto.Method.Trim()[..Math.Min(30, dto.Method.Trim().Length)],
            Date = dto.Date ?? DateTime.UtcNow,
            Reference = string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim()[..Math.Min(60, dto.Reference.Trim().Length)],
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()[..Math.Min(500, dto.Notes.Trim().Length)],
            CreatedBy = User.FindFirstValue(ClaimTypes.Name), CreatedAt = DateTime.UtcNow,
        };
        _db.FinanceEntries.Add(entry);
        await _db.SaveChangesAsync();
        return Ok(ToDto(entry));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _db.FinanceEntries.FirstOrDefaultAsync(f => f.Id == id);
        if (entry == null) return NotFound(new { message = "This entry no longer exists." });
        _db.FinanceEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
