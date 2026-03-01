using Microsoft.AspNetCore.Http;
using ReactPosApi.Controllers;
using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<PagedResult<ProductDto>> GetAllPagedAsync(PaginationQuery query);
    Task<ProductDto?> GetByIdAsync(int id);
    Task<List<ProductDto>> GetExpiredAsync();
    Task<ProductDto?> UpdateExpiredAsync(int id, UpdateExpiredDto dto);
    Task<(bool success, string? error)> DeleteExpiredAsync(int id);
    Task<List<ProductDto>> GetLowStocksAsync();
    Task<List<ProductDto>> GetOutOfStocksAsync();
    Task<ProductDto?> UpdateLowStockAsync(int id, UpdateLowStockDto dto);
    Task<ProductDto> CreateAsync(ProductFormModel form);
    Task<ProductDto?> UpdateAsync(int id, ProductFormModel form);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
