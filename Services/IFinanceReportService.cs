using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IFinanceReportService
{
    Task<BalanceSheetDto> GetBalanceSheetAsync(DateTime? date, string? store);
    Task<TrialBalanceDto> GetTrialBalanceAsync(DateTime? from, DateTime? to, string? store);
    Task<CashFlowDto> GetCashFlowAsync(DateTime? from, DateTime? to, string? store);
    Task<AccountStatementDto> GetAccountStatementAsync(int? accountId, DateTime? from, DateTime? to);
}
