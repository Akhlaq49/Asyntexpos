using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using System.Globalization;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private static readonly TimeZoneInfo PakistanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");

    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    private static DateTime GetPakistanNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PakistanTimeZone);

    /// <summary>
    /// POS Business Dashboard — sales, purchases, expenses, returns, products, customers.
    /// </summary>
    [HttpGet("pos")]
    public async Task<IActionResult> GetPosDashboardData()
    {
        var today = DateTime.UtcNow;
        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // ── Sales ──
        var allSales = await _db.Sales.Where(s => s.Status != ReactPosApi.Models.SaleStatus.Queued).ToListAsync();   // a queued order counts once marked as sold
        var totalSalesAmount = allSales.Sum(s => s.GrandTotal);
        var salesThisMonth = allSales.Where(s => s.SaleDate >= thisMonthStart).Sum(s => s.GrandTotal);
        var salesLastMonth = allSales.Where(s => s.SaleDate >= lastMonthStart && s.SaleDate < thisMonthStart).Sum(s => s.GrandTotal);
        var salesPctChange = salesLastMonth > 0 ? Math.Round(((salesThisMonth - salesLastMonth) / salesLastMonth) * 100, 1) : 0;
        var totalSalesCount = allSales.Count;
        var salesCountThisMonth = allSales.Count(s => s.SaleDate >= thisMonthStart);
        var totalSalesDue = allSales.Sum(s => s.Due);

        // ── Purchases ──
        var allPurchases = await _db.Purchases.ToListAsync();
        var totalPurchaseAmount = allPurchases.Sum(p => p.Total);
        var purchasesThisMonth = allPurchases.Where(p => p.Date >= thisMonthStart).Sum(p => p.Total);
        var purchasesLastMonth = allPurchases.Where(p => p.Date >= lastMonthStart && p.Date < thisMonthStart).Sum(p => p.Total);
        var purchasePctChange = purchasesLastMonth > 0 ? Math.Round(((purchasesThisMonth - purchasesLastMonth) / purchasesLastMonth) * 100, 1) : 0;

        // ── Expenses ──
        var allExpenses = await _db.Expenses.ToListAsync();
        var totalExpenseAmount = allExpenses.Sum(e => e.Amount);
        var expensesThisMonth = allExpenses.Where(e => e.Date >= thisMonthStart).Sum(e => e.Amount);
        var expensesLastMonth = allExpenses.Where(e => e.Date >= lastMonthStart && e.Date < thisMonthStart).Sum(e => e.Amount);
        var expensePctChange = expensesLastMonth > 0 ? Math.Round(((expensesThisMonth - expensesLastMonth) / expensesLastMonth) * 100, 1) : 0;

        // ── Sales Returns ──
        var allReturns = await _db.SalesReturns.ToListAsync();
        var totalReturnAmount = allReturns.Sum(r => r.GrandTotal);
        var returnsThisMonth = allReturns.Where(r => r.ReturnDate >= thisMonthStart).Sum(r => r.GrandTotal);
        var returnsLastMonth = allReturns.Where(r => r.ReturnDate >= lastMonthStart && r.ReturnDate < thisMonthStart).Sum(r => r.GrandTotal);
        var returnPctChange = returnsLastMonth > 0 ? Math.Round(((returnsThisMonth - returnsLastMonth) / returnsLastMonth) * 100, 1) : 0;

        // ── Customers ──
        var totalCustomers = await _db.Parties.CountAsync(p => p.Role == "Customer");
        var customersThisMonth = await _db.Parties.CountAsync(p => p.Role == "Customer" && p.CreatedAt >= thisMonthStart);

        // ── Products / Inventory ──
        var totalProducts = await _db.Products.CountAsync();
        var lowStockProducts = await _db.Products.CountAsync(p => p.Quantity <= p.QuantityAlert && p.QuantityAlert > 0);

        // ── Monthly sales vs purchase trend (last 12 months) ──
        var monthlyTrend = new List<object>();
        for (int i = 11; i >= 0; i--)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            var monthLabel = monthStart.ToString("MMM yyyy");

            var mSales = allSales.Where(s => s.SaleDate >= monthStart && s.SaleDate < monthEnd).Sum(s => s.GrandTotal);
            var mPurchases = allPurchases.Where(p => p.Date >= monthStart && p.Date < monthEnd).Sum(p => p.Total);

            monthlyTrend.Add(new { month = monthLabel, sales = mSales, purchases = mPurchases });
        }

        // ── Best selling products (by quantity sold) ──
        var bestSellers = await _db.SaleItems.Where(si => si.Sale!.Status != ReactPosApi.Models.SaleStatus.Queued)
            .GroupBy(si => si.ProductName)
            .Select(g => new
            {
                productName = g.Key,
                totalQty = g.Sum(x => x.Quantity),
                totalRevenue = g.Sum(x => x.TotalCost)
            })
            .OrderByDescending(x => x.totalQty)
            .Take(5)
            .ToListAsync();

        // ── Recent sales ──
        var recentSales = await _db.Sales.Where(s => s.Status != ReactPosApi.Models.SaleStatus.Queued)
            .OrderByDescending(s => s.SaleDate)
            .Take(10)
            .Select(s => new
            {
                id = s.Id,
                reference = s.Reference,
                customerName = s.CustomerName,
                grandTotal = s.GrandTotal,
                paid = s.Paid,
                due = s.Due,
                status = s.Status,
                paymentStatus = s.PaymentStatus,
                saleDate = s.SaleDate
            })
            .ToListAsync();

        // ── Expense by category (top 5) ──
        var expenseByCategory = await _db.Expenses
            .Include(e => e.ExpenseCategory)
            .GroupBy(e => e.ExpenseCategory!.Name)
            .Select(g => new
            {
                category = g.Key ?? "Uncategorized",
                total = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.total)
            .Take(5)
            .ToListAsync();

        // ── Profit calculation ──
        var totalProfit = totalSalesAmount - totalPurchaseAmount - totalExpenseAmount - totalReturnAmount;

        return Ok(new
        {
            // KPI Cards
            totalSalesAmount,
            totalPurchaseAmount,
            totalReturnAmount,
            totalExpenseAmount,

            // Changes
            salesPctChange,
            purchasePctChange,
            returnPctChange,
            expensePctChange,

            // This month values
            salesThisMonth,
            purchasesThisMonth,
            expensesThisMonth,
            returnsThisMonth,

            // Counts
            totalSalesCount,
            salesCountThisMonth,
            totalSalesDue,
            totalCustomers,
            customersThisMonth,
            totalProducts,
            lowStockProducts,
            totalProfit,

            // Charts / Lists
            monthlyTrend,
            bestSellers,
            recentSales,
            expenseByCategory
        });
    }
}
