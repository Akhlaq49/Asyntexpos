using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;
using static ReactPosApi.UnitTests.ACCT.Seed;
using C = ReactPosApi.Services.LedgerChart;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>Constitution VII: each document's posting rule (Services/ACCT/LedgerPoster.cs), case by case</summary>
public class LedgerPostingTests
{
    [Fact]
    public void Sale_debits_the_customer_and_splits_sales_tax_shipping_installation_and_cost()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 10, 100, Day.AddDays(-5));
        var sale = Sale(t.Db, pid, 4, 250, Day, orderTax: 50, shipping: 20, installation: 30);
        var e = Entry(t.Db, VoucherType.Sale, sale.Id);
        Assert.Equal(1100, e[C.Receivable]);
        Assert.Equal(-1000, e[C.Sales]);
        Assert.Equal(-50, e[C.TaxPayable]);
        Assert.Equal(-20, e[C.ShippingIncome]);
        Assert.Equal(-30, e[C.InstallationIncome]);
        Assert.Equal(400, e[C.Cogs]);                  // 4 x average cost 100
        Assert.Equal(-400, e[C.Stock]);
        AssertAllBalanced(t.Db);
    }

    [Fact]
    public void Item_tax_is_sales_tax_too()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        var s = new Sale { Reference = "S1", CustomerName = "Ali", SaleDate = Day, Status = "Completed", GrandTotal = 220,
            Items = new() { new SaleItem { ProductId = pid, Quantity = 2, PurchasePrice = 100, TaxAmount = 10, UnitCost = 110, TotalCost = 220 } } };
        t.Db.Sales.Add(s); t.Db.SaveChanges();
        var e = Entry(t.Db, VoucherType.Sale, s.Id);
        Assert.Equal(-20, e[C.TaxPayable]);
        Assert.Equal(-200, e[C.Sales]);
    }

    [Theory]
    [InlineData("Queued")]
    [InlineData("Cancelled")]
    public void Queued_or_cancelled_sale_posts_nothing(string status)
    {
        using var t = new TestDb();
        var sale = Sale(t.Db, Product(t.Db), 1, 100, Day, status: status);
        Assert.Empty(Entry(t.Db, VoucherType.Sale, sale.Id));
    }

    [Theory]
    [InlineData("Cash", C.Cash)]
    [InlineData("", C.Cash)]
    [InlineData("Not recorded", C.Cash)]
    [InlineData("Bank Transfer", C.Bank)]
    [InlineData("Online", C.Bank)]
    [InlineData("Cheque", C.Bank)]
    public void Customer_payment_goes_to_cash_or_bank_by_method(string method, string account)
    {
        using var t = new TestDb();
        var sale = Sale(t.Db, Product(t.Db), 1, 500, Day);
        var p = SalePayment(t.Db, sale.Id, 300, method);
        var e = Entry(t.Db, VoucherType.SalePayment, p.Id);
        Assert.Equal(300, e[account]);
        Assert.Equal(-300, e[C.Receivable]);
    }

    [Fact]
    public void Payment_on_a_queued_sale_is_a_customer_advance()
    {
        using var t = new TestDb();
        var sale = Sale(t.Db, Product(t.Db), 1, 500, Day, status: "Queued");
        SalePayment(t.Db, sale.Id, 200);
        Assert.Equal(-200, Balance(t.Db, C.Receivable));     // the customer is owed (credit balance)
    }

    [Fact]
    public void Received_purchase_debits_stock_tax_freight_and_non_stock_and_credits_the_supplier()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        var p = Purchase(t.Db, pid, 10, 100, Day, orderTax: 30, shipping: 15, discount: 5, nonStock: 40);
        var e = Entry(t.Db, VoucherType.Purchase, p.Id);
        Assert.Equal(1000, e[C.Stock]);
        Assert.Equal(40, e[C.NonStockPurchases]);
        Assert.Equal(30, e[C.InputTax]);
        Assert.Equal(15, e[C.Freight]);
        Assert.Equal(-5, e[C.PurchaseDiscounts]);
        Assert.Equal(-1080, e[C.Payable]);
        Assert.False(e.ContainsKey(C.Rounding));
    }

    [Fact]
    public void A_total_that_does_not_match_the_lines_goes_to_rounding()
    {
        using var t = new TestDb();
        var p = Purchase(t.Db, Product(t.Db), 3, 33.33m, Day, total: 100);
        var e = Entry(t.Db, VoucherType.Purchase, p.Id);
        Assert.Equal(0.01m, e[C.Rounding]);
        AssertAllBalanced(t.Db);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Ordered")]
    [InlineData("Cancelled")]
    public void Purchase_not_received_posts_nothing(string status)
    {
        using var t = new TestDb();
        var p = Purchase(t.Db, Product(t.Db), 1, 100, Day, status: status);
        Assert.Empty(Entry(t.Db, VoucherType.Purchase, p.Id));
    }

    [Theory]
    [InlineData("Opening Stock")]
    [InlineData("Stock Entry")]
    public void Stock_brought_in_by_the_owner_credits_equity_not_a_supplier(string supplier)
    {
        using var t = new TestDb();
        var p = Purchase(t.Db, Product(t.Db), 5, 100, Day, supplier: supplier);
        var e = Entry(t.Db, VoucherType.Purchase, p.Id);
        Assert.Equal(500, e[C.Stock]);
        Assert.Equal(-500, e[C.OwnerStock]);
        Assert.False(e.ContainsKey(C.Payable));
    }

    [Theory]
    [InlineData(PurchasePaymentType.Payment, "Cash", 400, C.Cash, -400)]
    [InlineData(PurchasePaymentType.Refund, "Cash", -400, C.Cash, 400)]
    [InlineData(PurchasePaymentType.Payment, "Bank Transfer", 400, C.Bank, -400)]
    [InlineData(PurchasePaymentType.Payment, PurchasePaymentRules.ReturnCredit, 400, C.SupplierReturns, -400)]
    public void Supplier_payment_refund_and_return_credit(string type, string method, decimal payable, string other, decimal otherAmount)
    {
        using var t = new TestDb();
        var p = Purchase(t.Db, Product(t.Db), 10, 100, Day);
        var pay = new PurchasePayment { PurchaseId = p.Id, Type = type, Amount = 400, Method = method, Date = Day };
        t.Db.PurchasePayments.Add(pay); t.Db.SaveChanges();
        var e = Entry(t.Db, VoucherType.PurchasePayment, pay.Id);
        Assert.Equal(payable, e[C.Payable]);
        Assert.Equal(otherAmount, e[other]);
    }

    [Fact]
    public void Purchase_return_moves_stock_to_supplier_returns_and_its_refund_back_to_money()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        var p = Purchase(t.Db, pid, 10, 100, Day);
        var r = new PurchaseReturn { Reference = "PR1", PurchaseId = p.Id, PurchaseReference = p.Reference, SupplierName = "Acme", ReturnDate = Day.AddDays(1), Total = 300,
            Items = new() { new PurchaseReturnItem { PurchaseItemId = p.Items!.First().Id, ProductId = pid, ProductName = "x", Quantity = 3, UnitCost = 100, Total = 300 } } };
        t.Db.PurchaseReturns.Add(r); t.Db.SaveChanges();
        var e = Entry(t.Db, VoucherType.PurchaseReturn, r.Id);
        Assert.Equal(300, e[C.SupplierReturns]);
        Assert.Equal(-300, e[C.Stock]);
        var refund = new PurchaseReturnRefund { PurchaseReturnId = r.Id, Amount = 300, Method = "Online", Date = Day.AddDays(2) };
        t.Db.PurchaseReturnRefunds.Add(refund); t.Db.SaveChanges();
        var f = Entry(t.Db, VoucherType.PurchaseReturnRefund, refund.Id);
        Assert.Equal(300, f[C.Bank]);
        Assert.Equal(-300, f[C.SupplierReturns]);
        Assert.Equal(0, Balance(t.Db, C.SupplierReturns));
    }

    [Fact]
    public void Sales_return_reverses_the_sale_brings_stock_back_at_cost_and_pays_the_refund()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 10, 100, Day.AddDays(-5));
        var sale = Sale(t.Db, pid, 4, 250, Day);
        var r = new SalesReturn { Reference = "SR1", CustomerName = "Ali", ReturnDate = Day.AddDays(1), GrandTotal = 250, Paid = 250, SaleId = sale.Id,
            Items = new() { new SalesReturnItem { ProductId = pid, ProductName = "x", Quantity = 1, NetUnitPrice = 250, Subtotal = 250 } } };
        t.Db.SalesReturns.Add(r); t.Db.SaveChanges();
        var e = Entry(t.Db, VoucherType.SalesReturn, r.Id);
        Assert.Equal(250, e[C.SalesReturns]);
        Assert.Equal(0, e[C.Receivable]);            // -250 for the return, +250 for the refund paid
        Assert.Equal(-250, e[C.Cash]);
        Assert.Equal(100, e[C.Stock]);
        Assert.Equal(-100, e[C.Cogs]);
        AssertAllBalanced(t.Db);
    }

    [Fact]
    public void Expense_debits_its_category_account_under_General_Administration()
    {
        using var t = new TestDb();
        var cat = new ExpenseCategory { Name = "Electricity" };
        t.Db.ExpenseCategories.Add(cat); t.Db.SaveChanges();
        var x = new Expense { Reference = "E1", ExpenseName = "Bill", ExpenseCategoryId = cat.Id, Date = Day, Amount = 75 };
        t.Db.Expenses.Add(x); t.Db.SaveChanges();
        var e = Entry(t.Db, VoucherType.Expense, x.Id);
        Assert.Equal(75, e[$"exp-cat:{cat.Id}"]);
        Assert.Equal(-75, e[C.Cash]);
        var account = t.Db.LedgerAccounts.Single(a => a.SystemKey == $"exp-cat:{cat.Id}");
        Assert.Equal("Electricity", account.Name);
        Assert.Equal("661001", account.Code);
        Assert.Equal(t.Db.LedgerAccounts.Single(a => a.SystemKey == C.ExpenseCategories).Id, account.ParentId);
    }

    [Theory]
    [InlineData(FinanceEntryType.OwnerInvestment, C.Cash, 1000, C.OwnerCapital, -1000)]
    [InlineData(FinanceEntryType.OwnerDrawing, C.Drawings, 1000, C.Cash, -1000)]
    [InlineData(FinanceEntryType.LoanReceived, C.Cash, 1000, C.Loans, -1000)]
    [InlineData(FinanceEntryType.LoanRepayment, C.Loans, 1000, C.Cash, -1000)]
    [InlineData(FinanceEntryType.AssetPurchase, C.FixedAssets, 1000, C.Cash, -1000)]
    [InlineData(FinanceEntryType.Depreciation, C.Depreciation, 1000, C.AccDepreciation, -1000)]
    public void Capital_and_loan_entries(string type, string debitKey, decimal debit, string creditKey, decimal credit)
    {
        using var t = new TestDb();
        var f = new FinanceEntry { Type = type, Name = "Owner", Amount = 1000, Method = "Cash", Date = Day };
        t.Db.FinanceEntries.Add(f); t.Db.SaveChanges();
        var e = Entry(t.Db, VoucherType.FinanceEntry, f.Id);
        Assert.Equal(debit, e[debitKey]);
        Assert.Equal(credit, e[creditKey]);
    }

    [Fact]
    public void Changing_a_document_replaces_its_entry_and_deleting_it_removes_the_entry()
    {
        using var t = new TestDb();
        var cat = new ExpenseCategory { Name = "Rent" };
        t.Db.ExpenseCategories.Add(cat); t.Db.SaveChanges();
        var x = new Expense { Reference = "E1", ExpenseCategoryId = cat.Id, Date = Day, Amount = 75 };
        t.Db.Expenses.Add(x); t.Db.SaveChanges();
        x.Amount = 90; t.Db.SaveChanges();
        Assert.Equal(-90, Entry(t.Db, VoucherType.Expense, x.Id)[C.Cash]);
        Assert.Equal(1, t.Db.JournalEntries.Count(j => j.VoucherType == VoucherType.Expense));
        t.Db.Expenses.Remove(x); t.Db.SaveChanges();          // soft delete
        Assert.Empty(Entry(t.Db, VoucherType.Expense, x.Id));
    }

    [Fact]
    public void A_backdated_purchase_re_costs_the_later_sales()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 10, 100, Day.AddDays(-10));
        var sale = Sale(t.Db, pid, 4, 250, Day);
        Assert.Equal(400, Entry(t.Db, VoucherType.Sale, sale.Id)[C.Cogs]);
        Purchase(t.Db, pid, 10, 130, Day.AddDays(-1));         // before the sale: average (1000 + 1300) / 20 = 115
        Assert.Equal(460, Entry(t.Db, VoucherType.Sale, sale.Id)[C.Cogs]);
        Assert.Equal(1000 + 1300 - 460, Balance(t.Db, C.Stock));
    }

    [Fact]
    public void Nothing_dated_on_or_before_the_lock_date_can_be_added_changed_or_deleted()
    {
        using var t = new TestDb();
        var cat = new ExpenseCategory { Name = "Rent" };
        t.Db.ExpenseCategories.Add(cat); t.Db.SaveChanges();
        var x = new Expense { Reference = "E1", ExpenseCategoryId = cat.Id, Date = Day, Amount = 75 };
        t.Db.Expenses.Add(x); t.Db.SaveChanges();
        t.Db.AccountingSettings.Single().LockDate = Day; t.Db.SaveChanges();

        var db = t.Fresh();
        db.Expenses.Add(new Expense { Reference = "E2", ExpenseCategoryId = cat.Id, Date = Day.AddDays(-1), Amount = 10 });
        Assert.Throws<LedgerLockedException>(() => db.SaveChanges());
        db = t.Fresh();
        Assert.Equal(1, db.Expenses.Count());                 // the refused document was not saved either (one transaction)

        db.Expenses.Single().Amount = 80;
        Assert.Throws<LedgerLockedException>(() => db.SaveChanges());
        db = t.Fresh();
        db.Expenses.Remove(db.Expenses.Single());
        Assert.Throws<LedgerLockedException>(() => db.SaveChanges());

        db = t.Fresh();                                       // after the lock date: allowed
        db.Expenses.Add(new Expense { Reference = "E3", ExpenseCategoryId = cat.Id, Date = Day.AddDays(1), Amount = 10 });
        db.SaveChanges();
        Assert.Equal(2, db.Expenses.Count());
    }

    [Fact]
    public void A_posting_failure_never_blocks_the_document_and_marks_the_ledger_for_rebuild()
    {
        using var t = new TestDb();
        var cat = new ExpenseCategory { Name = "Rent" };
        t.Db.ExpenseCategories.Add(cat); t.Db.SaveChanges();
        var db = t.Fresh();
        db.Database.ExecuteSqlRaw($"PRAGMA foreign_keys = OFF; DROP TABLE {Data.DbNames.AcctDetl}; PRAGMA foreign_keys = ON;");   // break the ledger
        db.Expenses.Add(new Expense { Reference = "E1", ExpenseCategoryId = cat.Id, Date = Day, Amount = 10 });
        db.SaveChanges();                                    // no exception: the expense is saved
        db = t.Fresh();
        Assert.Equal(1, db.Expenses.Count());
        var settings = db.AccountingSettings.Single();
        Assert.True(settings.NeedsRebuild);
        Assert.NotNull(settings.LastError);
    }

    [Fact]
    public async Task Rebuild_gives_the_same_ledger_as_posting_document_by_document()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 10, 100, Day.AddDays(-3));
        var sale = Sale(t.Db, pid, 4, 250, Day, orderTax: 50);
        SalePayment(t.Db, sale.Id, 500, "Online");
        var before = t.Db.JournalLines.AsNoTracking().AsEnumerable().GroupBy(l => l.AccountId).ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));
        await new LedgerPoster(t.Fresh()).RebuildAsync(default);
        var after = t.Fresh().JournalLines.AsNoTracking().AsEnumerable().GroupBy(l => l.AccountId).ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));
        Assert.Equal(before.OrderBy(x => x.Key), after.OrderBy(x => x.Key));
        var settings = t.Db.AccountingSettings.Single();
        Assert.False(settings.NeedsRebuild);
        Assert.Equal(LedgerPoster.RulesVersion, settings.RulesVersion);
    }

    [Fact]
    public async Task EnsureFresh_rebuilds_only_when_needed()
    {
        using var t = new TestDb();
        Sale(t.Db, Product(t.Db), 1, 100, Day);
        var s = t.Db.AccountingSettings.Single();
        s.NeedsRebuild = true; s.RulesVersion = 0; t.Db.SaveChanges();
        await new LedgerPoster(t.Fresh()).EnsureFreshAsync(default);
        var rebuiltAt = t.Fresh().AccountingSettings.Single().RebuiltAt;
        Assert.NotNull(rebuiltAt);
        await new LedgerPoster(t.Fresh()).EnsureFreshAsync(default);
        Assert.Equal(rebuiltAt, t.Fresh().AccountingSettings.Single().RebuiltAt);
    }

    [Fact]
    public void Entries_belong_to_their_company_only()
    {
        using var t = new TestDb();
        Sale(t.Db, Product(t.Db), 1, 100, Day);
        using var other = t.Create(t.TenantId + 1000);
        Assert.Empty(other.JournalEntries.ToList());
        Assert.All(t.Db.JournalEntries.ToList(), e => Assert.Equal(t.TenantId, e.TenantId));
    }
}
