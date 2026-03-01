using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardData()
    {
        var today = DateTime.UtcNow;
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

        var paidEntries = allEntries.Where(e => e.Status == "paid").ToList();
        var partialEntries = allEntries.Where(e => e.Status == "partial").ToList();
        var overdueEntries = allEntries.Where(e => e.Status == "overdue").ToList();
        var dueEntries = allEntries.Where(e => e.Status == "due").ToList();

        var totalCollected = paidEntries.Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0))
                           + partialEntries.Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));

        var totalOutstanding = allPlans.Where(p => p.Status == "active").Sum(p =>
        {
            var planEntries = allEntries.Where(e => e.PlanId == p.Id);
            var paidForPlan = planEntries.Where(e => e.Status == "paid").Sum(e => e.EmiAmount);
            return p.TotalPayable - p.DownPayment - paidForPlan;
        });

        var overdueAmount = overdueEntries.Sum(e => e.EmiAmount - (e.ActualPaidAmount ?? 0) - (e.MiscAdjustedAmount ?? 0));

        // Collections this month
        var collectionsThisMonth = paidEntries
            .Where(e => !string.IsNullOrEmpty(e.PaidDate) && DateTime.TryParse(e.PaidDate, out var pd) && pd >= thisMonthStart)
            .Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));
        var collectionsLastMonth = paidEntries
            .Where(e => !string.IsNullOrEmpty(e.PaidDate) && DateTime.TryParse(e.PaidDate, out var pd) && pd >= lastMonthStart && pd < thisMonthStart)
            .Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));

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
                .Sum(e => (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0));

            var plansDue = allEntries
                .Where(e => DateTime.TryParse(e.DueDate, out var dd) && dd >= monthStart && dd < monthEnd)
                .Sum(e => e.EmiAmount);

            monthlyCollections.Add(new { month = monthLabel, collected, expected = plansDue });
        }

        // ── Upcoming dues (next 7 days) ──
        var next7Days = todayStr;
        var next7DaysEnd = today.AddDays(7).ToString("yyyy-MM-dd");
        var upcomingDues = allEntries
            .Where(e => (e.Status == "due" || e.Status == "upcoming" || e.Status == "overdue")
                && string.Compare(e.DueDate, todayStr) >= 0
                && string.Compare(e.DueDate, next7DaysEnd) <= 0)
            .OrderBy(e => e.DueDate)
            .Take(10)
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
                    status = e.Status
                };
            }).ToList();

        // ── Overdue installments ──
        var overdueList = allEntries
            .Where(e => e.Status == "overdue")
            .OrderBy(e => e.DueDate)
            .Take(10)
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

        // ── Recent payments ──
        var recentPayments = paidEntries
            .Where(e => !string.IsNullOrEmpty(e.PaidDate))
            .OrderByDescending(e => e.PaidDate)
            .Take(10)
            .Select(e =>
            {
                var plan = allPlans.FirstOrDefault(p => p.Id == e.PlanId);
                return new
                {
                    planId = e.PlanId,
                    installmentNo = e.InstallmentNo,
                    paidDate = e.PaidDate,
                    amount = (e.ActualPaidAmount ?? 0) + (e.MiscAdjustedAmount ?? 0),
                    customerName = plan?.Customer?.FullName ?? "Unknown",
                    productName = plan?.Product?.ProductName ?? "Unknown"
                };
            }).ToList();

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
            recentPayments,
            recentPlans
        });
    }
}
