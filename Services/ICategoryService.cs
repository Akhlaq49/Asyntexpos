using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<CategoryDto?> GetByIdAsync(int id);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto);
    Task<CategoryDto?> UpdateAsync(int id, CreateCategoryDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
