using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;

    public CustomerService(AppDbContext db, IFileService fileService)
    {
        _db = db;
        _fileService = fileService;
    }

    public async Task<PagedResult<CustomerDto>> GetAllPagedAsync(PaginationQuery query)
    {
        var q = _db.Parties.Where(p => p.Role == "Customer").AsQueryable();

        if (!string.IsNullOrEmpty(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(p => p.FullName.ToLower().Contains(s) ||
                             (p.Phone != null && p.Phone.Contains(s)) ||
                             (p.Email != null && p.Email.ToLower().Contains(s)) ||
                             (p.Cnic != null && p.Cnic.Contains(s)) ||
                             (p.SO != null && p.SO.ToLower().Contains(s)) ||
                             (p.City != null && p.City.ToLower().Contains(s)) ||
                             (p.Address != null && p.Address.ToLower().Contains(s)));
        }

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(p => p.Status == query.Status);

        q = q.OrderByDescending(p => p.CreatedAt);

        var totalCount = await q.CountAsync();
        var items = await q.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<CustomerDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<CustomerDto>> GetAllAsync()
    {
        var list = await _db.Parties
            .Where(p => p.Role == "Customer")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<CustomerDto?> GetByIdAsync(int id)
    {
        var c = await _db.Parties.FindAsync(id);
        if (c == null || c.Role != "Customer") return null;

        return MapToDto(c);
    }

    /// <summary>Blank optional fields are stored as NULL (a blank email would clash with the unique email index)</summary>
    private static string? Opt(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        var entity = new Party
        {
            FullName = dto.Name.Trim(),
            SO = Opt(dto.SO),
            Cnic = Opt(dto.Cnic),
            Phone = Opt(dto.Phone),
            Email = Opt(dto.Email),
            Address = Opt(dto.Address),
            City = Opt(dto.City),
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "active" : dto.Status,
            Role = "Customer"
        };
        _db.Parties.Add(entity);
        await _db.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<CustomerDto?> UpdateAsync(int id, CreateCustomerDto dto)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null || entity.Role != "Customer") return null;

        entity.FullName = dto.Name.Trim();
        entity.SO = Opt(dto.SO);
        entity.Cnic = Opt(dto.Cnic);
        entity.Phone = Opt(dto.Phone);
        entity.Email = Opt(dto.Email);
        entity.Address = Opt(dto.Address);
        entity.City = Opt(dto.City);
        entity.Status = string.IsNullOrWhiteSpace(dto.Status) ? entity.Status : dto.Status;
        await _db.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<CustomerDto?> UploadPictureAsync(int id, IFormFile picture)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null || entity.Role != "Customer") return null;

        entity.Picture = await _fileService.SaveFileAsync(picture, "customers");
        await _db.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null || entity.Role != "Customer") return false;

        _db.Parties.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    private static CustomerDto MapToDto(Party c) => new()
    {
        Id = c.Id.ToString(),
        Name = c.FullName,
        SO = c.SO,
        Cnic = c.Cnic,
        Phone = c.Phone ?? "",
        Email = c.Email ?? "",
        Address = c.Address ?? "",
        City = c.City ?? "",
        Picture = c.Picture,
        Status = c.Status
    };
}
