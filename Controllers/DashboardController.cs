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

    [HttpGet]
    public async Task<IActionResult> GetDashboardData()
    {
        var today = GetPakistanNow();
        var todayStr = today.ToString("yyyy-MM-dd");
        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var lastMonthEnd = thisMonthStart.AddDays(-1);

        // ── Installment Plans ──
        var allPlans = await _db.InstallmentPlans
            .Include(p => p.Customer)
            .Include(p => p.Product)
            .ToListAsync();

        var totalPlans = allPlans.Count;
        var activePlans = allPlans.Count(p => p.Status == "active");
        var completedPlans = allPlans.Count(p => p.Status == "completed");
        var defaultedPlans = allPlans.Count(p => p.Status == "defaulted");
        var cancelledPlans = allPlans.Count(p => p.Status == "cancelled");

        var totalFinancedAmount = allPlans.Sum(p => p.FinancedAmount);
        var totalDownPayments = allPlans.Sum(p => p.DownPayment);
        var totalExpectedRevenue = allPlans.Sum(p => p.TotalPayable);
        var totalInterestExpected = allPlans.Sum(p => p.TotalInterest);

        // Plans created this month vs last month
        var plansThisMonth = allPlans.Count(p => p.CreatedAt >= thisMonthStart);
        var plansLastMonth = allPlans.Count(p => p.CreatedAt >= lastMonthStart && p.CreatedAt < thisMonthStart);

        // ── Repayment Entries (installments) ──
        var allEntries = await _db.RepaymentEntries.ToListAsync();

        bool TryParseDueDate(string? dueDate, out DateTime parsedDate)
        {
            parsedDate = default;
            if (string.IsNullOrWhiteSpace(dueDate)) return false;

            var trimmed = dueDate.Trim();
            return DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate)
                || DateTime.TryParse(trimmed, out parsedDate);
        }

        // "paid" and "partial" are settled states kept from DB; all others are derived from DueDate at runtime.
        // overdue  → due date has passed and entry is unpaid
        // due      → due date is today
        // upcoming → due tomorrow, day after tomorrow, or further in the future
        string ResolveEntryStatus(Models.RepaymentEntry entry)
        {
            if (string.Equals(entry.Status, "paid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.Status, "partial", StringComparison.OrdinalIgnoreCase))
            {
                return entry.Status!.ToLowerInvariant();
            }

            if (TryParseDueDate(entry.DueDate, out var dueDate))
            {
                if (dueDate.Date < today.Date) return "overdue";
                if (dueDate.Date == today.Date) return "due";
                return "upcoming"; // tomorrow, day after tomorrow, or any future date
            }

            return "upcoming";
        }

        var paidEntries = allEntries.Where(e => ResolveEntryStatus(e) == "paid").ToList();
        var partialEntries = allEntries.Where(e => ResolveEntryStatus(e) == "partial").ToList();
        var overdueEntries = allEntries.Where(e => ResolveEntryStatus(e) == "overdue").ToList();
        var dueEntries = allEntries.Where(e => ResolveEntryStatus(e) == "due").ToList();
        var upcomingEntries = allEntries.Where(e => ResolveEntryStatus(e) == "upcoming").ToList();

        var totalCollected = paidEntries.Sum(e => (e.EmiAmount)) +
                     partialEntries.Sum(e => (e.ActualPaidAmount ?? 0)) +
                     partialEntries.Sum(e => (e.MiscAdjustedAmount ?? 0));

var totalOutstanding = allPlans
    .Where(p => p.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
    .Sum(p =>
    {
        // Get all entries for this plan
        var planEntries = allEntries.Where(e => e.PlanId == p.Id);

        // Total paid for this plan (Paid + Partial)
        var paidForPlan = planEntries
            .Where(e => e.Status?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true)
            .Sum(e => e.EmiAmount);

        var partialPaidForPlan = planEntries
            .Where(e => e.Status?.Equals("Partial", StringComparison.OrdinalIgnoreCase) == true)
            .Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));

        var totalPaidForPlan = paidForPlan + partialPaidForPlan;

        // Outstanding for this plan
        return (p.TotalPayable - (p.DownPayment) - totalPaidForPlan);
    });

        var overdueAmount = overdueEntries.Sum(e => e.EmiAmount - (e.ActualPaidAmount ?? 0) - (e.MiscAdjustedAmount ?? 0));

        // Collections this month
        var collectionsThisMonth = paidEntries
            .Where(e => !string.IsNullOrEmpty(e.PaidDate) && DateTime.TryParse(e.PaidDate, out var pd) && pd >= thisMonthStart)
            .Sum(e => (e.EmiAmount) );
        var collectionsLastMonth = paidEntries
            .Where(e => !string.IsNullOrEmpty(e.PaidDate) && DateTime.TryParse(e.PaidDate, out var pd) && pd >= lastMonthStart && pd < thisMonthStart)
            .Sum(e => (e.EmiAmount) );

        var collectionsThisMonthPartial = partialEntries
            .Where(e => !string.IsNullOrEmpty(e.PaidDate) && DateTime.TryParse(e.PaidDate, out var pd) && pd >= thisMonthStart)
            .Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));
        var collectionsLastMonthPartial = partialEntries
            .Where(e => !string.IsNullOrEmpty(e.PaidDate) && DateTime.TryParse(e.PaidDate, out var pd) && pd >= lastMonthStart && pd < thisMonthStart)
            .Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));
        collectionsThisMonth = collectionsThisMonth + collectionsThisMonthPartial;
        collectionsLastMonth = collectionsLastMonth + collectionsLastMonthPartial;
        // ── Customers ──
        var totalCustomers = await _db.Parties.CountAsync(p => p.Role == "Customer");
        var customersThisMonth = await _db.Parties.CountAsync(p => p.Role == "Customer" && p.CreatedAt >= thisMonthStart);

        // ── Monthly collection trend (last 12 months) ──
        var monthlyCollections = new List<object>();
        for (int i = 11; i >= 0; i--)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            var monthLabel = monthStart.ToString("MMM yyyy");

            var collected = paidEntries
                .Where(e => !string.IsNullOrEmpty(e.PaidDate) && DateTime.TryParse(e.PaidDate, out var pd) && pd >= monthStart && pd < monthEnd)
                .Sum(e => (e.EmiAmount) );
            var collectedPartial = partialEntries
                .Where(e => !string.IsNullOrEmpty(e.PaidDate) && DateTime.TryParse(e.PaidDate, out var pd) && pd >= monthStart && pd < monthEnd)
                .Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));

            var plansDue = allEntries
                .Where(e => DateTime.TryParse(e.DueDate, out var dd) && dd >= monthStart && dd < monthEnd)
                .Sum(e => e.EmiAmount);
            collected = collected + collectedPartial;
            monthlyCollections.Add(new { month = monthLabel, collected, expected = plansDue });
        }

        // ── Due today list (strictly current date only) ──
        var upcomingDues = allEntries
            .Where(e =>
            {
                var status = ResolveEntryStatus(e);
                if (status != "due") return false;
                return TryParseDueDate(e.DueDate, out var dd) && dd.Date == today.Date;
            })
            .OrderBy(e => e.DueDate)
            .Take(10)
            .Select(e =>
            {
                var resolvedStatus = ResolveEntryStatus(e);
                var plan = allPlans.FirstOrDefault(p => p.Id == e.PlanId);
                return new
                {
                    planId = e.PlanId,
                    installmentNo = e.InstallmentNo,
                    dueDate = e.DueDate,
                    emiAmount = e.EmiAmount,
                    customerName = plan?.Customer?.FullName ?? "Unknown",
                    customerPhone = plan?.Customer?.Phone ?? "",
                    productName = plan?.Product?.ProductName ?? "Unknown",
                    status = resolvedStatus
                };
            })
            .Where(e => e.status == "due")
            .ToList();

        // ── Overdue installments ──
        var overdueList = allEntries
            .Where(e => ResolveEntryStatus(e) == "overdue")
            .OrderBy(e => e.DueDate)
            .Select(e =>
            {
                var plan = allPlans.FirstOrDefault(p => p.Id == e.PlanId);
                var remaining = e.EmiAmount - (e.ActualPaidAmount ?? 0) - (e.MiscAdjustedAmount ?? 0);
                return new
                {
                    planId = e.PlanId,
                    installmentNo = e.InstallmentNo,
                    dueDate = e.DueDate,
                    emiAmount = e.EmiAmount,
                    remaining,
                    customerName = plan?.Customer?.FullName ?? "Unknown",
                    customerPhone = plan?.Customer?.Phone ?? "",
                    productName = plan?.Product?.ProductName ?? "Unknown"
                };
            }).ToList();

        // ── Upcoming installments ──
        var upcomingList = allEntries
            .Where(e => ResolveEntryStatus(e) == "upcoming")
            .OrderBy(e => e.DueDate)
            .Take(15)
            .Select(e =>
            {
                var plan = allPlans.FirstOrDefault(p => p.Id == e.PlanId);
                return new
                {
                    planId = e.PlanId,
                    installmentNo = e.InstallmentNo,
                    dueDate = e.DueDate,
                    emiAmount = e.EmiAmount,
                    customerName = plan?.Customer?.FullName ?? "Unknown",
                    customerPhone = plan?.Customer?.Phone ?? "",
                    productName = plan?.Product?.ProductName ?? "Unknown",
                    status = "upcoming"
                };
            }).ToList();

        // ── Recent payments ──
       var recentPayments = allEntries
    .Where(e => !string.IsNullOrEmpty(e.PaidDate))
    .OrderByDescending(e => e.PaidDate)
    .Take(10)
    .Select(e =>
    {
        var plan = allPlans.FirstOrDefault(p => p.Id == e.PlanId);

        decimal amount = 0;

        if (e.Status?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true)
        {
            amount = e.EmiAmount;
        }
        else if (e.Status?.Equals("Partial", StringComparison.OrdinalIgnoreCase) == true)
        {
            amount = (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0);
        }

        return new
        {
            planId = e.PlanId,
            installmentNo = e.InstallmentNo,
            paidDate = e.PaidDate,
            amount = amount,
            customerName = plan?.Customer?.FullName ?? "Unknown",
            productName = plan?.Product?.ProductName ?? "Unknown"
        };
    })
    .ToList();
        

        // ── Recent plans ──
        var recentPlans = allPlans
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new
            {
                id = p.Id,
                customerName = p.Customer?.FullName ?? "Unknown",
                customerPhone = p.Customer?.Phone ?? "",
                productName = p.Product?.ProductName ?? "Unknown",
                financedAmount = p.FinancedAmount,
                emiAmount = p.EmiAmount,
                tenure = p.Tenure,
                status = p.Status,
                createdAt = p.CreatedAt.ToString("yyyy-MM-dd")
            }).ToList();

        // ── Plan status distribution ──
        var statusDistribution = new
        {
            active = activePlans,
            completed = completedPlans,
            defaulted = defaultedPlans,
            cancelled = cancelledPlans
        };

        // ── Percentage changes ──
        decimal plansPctChange = plansLastMonth > 0 ? Math.Round(((decimal)(plansThisMonth - plansLastMonth) / plansLastMonth) * 100, 1) : 0;
        decimal collectionsPctChange = collectionsLastMonth > 0 ? Math.Round(((collectionsThisMonth - collectionsLastMonth) / collectionsLastMonth) * 100, 1) : 0;

        return Ok(new
        {
            // KPI Cards
            totalPlans,
            activePlans,
            completedPlans,
            totalCustomers,
            customersThisMonth,
            totalFinancedAmount,
            totalDownPayments,
            totalExpectedRevenue,
            totalInterestExpected,
            totalCollected,
            totalOutstanding,
            overdueAmount,
            overdueCount = overdueEntries.Count,
            dueCount = dueEntries.Count,
            upcomingCount = upcomingEntries.Count,

            // Trends
            plansThisMonth,
            plansLastMonth,
            plansPctChange,
            collectionsThisMonth,
            collectionsLastMonth,
            collectionsPctChange,

            // Status distribution (for donut chart)
            statusDistribution,

            // Monthly trend (for bar chart)
            monthlyCollections,

            // Lists
            upcomingDues,
            overdueList,
            upcomingList,
            recentPayments,
            recentPlans
        });
    }

    /// <summary>
    /// POS Business Dashboard — sales, purchases, expenses, returns, products, customers.
    /// Separate from installment dashboard.
    /// </summary>
    [HttpGet("pos")]
    public async Task<IActionResult> GetPosDashboardData()
    {
        var today = DateTime.UtcNow;
        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // ── Sales ──
        var allSales = await _db.Sales.ToListAsync();
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
        var bestSellers = await _db.SaleItems
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
        var recentSales = await _db.Sales
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
