using System.Text.Json;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;
using static ReactPosApi.UnitTests.ACCT.Seed;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>Reports read only the ledger (Services/ACCT/LedgerReports.cs)</summary>
public class LedgerReportsTests
{
    private static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>bought 10 x 100 (owed), sold 4 x 250 + tax 50, customer paid 500 of 1050, expense 75, capital 5000</summary>
    private static (TestDb t, Sale sale) Books()
    {
        var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 10, 100, Day.AddDays(-5));
        var sale = Sale(t.Db, pid, 4, 250, Day, orderTax: 50);
        SalePayment(t.Db, sale.Id, 500, date: Day.AddDays(1));
        var cat = new ExpenseCategory { Name = "Rent" };
        t.Db.ExpenseCategories.Add(cat); t.Db.SaveChanges();
        t.Db.Expenses.Add(new Expense { Reference = "E", ExpenseCategoryId = cat.Id, Date = Day, Amount = 75 });
        t.Db.FinanceEntries.Add(new FinanceEntry { Type = FinanceEntryType.OwnerInvestment, Name = "Owner", Amount = 5000, Method = "Cash", Date = Day.AddDays(-10) });
        t.Db.SaveChanges();
        return (t, sale);
    }

    [Fact]
    public async Task Trial_balance_debits_equal_credits()
    {
        var (t, _) = Books();
        using (t)
        {
            var tb = Json(await new LedgerReports(t.Fresh()).TrialBalanceAsync(null, null, default));
            Assert.True(tb.GetProperty("balanced").GetBoolean());
            Assert.Equal(tb.GetProperty("totals").GetProperty("closingDebit").GetDecimal(), tb.GetProperty("totals").GetProperty("closingCredit").GetDecimal());
        }
    }

    [Fact]
    public async Task Trial_balance_opening_balance_is_what_came_before_the_period()
    {
        var (t, _) = Books();
        using (t)
        {
            var tb = Json(await new LedgerReports(t.Fresh()).TrialBalanceAsync(Day, null, default));
            var cash = tb.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "151001");
            Assert.Equal(5000, cash.GetProperty("opening").GetDecimal());
            Assert.Equal(5000 - 75 + 500, cash.GetProperty("closing").GetDecimal());
        }
    }

    [Fact]
    public async Task Profit_and_loss_accrual_counts_the_sale_when_made_with_its_cost()
    {
        var (t, _) = Books();
        using (t)
        {
            var pl = Json(await new LedgerReports(t.Fresh()).ProfitAndLossAsync(Day.AddDays(-30), Day.AddDays(30), "accrual", default));
            Assert.Equal(1000, pl.GetProperty("totalIncome").GetDecimal());          // tax is not income
            Assert.Equal(400 + 75, pl.GetProperty("totalExpenses").GetDecimal());    // cost of goods sold, not the purchase
            Assert.Equal(600, pl.GetProperty("grossProfit").GetDecimal());
            Assert.Equal(525, pl.GetProperty("netProfit").GetDecimal());
        }
    }

    [Fact]
    public async Task Profit_and_loss_cash_counts_the_paid_share_of_the_sale()
    {
        var (t, _) = Books();
        using (t)
        {
            var reports = new LedgerReports(t.Fresh());
            var pl = Json(await reports.ProfitAndLossAsync(Day.AddDays(-30), Day.AddDays(30), "cash", default));
            var share = 500m / 1050m;
            Assert.Equal(Math.Round(1000 * share, 2), pl.GetProperty("totalIncome").GetDecimal());
            Assert.Equal(Math.Round((1000 - 400) * share - 75, 2), pl.GetProperty("netProfit").GetDecimal(), 2);
            // before the payment: nothing of the sale yet
            var before = Json(await reports.ProfitAndLossAsync(Day.AddDays(-30), Day, "cash", default));
            Assert.Equal(0, before.GetProperty("totalIncome").GetDecimal());
        }
    }

    [Fact]
    public async Task Balance_sheet_balances_and_shows_profit_to_date()
    {
        var (t, _) = Books();
        using (t)
        {
            var bs = Json(await new LedgerReports(t.Fresh()).BalanceSheetAsync(Day.AddDays(30), default));
            Assert.True(bs.GetProperty("balanced").GetBoolean());
            Assert.Equal(bs.GetProperty("totalAssets").GetDecimal(), bs.GetProperty("totalLiabilitiesAndEquity").GetDecimal());
            var profit = bs.GetProperty("equity").EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Profit to date");
            Assert.Equal(525, profit.GetProperty("amount").GetDecimal());
        }
    }

    [Fact]
    public async Task Balance_sheet_keeps_an_advance_on_its_own_side()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        var a = Sale(t.Db, pid, 1, 100, Day, customer: "Owes");
        var b = Sale(t.Db, pid, 1, 100, Day, customer: "Paid ahead");
        SalePayment(t.Db, b.Id, 300);
        var bs = Json(await new LedgerReports(t.Fresh()).BalanceSheetAsync(Day.AddDays(1), default));
        var owe = bs.GetProperty("assets").EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Customers owe you");
        var adv = bs.GetProperty("liabilities").EnumerateArray().Single(r => r.GetProperty("name").GetString()!.StartsWith("Customers paid in advance"));
        Assert.Equal(100, owe.GetProperty("amount").GetDecimal());
        Assert.Equal(200, adv.GetProperty("amount").GetDecimal());
        Assert.True(bs.GetProperty("balanced").GetBoolean());
    }

    [Fact]
    public async Task Account_ledger_runs_a_balance_and_filters_by_party()
    {
        var (t, sale) = Books();
        using (t)
        {
            var receivable = t.Db.LedgerAccounts.Single(a => a.SystemKey == LedgerChart.Receivable).Id;
            var ledger = Json(await new LedgerReports(t.Fresh()).AccountLedgerAsync(receivable, null, null, "ali", default));
            var rows = ledger.GetProperty("rows").EnumerateArray().ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(1050, rows[0].GetProperty("balance").GetDecimal());
            Assert.Equal(550, rows[1].GetProperty("balance").GetDecimal());
            var none = Json(await new LedgerReports(t.Fresh()).AccountLedgerAsync(receivable, null, null, "someone else", default));
            Assert.Empty(none.GetProperty("rows").EnumerateArray());
        }
    }

    [Fact]
    public async Task Journal_pages_newest_first()
    {
        var (t, _) = Books();
        using (t)
        {
            var page = Json(await new LedgerReports(t.Fresh()).JournalAsync(null, null, null, null, 1, 10, default));
            var dates = page.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("date").GetDateTime()).ToList();
            Assert.Equal(dates.OrderByDescending(d => d), dates);
            var sales = Json(await new LedgerReports(t.Fresh()).JournalAsync(null, null, VoucherType.Sale, null, 1, 10, default));
            Assert.Equal(1, sales.GetProperty("total").GetInt32());
        }
    }

    [Fact]
    public async Task Customer_balances_list_what_each_owes()
    {
        var (t, _) = Books();
        using (t)
        {
            var parties = Json(await new LedgerReports(t.Fresh()).PartiesAsync(LedgerPartyType.Customer, null, default));
            var ali = parties.EnumerateArray().Single();
            Assert.Equal(550, ali.GetProperty("balance").GetDecimal());
        }
    }

    [Fact]
    public async Task Check_lists_products_changed_without_a_document()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 10, 100, Day);
        var p = t.Db.Products.Single(x => x.Id == pid);
        p.Quantity = 11;                     // the purchase (10) says 10
        t.Db.SaveChanges();
        var mismatches = Json(await new LedgerReports(t.Fresh()).StockMismatchesAsync(default));
        var m = mismatches.EnumerateArray().Single();
        Assert.Equal(1, m.GetProperty("difference").GetDecimal());
    }
}
