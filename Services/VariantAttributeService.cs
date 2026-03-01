using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class VariantAttributeService : IVariantAttributeService
{
    private readonly AppDbContext _db;
    public VariantAttributeService(AppDbContext db) => _db = db;

    public async Task<List<VariantAttributeDto>> GetAllAsync()
    {
        return await _db.VariantAttributes
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VariantAttributeDto
            {
                Id = v.Id,
                Name = v.Name,
                Values = v.Values,
                Status = v.Status,
                CreatedAt = v.CreatedAt.ToString("dd MMM yyyy")
            })
            .ToListAsync();
    }

    public async Task<VariantAttributeDto> CreateAsync(CreateVariantAttributeDto dto)
    {
        var entity = new VariantAttribute
        {
            Name = dto.Name,
            Values = dto.Values,
            Status = dto.Status
        };
        _db.VariantAttributes.Add(entity);
        await _db.SaveChangesAsync();

        return new VariantAttributeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Values = entity.Values,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt.ToString("dd MMM yyyy")
        };
    }

    public async Task<VariantAttributeDto?> UpdateAsync(int id, CreateVariantAttributeDto dto)
    {
        var entity = await _db.VariantAttributes.FindAsync(id);
        if (entity == null) return null;

        entity.Name = dto.Name;
        entity.Values = dto.Values;
        entity.Status = dto.Status;
        await _db.SaveChangesAsync();

        return new VariantAttributeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Values = entity.Values,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt.ToString("dd MMM yyyy")
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.VariantAttributes.FindAsync(id);
        if (entity == null) return false;
        _db.VariantAttributes.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}
