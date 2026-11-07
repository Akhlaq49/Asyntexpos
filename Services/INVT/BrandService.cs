using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class BrandService : IBrandService
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;

    public BrandService(AppDbContext db, IFileService fileService)
    {
        _db = db;
        _fileService = fileService;
    }

    public async Task<List<DropdownOptionDto>> GetAllDropdownAsync()
    {
        return await _db.Brands
            .Select(b => new DropdownOptionDto(b.Value, b.Label))
            .ToListAsync();
    }

    public async Task<List<BrandDto>> GetListAsync()
    {
        return await _db.Brands
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BrandDto
            {
                Id = b.Id,
                Name = b.Label,
                Image = b.Image,
                Status = b.Status,
                CreatedAt = b.CreatedAt.ToString("dd MMM yyyy")
            })
            .ToListAsync();
    }

    public async Task<BrandDto> CreateAsync(CreateBrandDto dto, IFormFile? image)
    {
        string? imagePath = null;
        if (image != null)
            imagePath = await _fileService.SaveFileAsync(image, "brands");

        var entity = new Brand
        {
            Value = dto.Name.ToLower().Replace(" ", "-"),
            Label = dto.Name,
            Image = imagePath,
            Status = dto.Status
        };
        _db.Brands.Add(entity);
        await _db.SaveChangesAsync();

        return new BrandDto
        {
            Id = entity.Id,
            Name = entity.Label,
            Image = entity.Image,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt.ToString("dd MMM yyyy")
        };
    }

    public async Task<BrandDto?> UpdateAsync(int id, CreateBrandDto dto, IFormFile? image)
    {
        var entity = await _db.Brands.FindAsync(id);
        if (entity == null) return null;

        entity.Value = dto.Name.ToLower().Replace(" ", "-");
        entity.Label = dto.Name;
        entity.Status = dto.Status;

        if (image != null)
            entity.Image = await _fileService.SaveFileAsync(image, "brands");

        await _db.SaveChangesAsync();

        return new BrandDto
        {
            Id = entity.Id,
            Name = entity.Label,
            Image = entity.Image,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt.ToString("dd MMM yyyy")
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Brands.FindAsync(id);
        if (entity == null) return (false, null);

        var productCount = await _db.Products.CountAsync(p => p.Brand == entity.Label);
        if (productCount > 0)
            return (false, $"Cannot delete this brand. It is used by {productCount} product(s).");

        _db.Brands.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
