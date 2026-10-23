using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IStockEntryService
{
    Task<List<StockEntryDto>> GetAllAsync();
    Task<StockEntryDto?> CreateAsync(CreateStockEntryDto dto);
    Task<StockEntryDto?> UpdateAsync(int id, CreateStockEntryDto dto);
    Task<bool> DeleteAsync(int id);
}

