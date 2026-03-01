using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface IReportService
{
    // Financial Reports
    Task<InstallmentCollectionReportDto> GetInstallmentCollectionReportAsync(DateTime? from, DateTime? to);
    Task<OutstandingBalanceReportDto> GetOutstandingBalanceReportAsync();
    Task<DailyCashFlowReportDto> GetDailyCashFlowReportAsync(DateTime? from, DateTime? to);
    Task<InstallmentProfitLossDto> GetInstallmentProfitLossAsync(DateTime? from, DateTime? to);

    // Customer Reports
    Task<CustomerLedgerDto> GetCustomerLedgerAsync(int customerId);
    Task<DefaultersReportDto> GetDefaultersReportAsync();
    Task<PaymentHistoryReportDto> GetPaymentHistoryReportAsync(int? customerId, DateTime? from, DateTime? to);

    // Sales Reports
    Task<InstallmentSalesSummaryDto> GetInstallmentSalesSummaryAsync(DateTime? from, DateTime? to);
    Task<ProductSalesReportDto> GetProductSalesReportAsync(DateTime? from, DateTime? to);

    // Risk & Compliance
    Task<DefaultRateReportDto> GetDefaultRateReportAsync();
    Task<RecoveryPerformanceDto> GetRecoveryPerformanceAsync(DateTime? from, DateTime? to);

    // Operational Reports
    Task<DueTodayReportDto> GetDueTodayReportAsync();
    Task<UpcomingDueReportDto> GetUpcomingDueReportAsync(int days = 7);
    Task<LateFeeReportDto> GetLateFeeReportAsync(DateTime? from, DateTime? to);

    // Product Profit Report
    Task<ProductProfitReportDto> GetProductProfitReportAsync(DateTime? from, DateTime? to);

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
    Task<List<IncomeReportItemDto>> GetIncomeReportAsync(DateTime? from, DateTime? to);
    Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime? from, DateTime? to);
    Task<AnnualReportDto> GetAnnualReportAsync(int year);
}
