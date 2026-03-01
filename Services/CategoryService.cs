using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;

    public CategoryService(AppDbContext db) => _db = db;

    public async Task<List<CategoryDto>> GetAllAsync()
    {
        return await _db.Categories
            .OrderByDescending(c => c.CreatedOn)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                CreatedOn = c.CreatedOn.ToString("dd MMM yyyy"),
                Status = c.Status
            })
            .ToListAsync();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c == null) return null;
        return new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            CreatedOn = c.CreatedOn.ToString("dd MMM yyyy"),
            Status = c.Status
        };
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        var entity = new Category
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Status = dto.Status
        };
        _db.Categories.Add(entity);
        await _db.SaveChangesAsync();

        return new CategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy"),
            Status = entity.Status
        };
    }

    public async Task<CategoryDto?> UpdateAsync(int id, CreateCategoryDto dto)
    {
        var entity = await _db.Categories.FindAsync(id);
        if (entity == null) return null;

        entity.Name = dto.Name;
        entity.Slug = dto.Slug;
        entity.Status = dto.Status;
        await _db.SaveChangesAsync();

        return new CategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy"),
            Status = entity.Status
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Categories.FindAsync(id);
        if (entity == null) return (false, null);

        var subCatCount = await _db.SubCategories.CountAsync(sc => sc.CategoryId == id);
        if (subCatCount > 0)
            return (false, $"Cannot delete this category. It has {subCatCount} sub-category(ies).");

        var productCount = await _db.Products.CountAsync(p => p.Category == entity.Name);
        if (productCount > 0)
            return (false, $"Cannot delete this category. It is used by {productCount} product(s).");

        _db.Categories.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
