namespace ReactPosApi.DTOs;

// ═══════════════════════════════════════════════════════════
// 1. FINANCIAL REPORTS
// ═══════════════════════════════════════════════════════════

// 1a. Installment Collection Report
public class InstallmentCollectionReportDto
{
    public int TotalInstallmentsDue { get; set; }
    public int TotalCollected { get; set; }
    public int PendingCount { get; set; }
    public int LatePayments { get; set; }
    public decimal TotalAmountDue { get; set; }
    public decimal TotalAmountCollected { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal LateAmount { get; set; }
    public List<CollectionByDateDto> CollectionByDate { get; set; } = new();
}

public class CollectionByDateDto
{
    public string Date { get; set; } = "";
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

// 1b. Outstanding Balance Report
public class OutstandingBalanceReportDto
{
    public decimal TotalOutstanding { get; set; }
    public decimal TotalOverdue { get; set; }
    public int TotalCustomers { get; set; }
    public AgingBucketDto Aging { get; set; } = new();
    public List<CustomerOutstandingDto> Customers { get; set; } = new();
}

public class AgingBucketDto
{
    public decimal Days0To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Days90Plus { get; set; }
    public int Count0To30 { get; set; }
    public int Count31To60 { get; set; }
    public int Count61To90 { get; set; }
    public int Count90Plus { get; set; }
}

public class CustomerOutstandingDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal OverdueAmount { get; set; }
    public int MaxDaysOverdue { get; set; }
    public int PlanId { get; set; }
    public string ProductName { get; set; } = "";
}

// 1c. Daily Cash Flow Report
public class DailyCashFlowReportDto
{
    public string Date { get; set; } = "";
    public decimal OpeningBalance { get; set; }
    public decimal CashCollected { get; set; }
    public decimal OnlinePayments { get; set; }
    public decimal DownPayments { get; set; }
    public decimal Expenses { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<DailyCashFlowEntryDto> DailyEntries { get; set; } = new();
}

public class DailyCashFlowEntryDto
{
    public string Date { get; set; } = "";
    public decimal CashCollected { get; set; }
    public decimal OnlinePayments { get; set; }
    public decimal DownPayments { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetFlow { get; set; }
}

// 1d. Profit & Loss Report (Installment-focused)
public class InstallmentProfitLossDto
{
    public decimal TotalSales { get; set; }
    public decimal TotalDownPayments { get; set; }
    public decimal InterestEarned { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal BadDebts { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal GrossRevenue { get; set; }
    public List<MonthlyProfitDto> MonthlyBreakdown { get; set; } = new();
}

public class MonthlyProfitDto
{
    public string Month { get; set; } = "";
    public decimal Collections { get; set; }
    public decimal Interest { get; set; }
    public decimal DownPayments { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit { get; set; }
}


// ═══════════════════════════════════════════════════════════
// 2. CUSTOMER REPORTS
// ═══════════════════════════════════════════════════════════

// 2a. Customer Ledger
public class CustomerLedgerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal TotalPenalties { get; set; }
    public List<LedgerTransactionDto> Transactions { get; set; } = new();
}

public class LedgerTransactionDto
{
    public string Date { get; set; } = "";
    public string Type { get; set; } = ""; // Down Payment, Installment, Misc Adjustment, Penalty
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
    public string? Reference { get; set; }
}

// 2b. Defaulters Report
public class DefaultersReportDto
{
    public int TotalDefaulters { get; set; }
    public decimal TotalDefaultedAmount { get; set; }
    public List<DefaulterDto> Defaulters { get; set; } = new();
}

public class DefaulterDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int PlanId { get; set; }
    public string ProductName { get; set; } = "";
    public int MissedInstallments { get; set; }
    public decimal OverdueAmount { get; set; }
    public int MaxDaysOverdue { get; set; }
    public string Status { get; set; } = ""; // overdue, partial
    public string? LastPaidDate { get; set; }
}

// 2c. Payment History Report
public class PaymentHistoryReportDto
{
    public int TotalPayments { get; set; }
    public decimal TotalAmount { get; set; }
    public List<PaymentHistoryEntryDto> Payments { get; set; } = new();
    public List<PaymentMethodSummaryDto> MethodSummary { get; set; } = new();
}

public class PaymentHistoryEntryDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public int PlanId { get; set; }
    public int InstallmentNo { get; set; }
    public string PaidDate { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? MiscAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string ProductName { get; set; } = "";
}

public class PaymentMethodSummaryDto
{
    public string Method { get; set; } = "";
    public int Count { get; set; }
    public decimal Amount { get; set; }
}


// ═══════════════════════════════════════════════════════════
// 3. SALES REPORTS (Installment-focused)
// ═══════════════════════════════════════════════════════════

// 3a. Sales Summary Report
public class InstallmentSalesSummaryDto
{
    public int TotalContracts { get; set; }
    public int ActiveContracts { get; set; }
    public int CompletedContracts { get; set; }
    public int CancelledContracts { get; set; }
    public decimal TotalDownPayments { get; set; }
    public decimal TotalFinancedAmount { get; set; }
    public decimal TotalRevenue { get; set; }
    public List<TenureSummaryDto> TenureBreakdown { get; set; } = new();
    public List<MonthlySalesDto> MonthlySales { get; set; } = new();
}

public class TenureSummaryDto
{
    public string TenureLabel { get; set; } = ""; // "3 months", "6 months", "12 months"
    public int Tenure { get; set; }
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public class MonthlySalesDto
{
    public string Month { get; set; } = "";
    public int Contracts { get; set; }
    public decimal DownPayments { get; set; }
    public decimal FinancedAmount { get; set; }
}

// 3b. Product-wise Sales Report
public class ProductSalesReportDto
{
    public int TotalProducts { get; set; }
    public int TotalUnitsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public List<ProductSalesItemDto> Products { get; set; } = new();
}

public class ProductSalesItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
    public int UnitsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal DownPaymentCollected { get; set; }
}


// ═══════════════════════════════════════════════════════════
// 4. RISK & COMPLIANCE REPORTS
// ═══════════════════════════════════════════════════════════

// 4a. Default Rate Report
public class DefaultRateReportDto
{
    public int TotalFinancedCustomers { get; set; }
    public int NumberOfDefaulters { get; set; }
    public decimal DefaultPercentage { get; set; }
    public decimal TotalFinancedAmount { get; set; }
    public decimal DefaultedAmount { get; set; }
    public List<DefaultTrendDto> MonthlyTrend { get; set; } = new();
}

public class DefaultTrendDto
{
    public string Month { get; set; } = "";
    public int TotalActive { get; set; }
    public int NewDefaults { get; set; }
    public decimal DefaultRate { get; set; }
}

// 4b. Recovery Performance Report
public class RecoveryPerformanceDto
{
    public decimal TotalOverdueAmount { get; set; }
    public decimal AmountRecovered { get; set; }
    public decimal RecoveryRate { get; set; }
    public int TotalOverdueEntries { get; set; }
    public int RecoveredEntries { get; set; }
    public List<MonthlyRecoveryDto> MonthlyRecovery { get; set; } = new();
}

public class MonthlyRecoveryDto
{
    public string Month { get; set; } = "";
    public decimal OverdueAmount { get; set; }
    public decimal Recovered { get; set; }
    public decimal RecoveryRate { get; set; }
}


// ═══════════════════════════════════════════════════════════
// 5. OPERATIONAL REPORTS
// ═══════════════════════════════════════════════════════════

// 5a. Installments Due Today
public class DueTodayReportDto
{
    public int TotalDueToday { get; set; }
    public decimal TotalAmountDue { get; set; }
    public List<DueTodayItemDto> Items { get; set; } = new();
}

public class DueTodayItemDto
{
    public int PlanId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int InstallmentNo { get; set; }
    public decimal AmountDue { get; set; }
    public string DueDate { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Status { get; set; } = "";
}

// 5b. Upcoming Due Report (Next 7 Days)
public class UpcomingDueReportDto
{
    public int TotalUpcoming { get; set; }
    public decimal TotalAmountDue { get; set; }
    public List<DueTodayItemDto> Items { get; set; } = new();
}

// 5c. Late Fee Report
public class LateFeeReportDto
{
    public decimal TotalLateFees { get; set; }
    public decimal PaidLateFees { get; set; }
    public decimal UnpaidLateFees { get; set; }
    public int TotalLateEntries { get; set; }
    public List<LateFeeItemDto> Items { get; set; } = new();
}

public class LateFeeItemDto
{
    public int PlanId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public int InstallmentNo { get; set; }
    public string DueDate { get; set; } = "";
    public string? PaidDate { get; set; }
    public int DaysLate { get; set; }
    public decimal LateFeeAmount { get; set; }
    public bool IsPaid { get; set; }
}


// ═══════════════════════════════════════════════════════════
// 6. PRODUCT PROFIT REPORT
// ═══════════════════════════════════════════════════════════

public class ProductProfitReportDto
{
    public int TotalPlans { get; set; }
    public decimal TotalProductCost { get; set; }
    public decimal TotalFinancedAmount { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalInterestEarned { get; set; }
    public decimal TotalDownPayments { get; set; }
    public decimal AverageProfitPerPlan { get; set; }
    public List<ProductProfitItemDto> Plans { get; set; } = new();
}

public class ProductProfitItemDto
{
    public int PlanId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductImage { get; set; }
    public decimal ProductPrice { get; set; }
    public decimal FinancedAmount { get; set; }
    public decimal TotalPayable { get; set; }
    public decimal DownPayment { get; set; }
    public decimal InterestEarned { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitPercentage { get; set; }
    public string Status { get; set; } = "";
    public string StartDate { get; set; } = "";
    public int Tenure { get; set; }
    public decimal InterestRate { get; set; }
}


// ═══════════════════════════════════════════════════════════
// 7. STANDARD POS REPORTS
// ═══════════════════════════════════════════════════════════

// 7a. Sales Report
public class SalesReportDto
{
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalUnpaid { get; set; }
    public decimal Overdue { get; set; }
    public List<SalesReportItemDto> Items { get; set; } = new();
}
public class SalesReportItemDto
{
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Category { get; set; } = "";
    public int SoldQty { get; set; }
    public decimal SoldAmount { get; set; }
    public int InStockQty { get; set; }
}

// 7b. Purchase Report
public class PurchaseReportDto
{
    public List<PurchaseReportItemDto> Items { get; set; } = new();
}
public class PurchaseReportItemDto
{
    public string Reference { get; set; } = "";
    public string Sku { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Category { get; set; } = "";
    public int InStockQty { get; set; }
    public decimal PurchaseQty { get; set; }
    public decimal PurchaseAmount { get; set; }
}

// 7c. Inventory Report
public class InventoryReportItemDto
{
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "";
    public int InStock { get; set; }
}

// 7d. Invoice Report
public class InvoiceReportDto
{
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalUnpaid { get; set; }
    public decimal Overdue { get; set; }
    public List<InvoiceReportItemDto> Items { get; set; } = new();
}
public class InvoiceReportItemDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string DueDate { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public decimal AmountDue { get; set; }
    public string Status { get; set; } = "";
}

// 7e. Supplier Report
public class SupplierReportItemDto
{
    public string Reference { get; set; } = "";
    public int Id { get; set; }
    public string Supplier { get; set; } = "";
    public int TotalItems { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string Status { get; set; } = "";
}

// 7f. Supplier Due Report
public class SupplierDueReportItemDto
{
    public string Reference { get; set; } = "";
    public int Id { get; set; }
    public string Supplier { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public string Status { get; set; } = "";
}

// 7g. Customer Report
public class CustomerReportItemDto
{
    public string Reference { get; set; } = "";
    public string Code { get; set; } = "";
    public string Customer { get; set; } = "";
    public int TotalOrders { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string Status { get; set; } = "";
}

// 7h. Customer Due Report
public class CustomerDueReportItemDto
{
    public string Reference { get; set; } = "";
    public string Code { get; set; } = "";
    public string Customer { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public string Status { get; set; } = "";
}

// 7i. Product Report
public class ProductReportItemDto
{
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Brand { get; set; } = "";
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public int TotalOrdered { get; set; }
    public decimal Revenue { get; set; }
}

// 7j. Product Expiry Report
public class ProductExpiryReportItemDto
{
    public string Sku { get; set; } = "";
    public string SerialNo { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ManufacturedDate { get; set; } = "";
    public string ExpiredDate { get; set; } = "";
}

// 7k. Product Quantity Alert Report
public class ProductQtyAlertItemDto
{
    public string Sku { get; set; } = "";
    public string SerialNo { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int TotalQuantity { get; set; }
    public int AlertQuantity { get; set; }
}

// 7l. Expense Report
public class ExpenseReportItemDto
{
    public string ExpenseName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Date { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}

// 7m. Income Report
public class IncomeReportItemDto
{
    public string Reference { get; set; } = "";
    public string Date { get; set; } = "";
    public string Store { get; set; } = "";
    public string Category { get; set; } = "";
    public string Notes { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "";
}

// 7n. Profit & Loss Report
public class ProfitAndLossDto
{
    public List<ProfitAndLossMonthDto> Months { get; set; } = new();
}
public class ProfitAndLossMonthDto
{
    public string Month { get; set; } = "";
    public decimal Sales { get; set; }
    public decimal Service { get; set; }
    public decimal PurchaseReturn { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal PurchaseExpense { get; set; }
    public decimal SalesReturn { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetProfit { get; set; }
}

// 7o. Annual Report
public class AnnualReportDto
{
    public int Year { get; set; }
    public List<AnnualReportMonthDto> Months { get; set; } = new();
    public AnnualReportMonthDto Totals { get; set; } = new();
}
public class AnnualReportMonthDto
{
    public string Month { get; set; } = "";
    public decimal Sales { get; set; }
    public decimal Purchases { get; set; }
    public decimal Returns { get; set; }
    public decimal Profit { get; set; }
}
