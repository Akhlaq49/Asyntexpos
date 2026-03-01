using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/finance-reports")]
public class FinanceReportsController : ControllerBase
{
    private readonly IFinanceReportService _service;
    public FinanceReportsController(IFinanceReportService service) => _service = service;

    [HttpGet("balance-sheet")]
    public async Task<ActionResult<BalanceSheetDto>> GetBalanceSheet(
        [FromQuery] DateTime? date, [FromQuery] string? store)
        => Ok(await _service.GetBalanceSheetAsync(date, store));

    [HttpGet("trial-balance")]
    public async Task<ActionResult<TrialBalanceDto>> GetTrialBalance(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? store)
        => Ok(await _service.GetTrialBalanceAsync(from, to, store));

    [HttpGet("cash-flow")]
    public async Task<ActionResult<CashFlowDto>> GetCashFlow(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? store)
        => Ok(await _service.GetCashFlowAsync(from, to, store));

    [HttpGet("account-statement")]
    public async Task<ActionResult<AccountStatementDto>> GetAccountStatement(
        [FromQuery] int? accountId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _service.GetAccountStatementAsync(accountId, from, to));
}
