using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IIncomeCategoryService
{
    Task<List<IncomeCategoryDto>> GetAllAsync();
    Task<IncomeCategoryDto?> GetByIdAsync(int id);
    Task<IncomeCategoryDto> CreateAsync(CreateIncomeCategoryDto dto);
    Task<IncomeCategoryDto?> UpdateAsync(int id, CreateIncomeCategoryDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
