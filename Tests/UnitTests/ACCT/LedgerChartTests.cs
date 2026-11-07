using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;
using C = ReactPosApi.Services.LedgerChart;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>The chart of accounts in the company's codes (Services/ACCT/LedgerChart.cs)</summary>
public class LedgerChartTests
{
    private static async Task<C> Load(TestDb t)
    {
        var chart = new C(t.Db, t.TenantId);
        await chart.LoadAsync(default);
        return chart;
    }

    [Theory]
    [InlineData(C.Cash, "151001")]
    [InlineData(C.Bank, "151002")]
    [InlineData(C.Stock, "152001")]
    [InlineData(C.Receivable, "153001")]
    [InlineData(C.SupplierReturns, "157001")]
    [InlineData(C.InputTax, "157002")]
    [InlineData(C.Loans, "311001")]
    [InlineData(C.Payable, "312001")]
    [InlineData(C.OwnerCapital, "331001")]
    [InlineData(C.Sales, "511001")]
    [InlineData(C.Cogs, "611001")]
    [InlineData(C.ExpenseCategories, "661000")]
    [InlineData(C.Depreciation, "691000")]
    [InlineData(C.PurchaseDiscounts, "812001")]
    public async Task A_new_company_gets_the_chart_in_its_codes(string key, string code)
    {
        using var t = new TestDb();
        await Load(t);
        Assert.Equal(code, t.Db.LedgerAccounts.Single(a => a.SystemKey == key).Code);
    }

    [Fact]
    public async Task Header_codes_are_groups_and_accounts_sit_under_them()
    {
        using var t = new TestDb();
        await Load(t);
        var all = t.Db.LedgerAccounts.ToList();
        var cash = all.Single(a => a.SystemKey == C.Cash);
        var group = all.Single(a => a.Id == cash.ParentId);
        Assert.Equal("151000", group.Code);
        Assert.True(group.IsGroup);
        Assert.Equal(5, all.Count(a => a.ParentId == null));        // Assets, Liabilities, Equity, Income, Expenses
        Assert.All(all.Where(a => a.ParentId != null), a => Assert.Equal(all.Single(p => p.Id == a.ParentId).RootType, a.RootType));
    }

    [Fact]
    public async Task Loading_twice_creates_nothing_new()
    {
        using var t = new TestDb();
        await Load(t);
        var n = t.Db.LedgerAccounts.Count();
        await Load(t);
        Assert.Equal(n, t.Db.LedgerAccounts.Count());
    }

    [Fact]
    public async Task A_deleted_optional_account_is_not_recreated_but_a_rule_account_is()
    {
        using var t = new TestDb();
        await Load(t);
        t.Db.LedgerAccounts.RemoveRange(t.Db.LedgerAccounts.Where(a => a.SystemKey == "coa:761000" || a.SystemKey == C.Rounding));
        t.Db.SaveChanges();
        await Load(t);
        Assert.False(t.Db.LedgerAccounts.Any(a => a.SystemKey == "coa:761000"));
        Assert.True(t.Db.LedgerAccounts.Any(a => a.SystemKey == C.Rounding));
    }

    [Fact]
    public async Task A_code_used_by_the_company_is_skipped()
    {
        using var t = new TestDb();
        t.Db.LedgerAccounts.Add(new LedgerAccount { Code = "151001", Name = "My own", RootType = LedgerRootType.Asset });
        t.Db.SaveChanges();
        await Load(t);
        Assert.Equal("151002", t.Db.LedgerAccounts.Single(a => a.SystemKey == C.Cash).Code);
        Assert.Equal("151003", t.Db.LedgerAccounts.Single(a => a.SystemKey == C.Bank).Code);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Cash", true)]
    [InlineData(" cash ", true)]
    [InlineData("Not recorded", true)]
    [InlineData("Online", false)]
    [InlineData("Bank Transfer", false)]
    [InlineData("Card", false)]
    public void Cash_methods(string? method, bool cash) => Assert.Equal(cash, C.IsCash(method));

    [Fact]
    public async Task Expense_categories_get_numbered_accounts_and_follow_a_rename()
    {
        using var t = new TestDb();
        var chart = await Load(t);
        var a = await chart.ExpenseCategoryAsync(7, "Rent", default);
        var b = await chart.ExpenseCategoryAsync(8, "Fuel", default);
        Assert.Equal("661001", t.Db.LedgerAccounts.Single(x => x.Id == a).Code);
        Assert.Equal("661002", t.Db.LedgerAccounts.Single(x => x.Id == b).Code);
        await chart.ExpenseCategoryAsync(7, "Office rent", default);
        await t.Db.SaveLedgerAsync(default);
        Assert.Equal("Office rent", t.Db.LedgerAccounts.Single(x => x.Id == a).Name);
    }

    [Fact]
    public async Task An_older_chart_is_moved_to_the_company_codes()
    {
        using var t = new TestDb();
        // the first chart: 4-digit codes, a bank account per payment method, an expense category account at 5501
        var assets = new LedgerAccount { Code = "1000", Name = "Assets", RootType = LedgerRootType.Asset, IsGroup = true, SystemKey = C.Assets };
        t.Db.LedgerAccounts.Add(assets); t.Db.SaveChanges();
        var cashBank = new LedgerAccount { Code = "1100", Name = "Cash & bank", RootType = LedgerRootType.Asset, IsGroup = true, SystemKey = C.CashBank, ParentId = assets.Id };
        t.Db.LedgerAccounts.Add(cashBank); t.Db.SaveChanges();
        t.Db.LedgerAccounts.AddRange(
            new LedgerAccount { Code = "1110", Name = "Cash in hand", RootType = LedgerRootType.Asset, SystemKey = C.Cash, ParentId = cashBank.Id },
            new LedgerAccount { Code = "1101", Name = "Online", RootType = LedgerRootType.Asset, SystemKey = "bank:online", ParentId = cashBank.Id },
            new LedgerAccount { Code = "5501", Name = "Fuel", RootType = LedgerRootType.Expense, SystemKey = "exp-cat:3" });
        t.Db.SaveChanges();

        await new C(t.Db, t.TenantId).ApplyDefaultsAsync(default);

        var all = t.Db.LedgerAccounts.ToList();
        Assert.Equal("151001", all.Single(a => a.SystemKey == C.Cash).Code);
        Assert.Equal("Cash in Hand", all.Single(a => a.SystemKey == C.Cash).Name);
        Assert.DoesNotContain(all, a => a.SystemKey == "bank:online");
        var fuel = all.Single(a => a.SystemKey == "exp-cat:3");
        Assert.Equal("661001", fuel.Code);
        Assert.Equal(all.Single(a => a.SystemKey == C.ExpenseCategories).Id, fuel.ParentId);
        Assert.All(all.Where(a => a.SystemKey != null), a => Assert.Equal(6, a.Code.Length));
    }

    [Fact]
    public async Task Money_accounts_by_method()
    {
        using var t = new TestDb();
        var chart = await Load(t);
        Assert.Equal(chart.Id(C.Cash), await chart.MoneyAsync("Cash", default));
        Assert.Equal(chart.Id(C.Bank), await chart.MoneyAsync("Cheque", default));
    }
}
