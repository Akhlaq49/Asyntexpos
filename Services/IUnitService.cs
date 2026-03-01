using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IUnitService
{
    Task<List<DropdownOptionDto>> GetAllDropdownAsync();
    Task<List<UnitDto>> GetListAsync();
    Task<UnitDto> CreateAsync(CreateUnitDto dto);
    Task<UnitDto?> UpdateAsync(int id, CreateUnitDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
