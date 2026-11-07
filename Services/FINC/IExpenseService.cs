using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IExpenseService
{
    Task<List<ExpenseDto>> GetAllAsync();
    Task<ExpenseDto?> GetByIdAsync(int id);
    Task<ExpenseDto> CreateAsync(CreateExpenseDto dto);
    Task<ExpenseDto?> UpdateAsync(int id, CreateExpenseDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
