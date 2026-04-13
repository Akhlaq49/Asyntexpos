using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class WebContentService : IWebContentService
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;
    private readonly IMemoryCache _cache;
    private const string CacheKeyPrefix = "WebContent_Storefront_";

    public WebContentService(AppDbContext db, IFileService fileService, IMemoryCache cache)
    {
        _db = db;
        _fileService = fileService;
        _cache = cache;
    }

    private string GetCacheKey() => $"{CacheKeyPrefix}{_db.CurrentTenantId}";

    private void InvalidateCache() => _cache.Remove(GetCacheKey());

    private static WebContentDto MapToDto(WebContent e) => new()
    {
        Id = e.Id,
        Section = e.Section,
        Title = e.Title,
        Subtitle = e.Subtitle,
        Content = e.Content,
        ImageUrl = e.ImageUrl,
        LinkUrl = e.LinkUrl,
        ButtonText = e.ButtonText,
        SortOrder = e.SortOrder,
        Status = e.Status,
        CreatedAt = e.CreatedAt.ToString("dd MMM yyyy")
    };

    // ──────────────────────────────────────────
    // Admin CRUD
    // ──────────────────────────────────────────

    public async Task<List<WebContentDto>> GetAllAsync()
    {
        var items = await _db.WebContents
            .OrderBy(w => w.Section)
            .ThenBy(w => w.SortOrder)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<List<WebContentDto>> GetBySectionAsync(string section)
    {
        var items = await _db.WebContents
            .Where(w => w.Section == section)
            .OrderBy(w => w.SortOrder)
            .ToListAsync();

        return items.Select(MapToDto).ToList();
    }

    public async Task<WebContentDto?> GetByIdAsync(int id)
    {
        var e = await _db.WebContents.FindAsync(id);
        return e == null ? null : MapToDto(e);
    }

    public async Task<WebContentDto> CreateAsync(CreateWebContentDto dto, IFormFile? image)
    {
        string? imagePath = null;
        if (image != null)
            imagePath = await _fileService.SaveFileAsync(image, "webcontent");

        var entity = new WebContent
        {
            Section = dto.Section.ToLower().Trim(),
            Title = dto.Title,
            Subtitle = dto.Subtitle,
            Content = dto.Content,
            ImageUrl = imagePath,
            LinkUrl = dto.LinkUrl,
            ButtonText = dto.ButtonText,
            SortOrder = dto.SortOrder,
            Status = dto.Status
        };

        _db.WebContents.Add(entity);
        await _db.SaveChangesAsync();
        InvalidateCache();

        return MapToDto(entity);
    }

    public async Task<WebContentDto?> UpdateAsync(int id, CreateWebContentDto dto, IFormFile? image)
    {
        var entity = await _db.WebContents.FindAsync(id);
        if (entity == null) return null;

        entity.Section = dto.Section.ToLower().Trim();
        entity.Title = dto.Title;
        entity.Subtitle = dto.Subtitle;
        entity.Content = dto.Content;
        entity.LinkUrl = dto.LinkUrl;
        entity.ButtonText = dto.ButtonText;
        entity.SortOrder = dto.SortOrder;
        entity.Status = dto.Status;
        entity.UpdatedAt = DateTime.UtcNow;

        if (image != null)
            entity.ImageUrl = await _fileService.SaveFileAsync(image, "webcontent");

        await _db.SaveChangesAsync();
        InvalidateCache();

        return MapToDto(entity);
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.WebContents.FindAsync(id);
        if (entity == null) return (false, null);

        _db.WebContents.Remove(entity);
        await _db.SaveChangesAsync();
        InvalidateCache();

        return (true, null);
    }

    // ──────────────────────────────────────────
    // Storefront (cached — fast retrieval)
    // ──────────────────────────────────────────

    public async Task<StorefrontWebContentResponse> GetStorefrontContentAsync()
    {
        var cacheKey = GetCacheKey();

        if (_cache.TryGetValue(cacheKey, out StorefrontWebContentResponse? cached) && cached != null)
            return cached;

        var allContent = await _db.WebContents
            .Where(w => w.Status == "active")
            .OrderBy(w => w.SortOrder)
            .ToListAsync();

        var response = new StorefrontWebContentResponse
        {
            Header = allContent.Where(c => c.Section == "header").Select(MapToDto).ToList(),
            Banner = allContent.Where(c => c.Section == "banner").Select(MapToDto).ToList(),
            Slider = allContent.Where(c => c.Section == "slider").Select(MapToDto).ToList(),
            NearSlider = allContent.Where(c => c.Section == "near_slider").Select(MapToDto).ToList(),
            Footer = allContent.Where(c => c.Section == "footer").Select(MapToDto).ToList()
        };

        _cache.Set(cacheKey, response, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(3)
        });

        return response;
    }
}
