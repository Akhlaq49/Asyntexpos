using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IVariantAttributeService
{
    Task<List<VariantAttributeDto>> GetAllAsync();
    Task<VariantAttributeDto> CreateAsync(CreateVariantAttributeDto dto);
    Task<VariantAttributeDto?> UpdateAsync(int id, CreateVariantAttributeDto dto);
    Task<bool> DeleteAsync(int id);
}
