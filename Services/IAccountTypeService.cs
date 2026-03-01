using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IAccountTypeService
{
    Task<List<AccountTypeDto>> GetAllAsync();
    Task<AccountTypeDto?> GetByIdAsync(int id);
    Task<AccountTypeDto> CreateAsync(CreateAccountTypeDto dto);
    Task<AccountTypeDto?> UpdateAsync(int id, CreateAccountTypeDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
