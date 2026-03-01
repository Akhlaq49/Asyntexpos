using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IBankAccountService
{
    Task<List<BankAccountDto>> GetAllAsync();
    Task<BankAccountDto?> GetByIdAsync(int id);
    Task<BankAccountDto> CreateAsync(CreateBankAccountDto dto);
    Task<BankAccountDto?> UpdateAsync(int id, CreateBankAccountDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
