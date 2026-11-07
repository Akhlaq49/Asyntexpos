using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly AppDbContext _db;
    public ExpenseCategoryService(AppDbContext db) => _db = db;

    public async Task<List<ExpenseCategoryDto>> GetAllAsync()
    {
        return await _db.ExpenseCategories
            .OrderByDescending(c => c.CreatedOn)
            .Select(c => new ExpenseCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Status = c.Status,
                CreatedOn = c.CreatedOn.ToString("dd MMM yyyy")
            })
            .ToListAsync();
    }

    public async Task<ExpenseCategoryDto?> GetByIdAsync(int id)
    {
        var c = await _db.ExpenseCategories.FindAsync(id);
        if (c == null) return null;
        return new ExpenseCategoryDto
        {
            Id = c.Id, Name = c.Name, Description = c.Description,
            Status = c.Status, CreatedOn = c.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryDto dto)
    {
        var entity = new ExpenseCategory
        {
            Name = dto.Name,
            Description = dto.Description,
            Status = dto.Status
        };
        _db.ExpenseCategories.Add(entity);
        await _db.SaveChangesAsync();
        return new ExpenseCategoryDto
        {
            Id = entity.Id, Name = entity.Name, Description = entity.Description,
            Status = entity.Status, CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<ExpenseCategoryDto?> UpdateAsync(int id, CreateExpenseCategoryDto dto)
    {
        var entity = await _db.ExpenseCategories.FindAsync(id);
        if (entity == null) return null;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Status = dto.Status;
        await _db.SaveChangesAsync();
        return new ExpenseCategoryDto
        {
            Id = entity.Id, Name = entity.Name, Description = entity.Description,
            Status = entity.Status, CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.ExpenseCategories.FindAsync(id);
        if (entity == null) return (false, null);
        var count = await _db.Expenses.CountAsync(e => e.ExpenseCategoryId == id);
        if (count > 0) return (false, $"Cannot delete. {count} expense(s) use this category.");
        _db.ExpenseCategories.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
