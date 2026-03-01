using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IMiscellaneousRegisterService
{
    Task<decimal> GetBalanceAsync(int customerId);
    Task<List<MiscellaneousRegisterDto>> GetByCustomerAsync(int customerId);
    Task<MiscellaneousRegisterDto> CreateAsync(CreateMiscTransactionDto dto);
    Task<bool> DeleteAsync(int id);
    Task<object> GetSummaryAsync();
}
