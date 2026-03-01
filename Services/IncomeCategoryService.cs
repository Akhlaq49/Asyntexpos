using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class IncomeCategoryService : IIncomeCategoryService
{
    private readonly AppDbContext _db;
    public IncomeCategoryService(AppDbContext db) => _db = db;

    public async Task<List<IncomeCategoryDto>> GetAllAsync()
    {
        return await _db.IncomeCategories
            .OrderByDescending(c => c.CreatedOn)
            .Select(c => new IncomeCategoryDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                CreatedOn = c.CreatedOn.ToString("dd MMM yyyy")
            })
            .ToListAsync();
    }

    public async Task<IncomeCategoryDto?> GetByIdAsync(int id)
    {
        var c = await _db.IncomeCategories.FindAsync(id);
        if (c == null) return null;
        return new IncomeCategoryDto
        {
            Id = c.Id, Code = c.Code, Name = c.Name,
            CreatedOn = c.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<IncomeCategoryDto> CreateAsync(CreateIncomeCategoryDto dto)
    {
        var entity = new IncomeCategory { Code = dto.Code, Name = dto.Name };
        _db.IncomeCategories.Add(entity);
        await _db.SaveChangesAsync();
        return new IncomeCategoryDto
        {
            Id = entity.Id, Code = entity.Code, Name = entity.Name,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<IncomeCategoryDto?> UpdateAsync(int id, CreateIncomeCategoryDto dto)
    {
        var entity = await _db.IncomeCategories.FindAsync(id);
        if (entity == null) return null;
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        await _db.SaveChangesAsync();
        return new IncomeCategoryDto
        {
            Id = entity.Id, Code = entity.Code, Name = entity.Name,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.IncomeCategories.FindAsync(id);
        if (entity == null) return (false, null);
        var count = await _db.FinanceIncomes.CountAsync(i => i.IncomeCategoryId == id);
        if (count > 0) return (false, $"Cannot delete. {count} income record(s) use this category.");
        _db.IncomeCategories.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
