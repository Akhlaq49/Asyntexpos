using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IWebContentService
{
    // Admin CRUD
    Task<List<WebContentDto>> GetAllAsync();
    Task<List<WebContentDto>> GetBySectionAsync(string section);
    Task<WebContentDto?> GetByIdAsync(int id);
    Task<WebContentDto> CreateAsync(CreateWebContentDto dto, IFormFile? image);
    Task<WebContentDto?> UpdateAsync(int id, CreateWebContentDto dto, IFormFile? image);
    Task<(bool success, string? error)> DeleteAsync(int id);

    // Storefront (cached)
    Task<StorefrontWebContentResponse> GetStorefrontContentAsync();
}
