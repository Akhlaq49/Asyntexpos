using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Controllers;

/// <summary>Tax types (GST, WHT, ...) that can be applied to products, quotations and purchase orders</summary>
[Authorize]
[ApiController]
[Route("api/tax-types")]
public class TaxTypesController : ControllerBase
{
    private readonly AppDbContext _db;
    public TaxTypesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<TaxTypeDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var q = _db.TaxTypes.AsQueryable();
        if (activeOnly) q = q.Where(t => t.IsActive);
        return await q.OrderBy(t => t.Name).Select(t => ToDto(t)).ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<TaxTypeDto>> Create([FromBody] SaveTaxTypeDto dto)
    {
        var error = await ValidateAsync(dto, null);
        if (error != null) return BadRequest(new { message = error });
        var entity = new TaxType { Name = dto.Name.Trim(), Description = dto.Description?.Trim(), IsActive = dto.IsActive, CreatedAt = DateTime.UtcNow };
        _db.TaxTypes.Add(entity);
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TaxTypeDto>> Update(int id, [FromBody] SaveTaxTypeDto dto)
    {
        var entity = await _db.TaxTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null) return NotFound();
        var error = await ValidateAsync(dto, id);
        if (error != null) return BadRequest(new { message = error });
        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description?.Trim();
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    /// <summary>Documents keep the tax name they were saved with, so deleting a type doesn't change them</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.TaxTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null) return NotFound();
        _db.TaxTypes.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ValidateAsync(SaveTaxTypeDto dto, int? id)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Enter the tax name.";
        if (dto.Name.Trim().Length > 100) return "The tax name can be at most 100 characters.";
        var name = dto.Name.Trim().ToLower();
        if (await _db.TaxTypes.AnyAsync(t => t.Id != id && t.Name.ToLower() == name))
            return $"A tax type named \"{dto.Name.Trim()}\" already exists.";
        return null;
    }

    private static TaxTypeDto ToDto(TaxType t) => new()
    {
        Id = t.Id, Name = t.Name, Description = t.Description, IsActive = t.IsActive, CreatedAt = t.CreatedAt,
    };
}
