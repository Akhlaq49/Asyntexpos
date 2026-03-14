using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

[ApiController]
[Route("api/smsgateway")]
public class SmsGatewayController : ControllerBase
{
    private readonly AppDbContext _db;

    public SmsGatewayController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Get all whitelisted phone numbers</summary>
    [HttpGet("readwhitelisted")]
    public async Task<IActionResult> ReadWhitelisted()
    {
        var numbers = await _db.SmsWhitelistedNumbers
            .OrderBy(w => w.PhoneNumber)
            .Select(w => w.PhoneNumber)
            .ToListAsync();

        return Ok(new { data = numbers });
    }

    /// <summary>Save (replace) whitelisted phone numbers</summary>
    [HttpPost("savewhitelisted")]
    public async Task<IActionResult> SaveWhitelisted([FromBody] SaveWhitelistedRequest request)
    {
        if (request.Numbers == null)
            return BadRequest(new { error = "Numbers list is required." });

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Remove existing whitelisted numbers
            var existing = await _db.SmsWhitelistedNumbers.ToListAsync();
            _db.SmsWhitelistedNumbers.RemoveRange(existing);

            // Add the new list
            var entries = request.Numbers
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Select(n => new SmsWhitelistedNumber
                {
                    PhoneNumber = n.Trim(),
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _db.SmsWhitelistedNumbers.AddRange(entries);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { success = true, message = $"{entries.Count} whitelisted number(s) saved." });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public class SaveWhitelistedRequest
{
    public List<string> Numbers { get; set; } = new();
}
