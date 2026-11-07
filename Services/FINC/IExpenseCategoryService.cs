using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IExpenseCategoryService
{
    Task<List<ExpenseCategoryDto>> GetAllAsync();
    Task<ExpenseCategoryDto?> GetByIdAsync(int id);
    Task<ExpenseCategoryDto> CreateAsync(CreateExpenseCategoryDto dto);
    Task<ExpenseCategoryDto?> UpdateAsync(int id, CreateExpenseCategoryDto dto);
    Task<(bool success, string? error)> DeleteAsync(int id);
}
