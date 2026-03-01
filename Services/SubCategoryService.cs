using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class SubCategoryService : ISubCategoryService
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;

    public SubCategoryService(AppDbContext db, IFileService fileService)
    {
        _db = db;
        _fileService = fileService;
    }

    public async Task<List<SubCategoryDto>> GetAllAsync()
    {
        return await _db.SubCategories
            .Include(sc => sc.Category)
            .OrderByDescending(sc => sc.CreatedAt)
            .Select(sc => new SubCategoryDto
            {
                Id = sc.Id,
                Image = sc.Image,
                SubCategory = sc.SubCategoryName,
                Category = sc.Category != null ? sc.Category.Name : "",
                CategoryCode = sc.CategoryCode ?? "",
                Description = sc.Description,
                Status = sc.Status
            })
            .ToListAsync();
    }

    public async Task<SubCategoryDto?> GetByIdAsync(int id)
    {
        var sc = await _db.SubCategories
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sc == null) return null;

        return new SubCategoryDto
        {
            Id = sc.Id,
            Image = sc.Image,
            SubCategory = sc.SubCategoryName,
            Category = sc.Category?.Name ?? "",
            CategoryCode = sc.CategoryCode ?? "",
            Description = sc.Description,
            Status = sc.Status
        };
    }

    public async Task<SubCategoryDto> CreateAsync(CreateSubCategoryDto dto, IFormFile? image)
    {
        string? imagePath = null;
        if (image != null)
            imagePath = await _fileService.SaveFileAsync(image, "subcategories");

        var entity = new SubCategory
        {
            SubCategoryName = dto.SubCategory,
            CategoryId = dto.CategoryId,
            CategoryCode = dto.CategoryCode,
            Description = dto.Description,
            Image = imagePath,
            Status = dto.Status
        };
        _db.SubCategories.Add(entity);
        await _db.SaveChangesAsync();

        await _db.Entry(entity).Reference(e => e.Category).LoadAsync();

        return new SubCategoryDto
        {
            Id = entity.Id,
            Image = entity.Image,
            SubCategory = entity.SubCategoryName,
            Category = entity.Category?.Name ?? "",
            CategoryCode = entity.CategoryCode ?? "",
            Description = entity.Description,
            Status = entity.Status
        };
    }

    public async Task<SubCategoryDto?> UpdateAsync(int id, CreateSubCategoryDto dto, IFormFile? image)
    {
        var entity = await _db.SubCategories.Include(s => s.Category).FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) return null;

        entity.SubCategoryName = dto.SubCategory;
        entity.CategoryId = dto.CategoryId;
        entity.CategoryCode = dto.CategoryCode;
        entity.Description = dto.Description;
        entity.Status = dto.Status;

        if (image != null)
            entity.Image = await _fileService.SaveFileAsync(image, "subcategories");

        await _db.SaveChangesAsync();

        return new SubCategoryDto
        {
            Id = entity.Id,
            Image = entity.Image,
            SubCategory = entity.SubCategoryName,
            Category = entity.Category?.Name ?? "",
            CategoryCode = entity.CategoryCode ?? "",
            Description = entity.Description,
            Status = entity.Status
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.SubCategories.FindAsync(id);
        if (entity == null) return (false, null);

        var productCount = await _db.Products.CountAsync(p => p.SubCategory == entity.SubCategoryName);
        if (productCount > 0)
            return (false, $"Cannot delete this sub-category. It is used by {productCount} product(s).");

        _db.SubCategories.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
