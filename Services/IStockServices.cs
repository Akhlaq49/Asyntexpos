using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IStockEntryService
{
    Task<List<StockEntryDto>> GetAllAsync();
    Task<StockEntryDto?> CreateAsync(CreateStockEntryDto dto);
    Task<StockEntryDto?> UpdateAsync(int id, CreateStockEntryDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface IStockTransferService
{
    Task<object> GetAllAsync();
    Task<object> CreateAsync(CreateStockTransferDto dto);
    Task<object?> UpdateAsync(int id, CreateStockTransferDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface IStockAdjustmentService
{
    Task<List<StockAdjustmentDto>> GetAllAsync();
    Task<StockAdjustmentDto?> CreateAsync(CreateStockAdjustmentDto dto);
    Task<StockAdjustmentDto?> UpdateAsync(int id, CreateStockAdjustmentDto dto);
    Task<bool> DeleteAsync(int id);
}
