using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public class ReportService : IReportService
{
    private static readonly TimeZoneInfo PakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");

    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    private static DateTime GetPakistanTodayDate() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PakistanTimeZone).Date;

    private static string GetPakistanTodayString() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PakistanTimeZone).ToString("yyyy-MM-dd");

    // ═══════════════════════════════════════════════════════════
    // STANDARD POS REPORTS
    // ═══════════════════════════════════════════════════════════

    public async Task<SalesReportDto> GetSalesReportAsync(DateTime? from, DateTime? to)
    {
        var sales = await _db.Sales.Include(s => s.Items).ToListAsync();
        if (from.HasValue) sales = sales.Where(s => s.SaleDate >= from.Value).ToList();
        if (to.HasValue) sales = sales.Where(s => s.SaleDate <= to.Value.AddDays(1)).ToList();

        var products = await _db.Products.Include(p => p.Images).ToListAsync();
        var productMap = products.ToDictionary(p => p.Id);

        var grouped = sales.SelectMany(s => s.Items).GroupBy(i => i.ProductId).Select(g => {
            var prod = productMap.GetValueOrDefault(g.Key);
            return new SalesReportItemDto
            {
                Sku = prod?.SKU ?? "", ProductName = g.First().ProductName,
                ProductImage = prod?.Images.FirstOrDefault()?.ImagePath ?? "",
                Brand = prod?.Brand ?? "", Category = prod?.Category ?? "",
                SoldQty = g.Sum(i => i.Quantity), SoldAmount = g.Sum(i => i.TotalCost),
                InStockQty = prod?.Quantity ?? 0
            };
        }).OrderByDescending(x => x.SoldAmount).ToList();

        var overdue = sales.Where(s => s.PaymentStatus == "Overdue").Sum(s => s.Due);
        return new SalesReportDto
        {
            TotalAmount = sales.Sum(s => s.GrandTotal), TotalPaid = sales.Sum(s => s.Paid),
            TotalUnpaid = sales.Sum(s => s.Due), Overdue = overdue, Items = grouped
        };
    }

    public async Task<List<SalesReportItemDto>> GetBestSellersAsync(DateTime? from, DateTime? to)
    {
        var report = await GetSalesReportAsync(from, to);
        return report.Items.OrderByDescending(i => i.SoldQty).ToList();
    }

    public async Task<PurchaseReportDto> GetPurchaseReportAsync(DateTime? from, DateTime? to)
    {
        var purchases = await _db.Purchases.Include(p => p.Items).ToListAsync();
        if (from.HasValue) purchases = purchases.Where(p => p.Date >= from.Value).ToList();
        if (to.HasValue) purchases = purchases.Where(p => p.Date <= to.Value.AddDays(1)).ToList();

        var products = await _db.Products.Include(p => p.Images).ToListAsync();
        var productMap = products.ToDictionary(p => p.Id);
        // Purchases created from Manage Stock have no ProductId on the line item, so fall back to the product name
        var productByName = products.GroupBy(p => p.ProductName.Trim().ToLower()).ToDictionary(g => g.Key, g => g.First());

        var items = purchases.SelectMany(p => p.Items!.Select(i => {
            var prod = i.ProductId.HasValue ? productMap.GetValueOrDefault(i.ProductId.Value) : null;
            prod ??= productByName.GetValueOrDefault((i.ProductName ?? "").Trim().ToLower());
            return new PurchaseReportItemDto
            {
                Reference = p.Reference, Sku = prod?.SKU ?? "",
                ProductImage = prod?.Images.FirstOrDefault()?.ImagePath ?? "",
                DueDate = p.Date.ToString("yyyy-MM-dd"), ProductName = i.ProductName,
                Category = prod?.Category ?? "", InStockQty = prod?.Quantity ?? 0,
                PurchaseQty = i.Quantity, PurchaseAmount = i.TotalCost
            };
        })).ToList();

        return new PurchaseReportDto { Items = items };
    }

    public async Task<List<InventoryReportItemDto>> GetInventoryReportAsync()
    {
        var products = await _db.Products.ToListAsync();
        return products.Select(p => new InventoryReportItemDto
        {
            Sku = p.SKU, ProductName = p.ProductName, Category = p.Category,
            Unit = p.Unit, InStock = p.Quantity
        }).OrderBy(p => p.ProductName).ToList();
    }

    public async Task<InvoiceReportDto> GetInvoiceReportAsync(DateTime? from, DateTime? to)
    {
        var invoices = await _db.Invoices.ToListAsync();
        if (from.HasValue) invoices = invoices.Where(i => i.DueDate >= from.Value).ToList();
        if (to.HasValue) invoices = invoices.Where(i => i.DueDate <= to.Value.AddDays(1)).ToList();

        var overdue = invoices.Where(i => i.DueDate < DateTime.UtcNow &&
                 i.AmountDue > 0)
        .Sum(i => i.AmountDue);

        var items = invoices.Select(i => new InvoiceReportItemDto
        {
            Id = i.Id, InvoiceNo = i.InvoiceNo, CustomerName = i.CustomerName,
            DueDate = i.DueDate.ToString(), Amount = i.TotalAmount, Paid = i.Paid,
            AmountDue = i.AmountDue, Status = i.Status
        }).OrderByDescending(i => i.DueDate).ToList();

        return new InvoiceReportDto
        {
            TotalAmount = invoices.Sum(i => i.TotalAmount), TotalPaid = invoices.Sum(i => i.Paid),
            TotalUnpaid = invoices.Sum(i => i.AmountDue), Overdue = overdue, Items = items
        };
    }

    public async Task<List<SupplierReportItemDto>> GetSupplierReportAsync(DateTime? from, DateTime? to)
    {
        var purchases = await _db.Purchases.Include(p => p.Items).ToListAsync();
        if (from.HasValue) purchases = purchases.Where(p => p.Date >= from.Value).ToList();
        if (to.HasValue) purchases = purchases.Where(p => p.Date <= to.Value.AddDays(1)).ToList();

        return purchases.Select(p => new SupplierReportItemDto
        {
            Reference = p.Reference, Id = p.Id, Supplier = p.SupplierName,
            TotalItems = p.Items?.Count ?? 0, Amount = p.Total,
            PaymentMethod = "Cash", Status = p.Status
        }).OrderByDescending(p => p.Id).ToList();
    }

    public async Task<List<SupplierDueReportItemDto>> GetSupplierDueReportAsync(DateTime? from, DateTime? to)
    {
        var purchases = await _db.Purchases.ToListAsync();
        if (from.HasValue) purchases = purchases.Where(p => p.Date >= from.Value).ToList();
        if (to.HasValue) purchases = purchases.Where(p => p.Date <= to.Value.AddDays(1)).ToList();

        return purchases.Where(p => p.Total - p.Paid > 0 || p.PaymentStatus != "Paid").Select(p => new SupplierDueReportItemDto
        {
            Reference = p.Reference, Id = p.Id, Supplier = p.SupplierName,
            TotalAmount = p.Total, Paid = p.Paid, Due = p.Total - p.Paid,
            Status = p.PaymentStatus
        }).OrderByDescending(p => p.Due).ToList();
    }

    public async Task<List<CustomerReportItemDto>> GetCustomerReportAsync(DateTime? from, DateTime? to)
    {
        var sales = await _db.Sales.Include(s => s.Payments).ToListAsync();
        if (from.HasValue) sales = sales.Where(s => s.SaleDate >= from.Value).ToList();
        if (to.HasValue) sales = sales.Where(s => s.SaleDate <= to.Value.AddDays(1)).ToList();

        var grouped = sales.GroupBy(s => new { s.CustomerId, s.CustomerName }).Select(g => {
            var first = g.First();
            var payMethod = first.Payments?.FirstOrDefault()?.PaymentType ?? "Cash";
            return new CustomerReportItemDto
            {
                Reference = first.Reference,
                Code = first.CustomerId?.ToString() ?? "",
                Customer = g.Key.CustomerName, TotalOrders = g.Count(),
                Amount = g.Sum(s => s.GrandTotal), PaymentMethod = payMethod,
                Status = g.All(s => s.PaymentStatus == "Paid") ? "Completed" : "Pending"
            };
        }).OrderByDescending(c => c.Amount).ToList();
        return grouped;
    }

    public async Task<List<CustomerDueReportItemDto>> GetCustomerDueReportAsync(DateTime? from, DateTime? to)
    {
        var sales = await _db.Sales.ToListAsync();
        if (from.HasValue) sales = sales.Where(s => s.SaleDate >= from.Value).ToList();
        if (to.HasValue) sales = sales.Where(s => s.SaleDate <= to.Value.AddDays(1)).ToList();

        var grouped = sales.Where(s => s.Due > 0).GroupBy(s => new { s.CustomerId, s.CustomerName }).Select(g => {
            var first = g.First();
            return new CustomerDueReportItemDto
            {
                Reference = first.Reference, Code = first.CustomerId?.ToString() ?? "",
                Customer = g.Key.CustomerName, TotalAmount = g.Sum(s => s.GrandTotal),
                Paid = g.Sum(s => s.Paid), Due = g.Sum(s => s.Due),
                Status = g.Any(s => s.PaymentStatus == "Overdue") ? "Overdue" : "Unpaid"
            };
        }).OrderByDescending(c => c.Due).ToList();
        return grouped;
    }

    public async Task<List<ProductReportItemDto>> GetProductReportAsync(DateTime? from, DateTime? to)
    {
        var products = await _db.Products.ToListAsync();
        var saleItems = await _db.SaleItems.Include(si => si.Sale).ToListAsync();
        if (from.HasValue) saleItems = saleItems.Where(si => si.Sale?.SaleDate >= from.Value).ToList();
        if (to.HasValue) saleItems = saleItems.Where(si => si.Sale?.SaleDate <= to.Value.AddDays(1)).ToList();

        var soldMap = saleItems.GroupBy(si => si.ProductId)
            .ToDictionary(g => g.Key, g => new { Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.TotalCost) });

        return products.Select(p => {
            var sold = soldMap.GetValueOrDefault(p.Id);
            return new ProductReportItemDto
            {
                Sku = p.SKU, ProductName = p.ProductName, Category = p.Category,
                Brand = p.Brand, Qty = p.Quantity, Price = p.Price,
                TotalOrdered = sold?.Qty ?? 0, Revenue = sold?.Revenue ?? 0
            };
        }).OrderByDescending(p => p.Revenue).ToList();
    }

    public async Task<List<ProductExpiryReportItemDto>> GetProductExpiryReportAsync()
    {
        var products = await _db.Products.Where(p => !string.IsNullOrEmpty(p.ExpiryDate)).ToListAsync();
        return products.Select(p => new ProductExpiryReportItemDto
        {
            Sku = p.SKU, SerialNo = p.ItemBarcode ?? "", ProductName = p.ProductName,
            ManufacturedDate = p.ManufacturedDate ?? "", ExpiredDate = p.ExpiryDate ?? ""
        }).OrderBy(p => p.ExpiredDate).ToList();
    }

    public async Task<List<ProductQtyAlertItemDto>> GetProductQtyAlertReportAsync()
    {
        var products = await _db.Products.Where(p => p.Quantity <= p.QuantityAlert).ToListAsync();
        return products.Select(p => new ProductQtyAlertItemDto
        {
            Sku = p.SKU, SerialNo = p.ItemBarcode ?? "", ProductName = p.ProductName,
            TotalQuantity = p.Quantity, AlertQuantity = p.QuantityAlert
        }).OrderBy(p => p.TotalQuantity).ToList();
    }

    public async Task<List<ExpenseReportItemDto>> GetExpenseReportAsync(DateTime? from, DateTime? to)
    {
        // the expenses recorded under Finance > Expenses (purchases have their own Purchase Report)
        var q = _db.Expenses.Include(e => e.ExpenseCategory).AsQueryable();
        if (from.HasValue) q = q.Where(e => e.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(e => e.Date < to.Value.Date.AddDays(1));
        var expenses = await q.OrderByDescending(e => e.Date).ToListAsync();

        return expenses.Select(e => new ExpenseReportItemDto
        {
            ExpenseName = e.ExpenseName,
            Category = e.ExpenseCategory?.Name ?? "",
            Description = e.Description ?? "",
            Date = e.Date.ToString("yyyy-MM-dd"), Amount = e.Amount,
            Status = string.IsNullOrEmpty(e.Status) ? "" : char.ToUpper(e.Status[0]) + e.Status[1..]
        }).ToList();
    }

    public async Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime? from, DateTime? to)
    {
        var startDate = from ?? DateTime.UtcNow.AddMonths(-5);
        var endDate = to ?? DateTime.UtcNow;

        var sales = await _db.Sales.Include(s => s.Items).ToListAsync();
        var purchases = await _db.Purchases.ToListAsync();
        var returns = await _db.SalesReturns.ToListAsync();
        var expenses = await _db.Expenses.ToListAsync();

        var months = new List<ProfitAndLossMonthDto>();
        var current = new DateTime(startDate.Year, startDate.Month, 1);
        while (current <= endDate)
        {
            var monthEnd = current.AddMonths(1);
            var monthSales = sales.Where(s => s.SaleDate>= current && s.SaleDate < monthEnd);
            var monthPurchases = purchases.Where(p => p.Date >= current && p.Date < monthEnd);
            var monthReturns = returns.Where(r => r.ReturnDate >= current && r.ReturnDate < monthEnd);

            var salesTotal = monthSales.Sum(s => s.GrandTotal);
            var purchaseTotal = monthPurchases.Sum(p => p.Total);
            var returnTotal = monthReturns.Sum(r => r.GrandTotal);
            var expenseTotal = expenses.Where(e => e.Date >= current && e.Date < monthEnd).Sum(e => e.Amount);

            months.Add(new ProfitAndLossMonthDto
            {
                Month = current.ToString("MMM yyyy"), Sales = salesTotal, Service = 0,
                PurchaseReturn = 0, GrossProfit = salesTotal,
                PurchaseExpense = purchaseTotal, SalesReturn = returnTotal, OtherExpense = expenseTotal,
                TotalExpense = purchaseTotal + returnTotal + expenseTotal,
                NetProfit = salesTotal - purchaseTotal - returnTotal - expenseTotal
            });
            current = monthEnd;
        }
        return new ProfitAndLossDto { Months = months };
    }

    public async Task<AnnualReportDto> GetAnnualReportAsync(int year)
    {
        var sales = await _db.Sales.ToListAsync();
        var purchases = await _db.Purchases.ToListAsync();
        var returns = await _db.SalesReturns.ToListAsync();
        var expenses = await _db.Expenses.ToListAsync();

        var months = new List<AnnualReportMonthDto>();
        for (int m = 1; m <= 12; m++)
        {
            var start = new DateTime(year, m, 1);
            var end = start.AddMonths(1);
            var monthSales = sales
    .Select(s => new
    {
        Sale = s,
        ParsedDate = s.SaleDate != null ? s.SaleDate : (DateTime?)null
    })
    .Where(x => x.ParsedDate >= start && x.ParsedDate < end)
    .Sum(x => x.Sale.GrandTotal);
            var monthPurchases = purchases.Where(p => p.Date >= start && p.Date < end).Sum(p => p.Total);
            var monthReturns = returns.Where(r => r.ReturnDate >= start && r.ReturnDate < end).Sum(r => r.GrandTotal);

            var monthExpenses = expenses.Where(e => e.Date >= start && e.Date < end).Sum(e => e.Amount);

            months.Add(new AnnualReportMonthDto
            {
                Month = start.ToString("MMMM yyyy"),
                Sales = monthSales, Purchases = monthPurchases, Expenses = monthExpenses,
                Returns = monthReturns, Profit = monthSales - monthPurchases - monthReturns - monthExpenses
            });
        }

        return new AnnualReportDto
        {
            Year = year, Months = months,
            Totals = new AnnualReportMonthDto
            {
                Month = "Total",
                Sales = months.Sum(m => m.Sales), Purchases = months.Sum(m => m.Purchases), Expenses = months.Sum(m => m.Expenses),
                Returns = months.Sum(m => m.Returns), Profit = months.Sum(m => m.Profit)
            }
        };
    }

}
