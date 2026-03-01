using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IFinanceIncomeService
{
    Task<List<FinanceIncomeDto>> GetAllAsync();
    Task<FinanceIncomeDto?> GetByIdAsync(int id);
    Task<FinanceIncomeDto> CreateAsync(CreateFinanceIncomeDto dto);
    Task<FinanceIncomeDto?> UpdateAsync(int id, CreateFinanceIncomeDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
