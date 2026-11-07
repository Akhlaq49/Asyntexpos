using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IWarrantyService
{
    Task<List<WarrantyDto>> GetAllAsync();
    Task<WarrantyDto> CreateAsync(CreateWarrantyDto dto);
    Task<WarrantyDto?> UpdateAsync(int id, CreateWarrantyDto dto);
    Task<bool> DeleteAsync(int id);
}
