using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;
    private readonly IMiscellaneousRegisterService _miscService;
    private readonly IFileService _fileService;

    public CustomerService(AppDbContext db, IMiscellaneousRegisterService miscService, IFileService fileService)
    {
        _db = db;
        _miscService = miscService;
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
                             (p.City != null && p.City.ToLower().Contains(s)));
        }

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(p => p.Status == query.Status);

        q = q.OrderByDescending(p => p.CreatedAt);

        var totalCount = await q.CountAsync();
        var items = await q.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();

        var dtos = new List<CustomerDto>();
        foreach (var c in items)
        {
            var miscBalance = await _miscService.GetBalanceAsync(c.Id);
            dtos.Add(MapToDto(c, miscBalance));
        }

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

        var customerDtos = new List<CustomerDto>();
        foreach (var c in list)
        {
            var miscBalance = await _miscService.GetBalanceAsync(c.Id);
            customerDtos.Add(MapToDto(c, miscBalance));
        }
        return customerDtos;
    }

    public async Task<CustomerDto?> GetByIdAsync(int id)
    {
        var c = await _db.Parties.FindAsync(id);
        if (c == null || c.Role != "Customer") return null;

        var miscBalance = await _miscService.GetBalanceAsync(id);
        return MapToDto(c, miscBalance);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        var entity = new Party
        {
            FullName = dto.Name,
            SO = dto.SO,
            Cnic = dto.Cnic,
            Phone = dto.Phone,
            Email = dto.Email,
            Address = dto.Address,
            City = dto.City,
            Status = dto.Status,
            Role = "Customer"
        };
        _db.Parties.Add(entity);
        await _db.SaveChangesAsync();

        var miscBalance = await _miscService.GetBalanceAsync(entity.Id);
        return MapToDto(entity, miscBalance);
    }

    public async Task<CustomerDto?> UpdateAsync(int id, CreateCustomerDto dto)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null || entity.Role != "Customer") return null;

        entity.FullName = dto.Name;
        entity.SO = dto.SO;
        entity.Cnic = dto.Cnic;
        entity.Phone = dto.Phone;
        entity.Email = dto.Email;
        entity.Address = dto.Address;
        entity.City = dto.City;
        entity.Status = dto.Status;
        await _db.SaveChangesAsync();

        var miscBalance = await _miscService.GetBalanceAsync(id);
        return MapToDto(entity, miscBalance);
    }

    public async Task<CustomerDto?> UploadPictureAsync(int id, IFormFile picture)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null || entity.Role != "Customer") return null;

        entity.Picture = await _fileService.SaveFileAsync(picture, "customers");
        await _db.SaveChangesAsync();

        var miscBalance = await _miscService.GetBalanceAsync(id);
        return MapToDto(entity, miscBalance);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Parties.FindAsync(id);
        if (entity == null || entity.Role != "Customer") return false;

        _db.Parties.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    private static CustomerDto MapToDto(Party c, decimal miscBalance = 0) => new()
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
        Status = c.Status,
        MiscBalance = miscBalance
    };
}
