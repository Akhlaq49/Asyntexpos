using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public class FinanceReportService : IFinanceReportService
{
    private readonly AppDbContext _db;
    public FinanceReportService(AppDbContext db) => _db = db;

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(DateTime? date, string? store)
    {
        var asOf = date ?? DateTime.UtcNow;

        // Assets: Bank account balances + total income
        var bankBalances = await _db.BankAccounts
            .Where(b => b.Status == "active")
            .Select(b => new BalanceSheetLineItem { AccountName = $"Bank - {b.BankName} ({b.HolderName})", Amount = b.OpeningBalance })
            .ToListAsync();

        var totalIncome = await _db.FinanceIncomes
            .Where(i => i.Date <= asOf && (string.IsNullOrEmpty(store) || i.Store == store))
            .SumAsync(i => (decimal?)i.Amount) ?? 0;

        var totalExpenses = await _db.Expenses
            .Where(e => e.Date <= asOf)
            .SumAsync(e => (decimal?)e.Amount) ?? 0;

        var assets = new List<BalanceSheetLineItem>();
        assets.AddRange(bankBalances);
        assets.Add(new BalanceSheetLineItem { AccountName = "Cash from Income", Amount = totalIncome });

        // Liabilities: total expenses
        var liabilities = new List<BalanceSheetLineItem>
        {
            new() { AccountName = "Accounts Payable (Expenses)", Amount = totalExpenses }
        };

        var totalAssets = assets.Sum(a => a.Amount);
        var totalLiabilities = liabilities.Sum(l => l.Amount);

        return new BalanceSheetDto
        {
            Assets = assets,
            TotalAssets = totalAssets,
            Liabilities = liabilities,
            TotalLiabilities = totalLiabilities,
            Equity = totalAssets - totalLiabilities
        };
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(DateTime? from, DateTime? to, string? store)
    {
        var startDate = from ?? DateTime.MinValue;
        var endDate = to ?? DateTime.UtcNow;

        var items = new List<TrialBalanceLineItem>();

        // Bank accounts (debit)
        var banks = await _db.BankAccounts
            .Where(b => b.Status == "active")
            .Select(b => new { b.BankName, b.HolderName, b.OpeningBalance })
            .ToListAsync();
        foreach (var b in banks)
            items.Add(new TrialBalanceLineItem { AccountName = $"Bank - {b.BankName} ({b.HolderName})", Debit = b.OpeningBalance });

        // Income accounts (credit)
        var incomeByCategory = await _db.FinanceIncomes
            .Include(i => i.IncomeCategory)
            .Where(i => i.Date >= startDate && i.Date <= endDate && (string.IsNullOrEmpty(store) || i.Store == store))
            .GroupBy(i => i.IncomeCategory!.Name)
            .Select(g => new { Category = g.Key, Total = g.Sum(i => i.Amount) })
            .ToListAsync();
        foreach (var ic in incomeByCategory)
            items.Add(new TrialBalanceLineItem { AccountName = $"Income - {ic.Category}", Credit = ic.Total });

        // Expense accounts (debit)
        var expenseByCategory = await _db.Expenses
            .Include(e => e.ExpenseCategory)
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .GroupBy(e => e.ExpenseCategory!.Name)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync();
        foreach (var ec in expenseByCategory)
            items.Add(new TrialBalanceLineItem { AccountName = $"Expense - {ec.Category}", Debit = ec.Total });

        return new TrialBalanceDto
        {
            Items = items,
            TotalDebit = items.Sum(i => i.Debit),
            TotalCredit = items.Sum(i => i.Credit)
        };
    }

    public async Task<CashFlowDto> GetCashFlowAsync(DateTime? from, DateTime? to, string? store)
    {
        var startDate = from ?? DateTime.MinValue;
        var endDate = to ?? DateTime.UtcNow;

        // Operating: Income received - Expenses paid
        var incomeByCategory = await _db.FinanceIncomes
            .Include(i => i.IncomeCategory)
            .Where(i => i.Date >= startDate && i.Date <= endDate && (string.IsNullOrEmpty(store) || i.Store == store))
            .GroupBy(i => i.IncomeCategory!.Name)
            .Select(g => new CashFlowLineItem { Description = $"Income - {g.Key}", Amount = g.Sum(i => i.Amount) })
            .ToListAsync();

        var expenseByCategory = await _db.Expenses
            .Include(e => e.ExpenseCategory)
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .GroupBy(e => e.ExpenseCategory!.Name)
            .Select(g => new CashFlowLineItem { Description = $"Expense - {g.Key}", Amount = -g.Sum(e => e.Amount) })
            .ToListAsync();

        var operating = new List<CashFlowLineItem>();
        operating.AddRange(incomeByCategory);
        operating.AddRange(expenseByCategory);

        // Sales as operating cash inflow
        var totalSales = await _db.Sales
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
            .SumAsync(s => (decimal?)s.GrandTotal) ?? 0;
        if (totalSales > 0)
            operating.Insert(0, new CashFlowLineItem { Description = "Sales Revenue", Amount = totalSales });

        // Purchases as operating cash outflow
        var totalPurchases = await _db.Purchases
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
            .SumAsync(p => (decimal?)p.Total) ?? 0;
        if (totalPurchases > 0)
            operating.Add(new CashFlowLineItem { Description = "Purchases", Amount = -totalPurchases });

        var totalOperating = operating.Sum(o => o.Amount);

        // Investing and Financing are placeholders for now
        var investing = new List<CashFlowLineItem>();
        var financing = new List<CashFlowLineItem>();

        return new CashFlowDto
        {
            OperatingActivities = operating,
            TotalOperating = totalOperating,
            InvestingActivities = investing,
            TotalInvesting = 0,
            FinancingActivities = financing,
            TotalFinancing = 0,
            NetCashFlow = totalOperating
        };
    }

    public async Task<AccountStatementDto> GetAccountStatementAsync(int? accountId, DateTime? from, DateTime? to)
    {
        var startDate = from ?? DateTime.MinValue;
        var endDate = to ?? DateTime.UtcNow;

        decimal runningBalance = 0;

        // If an account is selected, start with opening balance
        if (accountId.HasValue)
        {
            var account = await _db.BankAccounts.FindAsync(accountId.Value);
            if (account != null) runningBalance = account.OpeningBalance;
        }

        var entries = new List<AccountStatementEntry>();

        // Income entries
        var incomes = await _db.FinanceIncomes
            .Include(i => i.IncomeCategory)
            .Where(i => i.Date >= startDate && i.Date <= endDate
                && (!accountId.HasValue || i.Account == _db.BankAccounts.Where(b => b.Id == accountId.Value).Select(b => b.BankName).FirstOrDefault()))
            .OrderBy(i => i.Date)
            .ToListAsync();

        foreach (var i in incomes)
        {
            runningBalance += i.Amount;
            entries.Add(new AccountStatementEntry
            {
                ReferenceNumber = i.Reference,
                Date = i.Date.ToString("dd MMM yyyy"),
                Category = i.IncomeCategory?.Name ?? "",
                Description = i.Notes,
                Amount = i.Amount,
                TransactionType = "Credit",
                Balance = runningBalance
            });
        }

        // Expense entries
        var expenses = await _db.Expenses
            .Include(e => e.ExpenseCategory)
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .OrderBy(e => e.Date)
            .ToListAsync();

        foreach (var e in expenses)
        {
            runningBalance -= e.Amount;
            entries.Add(new AccountStatementEntry
            {
                ReferenceNumber = e.Reference,
                Date = e.Date.ToString("dd MMM yyyy"),
                Category = e.ExpenseCategory?.Name ?? "",
                Description = e.Description,
                Amount = e.Amount,
                TransactionType = "Debit",
                Balance = runningBalance
            });
        }

        // Sort by date
        entries = entries.OrderBy(e => DateTime.ParseExact(e.Date, "dd MMM yyyy", null)).ToList();

        // Recalculate running balance in order
        decimal bal = accountId.HasValue
            ? (await _db.BankAccounts.FindAsync(accountId.Value))?.OpeningBalance ?? 0
            : 0;
        foreach (var entry in entries)
        {
            if (entry.TransactionType == "Credit") bal += entry.Amount;
            else bal -= entry.Amount;
            entry.Balance = bal;
        }

        return new AccountStatementDto
        {
            Entries = entries,
            ClosingBalance = bal
        };
    }
}
