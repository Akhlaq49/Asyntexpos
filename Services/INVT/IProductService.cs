using Microsoft.AspNetCore.Http;
using ReactPosApi.Controllers;
using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<PagedResult<ProductDto>> GetAllPagedAsync(PaginationQuery query);
    Task<ProductDto?> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(ProductFormModel form);
    Task<ProductDto?> UpdateAsync(int id, ProductFormModel form);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
