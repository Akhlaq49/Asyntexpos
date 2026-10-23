using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IReportService
{

    // Standard POS Reports
    Task<SalesReportDto> GetSalesReportAsync(DateTime? from, DateTime? to);
    Task<List<SalesReportItemDto>> GetBestSellersAsync(DateTime? from, DateTime? to);
    Task<PurchaseReportDto> GetPurchaseReportAsync(DateTime? from, DateTime? to);
    Task<List<InventoryReportItemDto>> GetInventoryReportAsync();
    Task<InvoiceReportDto> GetInvoiceReportAsync(DateTime? from, DateTime? to);
    Task<List<SupplierReportItemDto>> GetSupplierReportAsync(DateTime? from, DateTime? to);
    Task<List<SupplierDueReportItemDto>> GetSupplierDueReportAsync(DateTime? from, DateTime? to);
    Task<List<CustomerReportItemDto>> GetCustomerReportAsync(DateTime? from, DateTime? to);
    Task<List<CustomerDueReportItemDto>> GetCustomerDueReportAsync(DateTime? from, DateTime? to);
    Task<List<ProductReportItemDto>> GetProductReportAsync(DateTime? from, DateTime? to);
    Task<List<ProductExpiryReportItemDto>> GetProductExpiryReportAsync();
    Task<List<ProductQtyAlertItemDto>> GetProductQtyAlertReportAsync();
    Task<List<ExpenseReportItemDto>> GetExpenseReportAsync(DateTime? from, DateTime? to);
    Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime? from, DateTime? to);
    Task<AnnualReportDto> GetAnnualReportAsync(int year);

}
