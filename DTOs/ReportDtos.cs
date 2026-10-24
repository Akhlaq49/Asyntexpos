namespace ReactPosApi.DTOs;

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
    public string ProductImage { get; set; } = "";
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
    public string ProductImage { get; set; } = "";
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
    /// <summary>Expenses recorded under Finance > Expenses</summary>
    public decimal OtherExpense { get; set; }
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
    /// <summary>Expenses recorded under Finance > Expenses</summary>
    public decimal Expenses { get; set; }
    public decimal Returns { get; set; }
    public decimal Profit { get; set; }
}
