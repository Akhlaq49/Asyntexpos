using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;
using static ReactPosApi.UnitTests.ACCT.Seed;
using C = ReactPosApi.Services.LedgerChart;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>Year-based reports (Services/ACCT/LedgerReports.cs): accumulated balances, comparison, GL balances by month</summary>
public class YearReportsTests
{
    private static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static readonly DateTime LastYear = new(2025, 6, 1);   // Day is 10 Mar 2026

    /// <summary>owner put in 5,000 (Jan 2025); rent 100 paid in Jun 2025, rent 75 paid on 10 Mar 2026</summary>
    private static TestDb Books()
    {
        var t = new TestDb();
        var cat = new ExpenseCategory { Name = "Rent" };
        t.Db.ExpenseCategories.Add(cat); t.Db.SaveChanges();
        t.Db.FinanceEntries.Add(new FinanceEntry { Type = FinanceEntryType.OwnerInvestment, Name = "Owner", Amount = 5000, Method = "Cash", Date = new DateTime(2025, 1, 1) });
        t.Db.Expenses.Add(new Expense { Reference = "E1", ExpenseCategoryId = cat.Id, Date = LastYear, Amount = 100 });
        t.Db.Expenses.Add(new Expense { Reference = "E2", ExpenseCategoryId = cat.Id, Date = Day, Amount = 75 });
        t.Db.SaveChanges();
        return t;
    }

    private static int AccountId(TestDb t, string key) => t.Fresh().LedgerAccounts.AsNoTracking().Single(a => a.SystemKey == key).Id;
    private static int ExpenseAccount(TestDb t) =>
        t.Fresh().JournalLines.AsNoTracking().Where(l => l.Debit == 75).Select(l => l.AccountId).Single();

    private static JsonElement Row(JsonElement report, int id) => report.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
    private static decimal Total(JsonElement report, string column) => report.GetProperty("rows").EnumerateArray().Sum(r => r.GetProperty(column).GetDecimal());

    [Theory]
    [InlineData(1, "2026-01-01")]
    [InlineData(7, "2025-07-01")]
    [InlineData(3, "2026-03-01")]
    public void Financial_year_start(int month, string start) =>
        Assert.Equal(DateTime.Parse(start), LedgerReports.FiscalYearStart(Day, month));

    [Fact]
    public async Task Accumulated_balances_carry_balance_sheet_accounts_and_start_income_and_expenses_at_zero()
    {
        using var t = Books();
        var r = Json(await new LedgerReports(t.Fresh()).AccountBalancesAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), default));
        Assert.Equal(new DateTime(2026, 1, 1), r.GetProperty("fiscalYearStart").GetDateTime());

        var rent = Row(r, ExpenseAccount(t));
        Assert.Equal(0, rent.GetProperty("carryforward").GetDecimal());          // last year's 100 is not carried
        Assert.Equal(75, rent.GetProperty("debit").GetDecimal());
        Assert.Equal(75, rent.GetProperty("accumulated").GetDecimal());

        var cash = Row(r, AccountId(t, C.Cash));
        Assert.Equal(4900, cash.GetProperty("carryforward").GetDecimal());
        Assert.Equal(75, cash.GetProperty("credit").GetDecimal());
        Assert.Equal(4825, cash.GetProperty("accumulated").GetDecimal());

        var earlier = Row(r, 0);                                                   // last year's rent, now profit of earlier years
        Assert.Equal(LedgerReports.EarlierProfitName, earlier.GetProperty("name").GetString());
        Assert.Equal(100, earlier.GetProperty("carryforward").GetDecimal());

        Assert.Equal(0, Total(r, "carryforward"));                                 // every column balances
        Assert.Equal(0, Total(r, "accumulated"));
        Assert.Equal(Total(r, "debit"), Total(r, "credit"));
        Assert.Equal(Total(r, "cumulativeDebit"), Total(r, "cumulativeCredit"));
    }

    [Fact]
    public async Task Accumulated_balances_show_the_earlier_periods_of_the_year_apart()
    {
        using var t = Books();
        var r = Json(await new LedgerReports(t.Fresh()).AccountBalancesAsync(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), default));
        var rent = Row(r, ExpenseAccount(t));
        Assert.Equal(75, rent.GetProperty("previousPeriods").GetDecimal());      // March is an earlier period of the year
        Assert.Equal(75, rent.GetProperty("accumulatedPrevious").GetDecimal());
        Assert.Equal(0, rent.GetProperty("debit").GetDecimal());
        Assert.Equal(75, rent.GetProperty("cumulativeDebit").GetDecimal());
    }

    [Fact]
    public async Task Comparison_puts_a_period_next_to_another_with_the_difference()
    {
        using var t = Books();
        var r = Json(await new LedgerReports(t.Fresh()).ComparisonAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31),
            new DateTime(2025, 6, 1), new DateTime(2025, 6, 30), default));
        var rent = Row(r, ExpenseAccount(t));                                      // expenses: the period itself
        Assert.Equal(75, rent.GetProperty("current").GetDecimal());
        Assert.Equal(100, rent.GetProperty("comparison").GetDecimal());
        Assert.Equal(-25, rent.GetProperty("difference").GetDecimal());
        Assert.Equal(-25, rent.GetProperty("percent").GetDecimal());

        var cash = Row(r, AccountId(t, C.Cash));                                   // balance sheet: at the end of the period
        Assert.Equal(4825, cash.GetProperty("current").GetDecimal());
        Assert.Equal(4900, cash.GetProperty("comparison").GetDecimal());

        Assert.Equal(0, Total(r, "current"));                                      // both columns balance
        Assert.Equal(0, Total(r, "comparison"));
    }

    [Fact]
    public async Task Gl_balances_by_month_with_carryforward_and_cumulative_balance()
    {
        using var t = Books();
        int cashId = AccountId(t, C.Cash), rentId = ExpenseAccount(t);
        var reports = new LedgerReports(t.Fresh());
        var cash = Json(await reports.GlBalancesAsync(new[] { cashId }, 2026, default));
        Assert.Equal(4900, cash.GetProperty("carryforward").GetDecimal());
        var march = cash.GetProperty("periods").EnumerateArray().Single(p => p.GetProperty("period").GetInt32() == 3);
        Assert.Equal(75, march.GetProperty("credit").GetDecimal());
        Assert.Equal(-75, march.GetProperty("balance").GetDecimal());
        Assert.Equal(4825, march.GetProperty("cumulative").GetDecimal());
        Assert.Equal(4825, cash.GetProperty("totals").GetProperty("closing").GetDecimal());
        Assert.Equal(12, cash.GetProperty("periods").GetArrayLength());

        var rent = Json(await reports.GlBalancesAsync(new[] { rentId }, 2026, default));
        Assert.Equal(0, rent.GetProperty("carryforward").GetDecimal());           // an expense account starts the year at zero

        var both = Json(await reports.GlBalancesAsync(new[] { rentId, cashId }, 2026, default));
        Assert.Equal(2, both.GetProperty("accounts").GetArrayLength());           // added together: rent + cash
        Assert.Equal(4900, both.GetProperty("totals").GetProperty("closing").GetDecimal());
    }

    [Fact]
    public async Task A_financial_year_starting_in_july_moves_the_carryforward()
    {
        using var t = Books();
        var db = t.Fresh();
        db.AccountingSettings.Single().FiscalYearStartMonth = 7; db.SaveChanges();
        var r = Json(await new LedgerReports(t.Fresh()).AccountBalancesAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), default));
        Assert.Equal(new DateTime(2025, 7, 1), r.GetProperty("fiscalYearStart").GetDateTime());
        Assert.Equal(100, Row(r, 0).GetProperty("carryforward").GetDecimal());    // June 2025 belongs to the year before
    }
}
