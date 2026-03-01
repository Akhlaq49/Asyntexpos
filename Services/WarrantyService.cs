using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class WarrantyService : IWarrantyService
{
    private readonly AppDbContext _db;
    public WarrantyService(AppDbContext db) => _db = db;

    public async Task<List<WarrantyDto>> GetAllAsync()
    {
        return await _db.Warranties
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WarrantyDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                Duration = w.Duration,
                Period = w.Period,
                DurationDisplay = w.Duration + " " + w.Period + (w.Duration != 1 ? "s" : ""),
                Status = w.Status
            })
            .ToListAsync();
    }

    public async Task<WarrantyDto> CreateAsync(CreateWarrantyDto dto)
    {
        var entity = new Warranty
        {
            Name = dto.Name,
            Description = dto.Description,
            Duration = dto.Duration,
            Period = dto.Period,
            Status = dto.Status
        };
        _db.Warranties.Add(entity);
        await _db.SaveChangesAsync();

        return new WarrantyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Duration = entity.Duration,
            Period = entity.Period,
            DurationDisplay = entity.Duration + " " + entity.Period + (entity.Duration != 1 ? "s" : ""),
            Status = entity.Status
        };
    }

    public async Task<WarrantyDto?> UpdateAsync(int id, CreateWarrantyDto dto)
    {
        var entity = await _db.Warranties.FindAsync(id);
        if (entity == null) return null;

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Duration = dto.Duration;
        entity.Period = dto.Period;
        entity.Status = dto.Status;
        await _db.SaveChangesAsync();

        return new WarrantyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Duration = entity.Duration,
            Period = entity.Period,
            DurationDisplay = entity.Duration + " " + entity.Period + (entity.Duration != 1 ? "s" : ""),
            Status = entity.Status
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Warranties.FindAsync(id);
        if (entity == null) return false;
        _db.Warranties.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}
