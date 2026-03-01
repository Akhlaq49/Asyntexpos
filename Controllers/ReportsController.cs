using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // ── Financial Reports ──

    [HttpGet("installment-collection")]
    public async Task<IActionResult> GetInstallmentCollection([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetInstallmentCollectionReportAsync(from, to);
        return Ok(report);
    }

    [HttpGet("outstanding-balance")]
    public async Task<IActionResult> GetOutstandingBalance()
    {
        var report = await _reportService.GetOutstandingBalanceReportAsync();
        return Ok(report);
    }

    [HttpGet("daily-cash-flow")]
    public async Task<IActionResult> GetDailyCashFlow([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetDailyCashFlowReportAsync(from, to);
        return Ok(report);
    }

    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetProfitLoss([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetInstallmentProfitLossAsync(from, to);
        return Ok(report);
    }

    // ── Customer Reports ──

    [HttpGet("customer-ledger/{customerId}")]
    public async Task<IActionResult> GetCustomerLedger(int customerId)
    {
        try
        {
            var report = await _reportService.GetCustomerLedgerAsync(customerId);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("defaulters")]
    public async Task<IActionResult> GetDefaulters()
    {
        var report = await _reportService.GetDefaultersReportAsync();
        return Ok(report);
    }

    [HttpGet("payment-history")]
    public async Task<IActionResult> GetPaymentHistory([FromQuery] int? customerId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetPaymentHistoryReportAsync(customerId, from, to);
        return Ok(report);
    }

    // ── Sales Reports ──

    [HttpGet("installment-sales-summary")]
    public async Task<IActionResult> GetInstallmentSalesSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetInstallmentSalesSummaryAsync(from, to);
        return Ok(report);
    }

    [HttpGet("product-sales")]
    public async Task<IActionResult> GetProductSales([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetProductSalesReportAsync(from, to);
        return Ok(report);
    }

    // ── Risk & Compliance ──

    [HttpGet("default-rate")]
    public async Task<IActionResult> GetDefaultRate()
    {
        var report = await _reportService.GetDefaultRateReportAsync();
        return Ok(report);
    }

    [HttpGet("recovery-performance")]
    public async Task<IActionResult> GetRecoveryPerformance([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetRecoveryPerformanceAsync(from, to);
        return Ok(report);
    }

    // ── Operational Reports ──

    [HttpGet("due-today")]
    public async Task<IActionResult> GetDueToday()
    {
        var report = await _reportService.GetDueTodayReportAsync();
        return Ok(report);
    }

    [HttpGet("upcoming-due")]
    public async Task<IActionResult> GetUpcomingDue([FromQuery] int days = 7)
    {
        var report = await _reportService.GetUpcomingDueReportAsync(days);
        return Ok(report);
    }

    [HttpGet("late-fees")]
    public async Task<IActionResult> GetLateFees([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetLateFeeReportAsync(from, to);
        return Ok(report);
    }

    // ── Product Profit Report ──

    [HttpGet("product-profit")]
    public async Task<IActionResult> GetProductProfit([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _reportService.GetProductProfitReportAsync(from, to);
        return Ok(report);
    }

    // ═══════════════════════════════════════════════════════════
    // STANDARD POS REPORTS
    // ═══════════════════════════════════════════════════════════

    [HttpGet("sales-report")]
    public async Task<IActionResult> GetSalesReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetSalesReportAsync(from, to));

    [HttpGet("best-sellers")]
    public async Task<IActionResult> GetBestSellers([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetBestSellersAsync(from, to));

    [HttpGet("purchase-report")]
    public async Task<IActionResult> GetPurchaseReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetPurchaseReportAsync(from, to));

    [HttpGet("inventory-report")]
    public async Task<IActionResult> GetInventoryReport()
        => Ok(await _reportService.GetInventoryReportAsync());

    [HttpGet("invoice-report")]
    public async Task<IActionResult> GetInvoiceReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetInvoiceReportAsync(from, to));

    [HttpGet("supplier-report")]
    public async Task<IActionResult> GetSupplierReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetSupplierReportAsync(from, to));

    [HttpGet("supplier-due-report")]
    public async Task<IActionResult> GetSupplierDueReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetSupplierDueReportAsync(from, to));

    [HttpGet("customer-report")]
    public async Task<IActionResult> GetCustomerReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetCustomerReportAsync(from, to));

    [HttpGet("customer-due-report")]
    public async Task<IActionResult> GetCustomerDueReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetCustomerDueReportAsync(from, to));

    [HttpGet("product-report")]
    public async Task<IActionResult> GetProductReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetProductReportAsync(from, to));

    [HttpGet("product-expiry-report")]
    public async Task<IActionResult> GetProductExpiryReport()
        => Ok(await _reportService.GetProductExpiryReportAsync());

    [HttpGet("product-qty-alert")]
    public async Task<IActionResult> GetProductQtyAlert()
        => Ok(await _reportService.GetProductQtyAlertReportAsync());

    [HttpGet("expense-report")]
    public async Task<IActionResult> GetExpenseReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetExpenseReportAsync(from, to));

    [HttpGet("income-report")]
    public async Task<IActionResult> GetIncomeReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetIncomeReportAsync(from, to));

    [HttpGet("profit-and-loss")]
    public async Task<IActionResult> GetProfitAndLoss([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetProfitAndLossAsync(from, to));

    [HttpGet("annual-report")]
    public async Task<IActionResult> GetAnnualReport([FromQuery] int? year)
        => Ok(await _reportService.GetAnnualReportAsync(year ?? DateTime.UtcNow.Year));
}
