using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;
using static ReactPosApi.UnitTests.ACCT.Seed;
using C = ReactPosApi.Services.LedgerChart;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>Profit and loss on the cash basis: income and expenses count when the money moves (LedgerReports.CashBasisAsync);
/// on the accrual basis when they happen, paid or not</summary>
public class CashBasisTests
{
    private static JsonElement Json(object o) => JsonSerializer.SerializeToElement(o, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task<Func<string, int>> ChartAsync(TestDb t)
    {
        await new LedgerPoster(t.Db).RebuildAsync(default);           // makes the company's chart of accounts
        var ids = t.Fresh().LedgerAccounts.AsNoTracking().ToList();
        return key => ids.Single(a => a.SystemKey == key || a.Code == key).Id;
    }

    private static void Voucher(AppDbContext db, DateTime date, params (int Account, decimal Debit, decimal Credit, string? Party)[] lines)
    {
        var v = new JournalVoucher { Date = date, Reference = $"JV-{db.JournalVouchers.Count() + 1:0000}", Description = "test" };
        var n = 0;
        foreach (var (account, debit, credit, party) in lines)
            v.Lines.Add(new JournalVoucherLine { LineNo = ++n, AccountId = account, Debit = debit, Credit = credit,
                PartyType = party != null ? LedgerPartyType.Supplier : null, PartyName = party });
        db.JournalVouchers.Add(v); db.SaveChanges();
    }

    private static async Task<(decimal Expenses, decimal Net)> PlAsync(AppDbContext db, string basis, DateTime from, DateTime to)
    {
        var pl = Json(await new LedgerReports(db).ProfitAndLossAsync(from, to, basis, default));
        return (pl.GetProperty("totalExpenses").GetDecimal(), pl.GetProperty("netProfit").GetDecimal());
    }

    /// <summary>Office expenses 2,500 owed to the head office (not paid) and IT cost 4,000 paid from the bank</summary>
    [Fact]
    public async Task An_expense_left_owing_counts_on_the_accrual_basis_but_not_on_the_cash_basis_until_it_is_paid()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        Voucher(t.Fresh(), Day, (id("632000"), 2500, 0, null), (id(C.Payable), 0, 2500, "head office"));
        Voucher(t.Fresh(), Day, (id("635000"), 4000, 0, null), (id(C.Bank), 0, 4000, null));
        var month = (Day.AddDays(-9), Day.AddDays(20));

        Assert.Equal((6500m, -6500m), await PlAsync(t.Fresh(), "accrual", month.Item1, month.Item2));     // both happened
        Assert.Equal((4000m, -4000m), await PlAsync(t.Fresh(), "cash", month.Item1, month.Item2));        // only 4,000 was paid

        // the head office is paid 1,000 of it a week later, then the rest: the cash basis follows the payments
        Voucher(t.Fresh(), Day.AddDays(7), (id(C.Payable), 1000, 0, "head office"), (id(C.Cash), 0, 1000, null));
        Assert.Equal(5000m, (await PlAsync(t.Fresh(), "cash", month.Item1, month.Item2)).Expenses);
        Voucher(t.Fresh(), Day.AddDays(40), (id(C.Payable), 1500, 0, "head office"), (id(C.Bank), 0, 1500, null));
        Assert.Equal(5000m, (await PlAsync(t.Fresh(), "cash", month.Item1, month.Item2)).Expenses);                 // paid after the period
        Assert.Equal(1500m, (await PlAsync(t.Fresh(), "cash", Day.AddDays(21), Day.AddDays(60))).Expenses);         // counts when paid
        Assert.Equal(6500m, (await PlAsync(t.Fresh(), "accrual", month.Item1, month.Item2)).Expenses);              // accrual unchanged

        // another supplier's payment doesn't pay the head office's bill
        Voucher(t.Fresh(), Day.AddDays(8), (id("632000"), 300, 0, null), (id(C.Payable), 0, 300, "printer"));
        Assert.Equal(5000m, (await PlAsync(t.Fresh(), "cash", month.Item1, month.Item2)).Expenses);
    }

    [Fact]
    public async Task A_purchases_shipping_counts_on_the_cash_basis_as_the_supplier_is_paid()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        var p = Purchase(t.Db, pid, 10, 100, Day, shipping: 200);           // total 1,200, shipping 200 is an expense
        var month = (Day.AddDays(-9), Day.AddDays(20));
        Assert.Equal(200m, (await PlAsync(t.Fresh(), "accrual", month.Item1, month.Item2)).Expenses);
        Assert.Equal(0m, (await PlAsync(t.Fresh(), "cash", month.Item1, month.Item2)).Expenses);            // nothing paid yet

        t.Db.PurchasePayments.Add(new PurchasePayment { PurchaseId = p.Id, Amount = 600, Method = "Cash", Date = Day.AddDays(2) });
        t.Db.SaveChanges();
        Assert.Equal(100m, (await PlAsync(t.Fresh(), "cash", month.Item1, month.Item2)).Expenses);           // half paid: half of it
    }

    [Fact]
    public async Task An_expense_paid_in_cash_and_depreciation_count_on_their_date_on_both_bases()
    {
        using var t = new TestDb();
        var cat = new ExpenseCategory { Name = "Rent" };
        t.Db.ExpenseCategories.Add(cat); t.Db.SaveChanges();
        t.Db.Expenses.Add(new Expense { Reference = "E", ExpenseCategoryId = cat.Id, Date = Day, Amount = 75 });
        t.Db.FinanceEntries.Add(new FinanceEntry { Type = FinanceEntryType.Depreciation, Name = "Laptop", Amount = 25, Date = Day });
        t.Db.SaveChanges();
        var month = (Day.AddDays(-9), Day.AddDays(20));
        Assert.Equal(100m, (await PlAsync(t.Fresh(), "accrual", month.Item1, month.Item2)).Expenses);
        Assert.Equal(100m, (await PlAsync(t.Fresh(), "cash", month.Item1, month.Item2)).Expenses);
    }
}
