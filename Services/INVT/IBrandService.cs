using Microsoft.AspNetCore.Http;
using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IBrandService
{
    Task<List<DropdownOptionDto>> GetAllDropdownAsync();
    Task<List<BrandDto>> GetListAsync();
    Task<BrandDto> CreateAsync(CreateBrandDto dto, IFormFile? image);
    Task<BrandDto?> UpdateAsync(int id, CreateBrandDto dto, IFormFile? image);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
