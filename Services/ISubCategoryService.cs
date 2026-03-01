using Microsoft.AspNetCore.Http;
using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface ISubCategoryService
{
    Task<List<SubCategoryDto>> GetAllAsync();
    Task<SubCategoryDto?> GetByIdAsync(int id);
    Task<SubCategoryDto> CreateAsync(CreateSubCategoryDto dto, IFormFile? image);
    Task<SubCategoryDto?> UpdateAsync(int id, CreateSubCategoryDto dto, IFormFile? image);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
