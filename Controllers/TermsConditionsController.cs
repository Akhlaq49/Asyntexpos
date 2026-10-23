using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

/// <summary>Terms &amp; conditions printed at the end of quotations and purchase orders</summary>
[Authorize]
[ApiController]
[Route("api/terms-conditions")]
public class TermsConditionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TermsConditionsController(AppDbContext db) => _db = db;

    public record TermsConditionsDto(string Content, DateTime UpdatedAt, string? UpdatedBy);
    public record SaveTermsConditionsDto(string Content);

    [HttpGet]
    public async Task<ActionResult<TermsConditionsDto>> Get()
    {
        var row = await GetOrCreateAsync();
        return new TermsConditionsDto(row.Content, row.UpdatedAt, row.UpdatedBy);
    }

    [HttpPut]
    public async Task<ActionResult<TermsConditionsDto>> Save([FromBody] SaveTermsConditionsDto dto)
    {
        var row = await GetOrCreateAsync();
        row.Content = (dto.Content ?? string.Empty).Trim();
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = User.FindFirstValue(ClaimTypes.Name);
        await _db.SaveChangesAsync();
        return new TermsConditionsDto(row.Content, row.UpdatedAt, row.UpdatedBy);
    }

    /// <summary>The tenant's row (query filter limits it to the current tenant); new tenants start with the default text</summary>
    private async Task<TermsConditions> GetOrCreateAsync()
    {
        var row = await _db.TermsConditions.FirstOrDefaultAsync();
        if (row != null) return row;
        row = new TermsConditions { Content = DefaultTermsConditions.Text.Trim(), UpdatedAt = DateTime.UtcNow };
        _db.TermsConditions.Add(row); // TenantId is set on save
        await _db.SaveChangesAsync();
        return row;
    }
}
