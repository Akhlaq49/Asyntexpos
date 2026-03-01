using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class UnitService : IUnitService
{
    private readonly AppDbContext _db;
    public UnitService(AppDbContext db) => _db = db;

    public async Task<List<DropdownOptionDto>> GetAllDropdownAsync()
    {
        return await _db.Units
            .Select(u => new DropdownOptionDto(u.Value, u.Label))
            .ToListAsync();
    }

    public async Task<List<UnitDto>> GetListAsync()
    {
        var units = await _db.Units
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var result = new List<UnitDto>();
        foreach (var u in units)
        {
            var count = await _db.Products.CountAsync(p => p.Unit == u.Label);
            result.Add(new UnitDto
            {
                Id = u.Id,
                Name = u.Label,
                ShortName = u.Value,
                ProductCount = count,
                Status = u.Status,
                CreatedAt = u.CreatedAt.ToString("dd MMM yyyy")
            });
        }
        return result;
    }

    public async Task<UnitDto> CreateAsync(CreateUnitDto dto)
    {
        var entity = new Unit
        {
            Value = dto.ShortName,
            Label = dto.Name,
            Status = dto.Status
        };
        _db.Units.Add(entity);
        await _db.SaveChangesAsync();

        return new UnitDto
        {
            Id = entity.Id,
            Name = entity.Label,
            ShortName = entity.Value,
            ProductCount = 0,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt.ToString("dd MMM yyyy")
        };
    }

    public async Task<UnitDto?> UpdateAsync(int id, CreateUnitDto dto)
    {
        var entity = await _db.Units.FindAsync(id);
        if (entity == null) return null;

        entity.Value = dto.ShortName;
        entity.Label = dto.Name;
        entity.Status = dto.Status;
        await _db.SaveChangesAsync();

        var count = await _db.Products.CountAsync(p => p.Unit == entity.Label);
        return new UnitDto
        {
            Id = entity.Id,
            Name = entity.Label,
            ShortName = entity.Value,
            ProductCount = count,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt.ToString("dd MMM yyyy")
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Units.FindAsync(id);
        if (entity == null) return (false, null);

        var productCount = await _db.Products.CountAsync(p => p.Unit == entity.Label);
        if (productCount > 0)
            return (false, $"Cannot delete this unit. It is used by {productCount} product(s).");

        _db.Units.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
