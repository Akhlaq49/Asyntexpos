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

    [HttpGet("profit-and-loss")]
    public async Task<IActionResult> GetProfitAndLoss([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _reportService.GetProfitAndLossAsync(from, to));

    [HttpGet("annual-report")]
    public async Task<IActionResult> GetAnnualReport([FromQuery] int? year)
        => Ok(await _reportService.GetAnnualReportAsync(year ?? DateTime.UtcNow.Year));

}
