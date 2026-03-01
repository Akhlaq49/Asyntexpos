using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/parties")]
public class PartiesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Supplier", "Biller", "Store", "Warehouse"
    };

    public PartiesController(AppDbContext db, IFileService fileService)
    {
        _db = db;
        _fileService = fileService;
    }

    /// <summary>
    /// GET api/parties?role=Supplier
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string role,
        [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? search)
    {
        if (string.IsNullOrEmpty(role) || !ValidRoles.Contains(role))
            return BadRequest("A valid role query parameter is required (Supplier, Biller, Store, Warehouse).");

        if (page.HasValue)
        {
            var q = _db.Parties.Where(p => p.Role == role).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                q = q.Where(p => p.FullName.ToLower().Contains(s) ||
                                 (p.Phone != null && p.Phone.Contains(s)) ||
                                 (p.Email != null && p.Email.ToLower().Contains(s)) ||
                                 (p.CompanyName != null && p.CompanyName.ToLower().Contains(s)) ||
                                 (p.Code != null && p.Code.ToLower().Contains(s)));
            }

            q = q.OrderByDescending(p => p.CreatedAt);

            var totalCount = await q.CountAsync();
            var items = await q
                .Skip((page.Value - 1) * (pageSize ?? 10))
                .Take(pageSize ?? 10)
                .ToListAsync();

            return Ok(new PagedResult<PartyDto>
            {
                Items = items.Select(p => MapToDto(p)).ToList(),
                TotalCount = totalCount,
                Page = page.Value,
                PageSize = pageSize ?? 10
            });
        }

        var list = await _db.Parties
            .Where(p => p.Role == role)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>
    /// GET api/parties/5
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToDto(entity));
    }

    /// <summary>
    /// POST api/parties
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePartyDto dto)
    {
        if (!ValidRoles.Contains(dto.Role))
            return BadRequest("Invalid role. Must be Supplier, Biller, Store, or Warehouse.");

        // Auto-generate code if not provided
        if (string.IsNullOrEmpty(dto.Code))
        {
            dto.Code = await GenerateCode(dto.Role);
        }

        var entity = new Party
        {
            FullName = dto.FullName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            PhoneWork = dto.PhoneWork,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Country = dto.Country,
            PostalCode = dto.PostalCode,
            Code = dto.Code,
            CompanyName = dto.CompanyName,
            ContactPerson = dto.ContactPerson,
            UserName = dto.UserName,
            PasswordHash = dto.Password, // In production, hash this
            Role = dto.Role,
            Status = dto.Status,
            IsActive = dto.IsActive,
        };

        _db.Parties.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
    }

    /// <summary>
    /// PUT api/parties/5
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePartyDto dto)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null) return NotFound();

        entity.FullName = dto.FullName;
        entity.LastName = dto.LastName;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.PhoneWork = dto.PhoneWork;
        entity.Address = dto.Address;
        entity.City = dto.City;
        entity.State = dto.State;
        entity.Country = dto.Country;
        entity.PostalCode = dto.PostalCode;
        entity.Code = dto.Code;
        entity.CompanyName = dto.CompanyName;
        entity.ContactPerson = dto.ContactPerson;
        entity.UserName = dto.UserName;
        entity.Status = dto.Status;
        entity.IsActive = dto.IsActive;

        if (!string.IsNullOrEmpty(dto.Password))
        {
            entity.PasswordHash = dto.Password;
        }

        await _db.SaveChangesAsync();
        return Ok(MapToDto(entity));
    }

    /// <summary>
    /// POST api/parties/5/picture
    /// </summary>
    [HttpPost("{id}/picture")]
    public async Task<IActionResult> UploadPicture(int id, IFormFile picture)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Picture = await _fileService.SaveFileAsync(picture, "parties");
        await _db.SaveChangesAsync();
        return Ok(MapToDto(entity));
    }

    /// <summary>
    /// DELETE api/parties/5
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null) return NotFound();

        _db.Parties.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GenerateCode(string role)
    {
        var prefix = role switch
        {
            "Supplier" => "SU",
            "Biller" => "BI",
            "Store" => "ST",
            "Warehouse" => "WH",
            _ => "PT"
        };

        var count = await _db.Parties.CountAsync(p => p.Role == role);
        return $"{prefix}{(count + 1).ToString("D3")}";
    }

    private static PartyDto MapToDto(Party p) => new()
    {
        Id = p.Id,
        FullName = p.FullName,
        LastName = p.LastName,
        Email = p.Email,
        Phone = p.Phone,
        PhoneWork = p.PhoneWork,
        Address = p.Address,
        City = p.City,
        State = p.State,
        Country = p.Country,
        PostalCode = p.PostalCode,
        Picture = p.Picture,
        Code = p.Code,
        CompanyName = p.CompanyName,
        ContactPerson = p.ContactPerson,
        UserName = p.UserName,
        Role = p.Role,
        Status = p.Status,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
    };
}
