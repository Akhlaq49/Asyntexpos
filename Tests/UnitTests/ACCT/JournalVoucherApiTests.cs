using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Controllers;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;
using static ReactPosApi.UnitTests.ACCT.Seed;
using C = ReactPosApi.Services.LedgerChart;
using Line = ReactPosApi.Controllers.JournalVouchersController.LineDto;
using Save = ReactPosApi.Controllers.JournalVouchersController.SaveDto;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>Journal voucher API (Controllers/ACCT/JournalVouchersController.cs): the rules a voucher must pass, edit, delete, reverse</summary>
public class JournalVoucherApiTests
{
    private static JsonElement Json(IActionResult r) =>
        JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(r).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string Error(IActionResult r) => Json(new OkObjectResult(Assert.IsType<BadRequestObjectResult>(r).Value)).GetProperty("message").GetString()!;

    private static async Task<Func<string, int>> ChartAsync(TestDb t)
    {
        await new LedgerPoster(t.Db).RebuildAsync(default);
        var ids = t.Fresh().LedgerAccounts.AsNoTracking().Where(a => a.SystemKey != null).ToDictionary(a => a.SystemKey!, a => a.Id);
        return key => ids[key];
    }

    private static int Party(TestDb t, string name, string role)
    {
        var p = new Party { FullName = name, Role = role };
        t.Db.Parties.Add(p); t.Db.SaveChanges();
        return p.Id;
    }

    private static JournalVouchersController Api(TestDb t) => new(t.Fresh());
    private static Save Voucher(params Line[] lines) => new(Day, "Reclass", lines.ToList());
    private static Line Dr(int account, decimal amount, int? party = null, string? name = null) => new(account, amount, 0, party, name, null);
    private static Line Cr(int account, decimal amount, int? party = null, string? name = null) => new(account, 0, amount, party, name, null);

    [Fact]
    public async Task A_balanced_voucher_is_saved_numbered_and_posted()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var v = Json(await Api(t).Create(Voucher(Dr(id(C.Bank), 500), Cr(id(C.Cash), 500)), default));
        Assert.Equal("JV-0001", v.GetProperty("reference").GetString());
        Assert.Equal(500, v.GetProperty("total").GetDecimal());
        Assert.Equal(2, v.GetProperty("lines").GetArrayLength());
        Assert.Equal(500, Balance(t.Fresh(), C.Bank));
        Assert.Equal(-500, Balance(t.Fresh(), C.Cash));

        var second = Json(await Api(t).Create(Voucher(Dr(id(C.Bank), 10), Cr(id(C.Cash), 10)), default));
        Assert.Equal("JV-0002", second.GetProperty("reference").GetString());
    }

    [Fact]
    public async Task Debits_must_equal_credits()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var message = Error(await Api(t).Create(Voucher(Dr(id(C.Bank), 500), Cr(id(C.Cash), 250)), default));
        Assert.Contains("must be equal", message);
        Assert.Contains("250.00", message);
        Assert.Empty(t.Fresh().JournalVouchers.ToList());
    }

    [Fact]
    public async Task Each_line_needs_either_a_debit_or_a_credit_and_there_must_be_two()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        Assert.Contains("either a debit or a credit", Error(await Api(t).Create(Voucher(new Line(id(C.Bank), 100, 100, null, null, null), Cr(id(C.Cash), 0)), default)));
        Assert.Contains("negative", Error(await Api(t).Create(Voucher(Dr(id(C.Bank), -100), Cr(id(C.Cash), -100)), default)));
        Assert.Contains("at least two lines", Error(await Api(t).Create(Voucher(Dr(id(C.Bank), 100)), default)));
        Assert.Contains("description", Error(await Api(t).Create(new Save(Day, " ", new() { Dr(id(C.Bank), 1), Cr(id(C.Cash), 1) }), default)));
        Assert.Contains("date", Error(await Api(t).Create(new Save(null, "x", new() { Dr(id(C.Bank), 1), Cr(id(C.Cash), 1) }), default)));
    }

    [Fact]
    public async Task Stock_group_and_inactive_accounts_are_refused()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        Assert.Contains("Stock value comes from", Error(await Api(t).Create(Voucher(Dr(id(C.Stock), 100), Cr(id(C.Cash), 100)), default)));
        Assert.Contains("is a group", Error(await Api(t).Create(Voucher(Dr(id(C.Assets), 100), Cr(id(C.Cash), 100)), default)));
        Assert.Contains("choose an account", Error(await Api(t).Create(Voucher(Dr(999999, 100), Cr(id(C.Cash), 100)), default)));

        var db = t.Fresh();
        var mine = new LedgerAccount { Code = "6999", Name = "Old account", RootType = LedgerRootType.Expense, ParentId = id(C.Expenses), IsActive = false };
        db.LedgerAccounts.Add(mine); db.SaveChanges();
        Assert.Contains("inactive", Error(await Api(t).Create(Voucher(Dr(mine.Id, 100), Cr(id(C.Cash), 100)), default)));

        var accounts = Json(await Api(t).Accounts(default)).EnumerateArray().Select(a => a.GetProperty("id").GetInt32()).ToList();
        Assert.DoesNotContain(id(C.Stock), accounts);
        Assert.DoesNotContain(id(C.Assets), accounts);
        Assert.DoesNotContain(mine.Id, accounts);
        Assert.Contains(id(C.Bank), accounts);
    }

    [Fact]
    public async Task Receivable_lines_need_a_customer_and_move_the_balance_between_customers()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var ali = Party(t, "Ali", "Customer");
        var bilal = Party(t, "Bilal", "Customer");
        var supplier = Party(t, "Acme", "Supplier");
        Assert.Contains("choose the customer", Error(await Api(t).Create(Voucher(Dr(id(C.Receivable), 200), Cr(id(C.Receivable), 200, ali)), default)));
        Assert.Contains("choose the customer", Error(await Api(t).Create(Voucher(Dr(id(C.Receivable), 200, supplier), Cr(id(C.Receivable), 200, ali)), default)));

        Json(await Api(t).Create(Voucher(Dr(id(C.Receivable), 200, bilal), Cr(id(C.Receivable), 200, ali)), default));
        var lines = t.Fresh().JournalLines.AsNoTracking().Where(l => l.PartyType == LedgerPartyType.Customer).ToList();
        Assert.Equal(200, lines.Single(l => l.PartyId == bilal && l.PartyName == "Bilal").Debit);
        Assert.Equal(200, lines.Single(l => l.PartyId == ali && l.PartyName == "Ali").Credit);
    }

    [Fact]
    public async Task Payable_lines_keep_the_supplier_by_name_and_loan_lines_need_a_lender()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var acme = Party(t, "Acme", "Supplier");
        Assert.Contains("choose the supplier", Error(await Api(t).Create(Voucher(Dr(id(C.Payable), 100), Cr(id(C.Cash), 100)), default)));
        Assert.Contains("lender", Error(await Api(t).Create(Voucher(Dr(id(C.Cash), 100), Cr(id(C.Loans), 100)), default)));

        Json(await Api(t).Create(Voucher(Dr(id(C.Payable), 100, acme), Cr(id(C.Loans), 100, name: "  HBL  ")), default));
        var lines = t.Fresh().JournalLines.AsNoTracking().Where(l => l.PartyType != null).ToList();
        var payable = lines.Single(l => l.PartyType == LedgerPartyType.Supplier);
        Assert.Equal("Acme", payable.PartyName);
        Assert.Null(payable.PartyId);                       // like purchases: suppliers are kept by name
        Assert.Equal("HBL", lines.Single(l => l.PartyType == LedgerPartyType.Lender).PartyName);
    }

    [Fact]
    public async Task A_party_on_an_account_that_needs_none_is_dropped()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var ali = Party(t, "Ali", "Customer");
        Json(await Api(t).Create(Voucher(Dr(id(C.Bank), 50, ali, "Ali"), Cr(id(C.Cash), 50)), default));
        Assert.All(t.Fresh().JournalLines.AsNoTracking().ToList(), l => Assert.Null(l.PartyName));
    }

    [Fact]
    public async Task Editing_replaces_the_lines_and_the_posted_entry()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var v = Json(await Api(t).Create(Voucher(Dr(id(C.Bank), 500), Cr(id(C.Cash), 500)), default)).GetProperty("id").GetInt32();
        var edited = Json(await Api(t).Update(v, new Save(Day.AddDays(1), "Fixed", new() { Dr(id(C.Depreciation), 300), Cr(id(C.AccDepreciation), 300) }), default));
        Assert.Equal("Fixed", edited.GetProperty("description").GetString());
        Assert.Equal("JV-0001", edited.GetProperty("reference").GetString());
        Assert.Equal(2, t.Fresh().JournalVoucherLines.Count());
        Assert.Equal(0, Balance(t.Fresh(), C.Bank));
        Assert.Equal(300, Balance(t.Fresh(), C.Depreciation));
        Assert.Equal(Day.AddDays(1), t.Fresh().JournalEntries.Single().Date);
    }

    [Fact]
    public async Task Deleting_removes_the_voucher_and_its_entry()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var v = Json(await Api(t).Create(Voucher(Dr(id(C.Bank), 500), Cr(id(C.Cash), 500)), default)).GetProperty("id").GetInt32();
        Assert.IsType<NoContentResult>(await Api(t).Delete(v, default));
        Assert.Empty(t.Fresh().JournalVouchers.ToList());
        Assert.Empty(t.Fresh().JournalVoucherLines.ToList());
        Assert.Empty(t.Fresh().JournalEntries.ToList());
        Assert.IsType<NotFoundObjectResult>(await Api(t).Delete(v, default));

        // soft delete: the row is kept, marked deleted, and its number is never given out again
        var db = t.Fresh();
        var kept = db.JournalVouchers.IgnoreQueryFilters().Single(x => x.Id == v);
        Assert.True(db.Entry(kept).Property<bool>(Data.SoftDelete.IsDeleted).CurrentValue);
        var next = Json(await Api(t).Create(Voucher(Dr(id(C.Bank), 5), Cr(id(C.Cash), 5)), default));
        Assert.Equal("JV-0002", next.GetProperty("reference").GetString());
    }

    [Fact]
    public async Task Reverse_swaps_debit_and_credit_once_and_locks_the_original()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var ali = Party(t, "Ali", "Customer");
        var v = Json(await Api(t).Create(Voucher(Dr(id(C.Receivable), 200, ali), Cr(id(C.Sales), 200)), default)).GetProperty("id").GetInt32();

        Assert.Contains("before the voucher", Error(await Api(t).Reverse(v, new(Day.AddDays(-1), null), default)));
        var r = Json(await Api(t).Reverse(v, new(Day.AddDays(2), null), default));
        Assert.Equal("JV-0002", r.GetProperty("reference").GetString());
        Assert.Equal("Reversal of JV-0001", r.GetProperty("description").GetString());
        Assert.Equal("JV-0001", r.GetProperty("reversalOfReference").GetString());
        Assert.Equal(0, Balance(t.Fresh(), C.Receivable));
        Assert.Equal(0, Balance(t.Fresh(), C.Sales));
        var back = t.Fresh().JournalVoucherLines.AsNoTracking().Single(l => l.JournalVoucherId == r.GetProperty("id").GetInt32() && l.AccountId == id(C.Receivable));
        Assert.Equal(200, back.Credit);
        Assert.Equal("Ali", back.PartyName);

        var original = Json(await Api(t).Get(v, default));
        Assert.Equal("JV-0002", original.GetProperty("reversedByReference").GetString());
        Assert.Contains("already been reversed", Error(await Api(t).Reverse(v, new(null, null), default)));
        Assert.Contains("Delete its reversal first", Error(await Api(t).Update(v, Voucher(Dr(id(C.Bank), 1), Cr(id(C.Cash), 1)), default)));
        Assert.Contains("Delete JV-0002 first", Error(await Api(t).Delete(v, default)));
        Assert.Contains("can't be reversed", Error(await Api(t).Reverse(r.GetProperty("id").GetInt32(), new(null, null), default)));
        Assert.Contains("can't be edited", Error(await Api(t).Update(r.GetProperty("id").GetInt32(), Voucher(Dr(id(C.Bank), 1), Cr(id(C.Cash), 1)), default)));

        Assert.IsType<NoContentResult>(await Api(t).Delete(r.GetProperty("id").GetInt32(), default));   // delete the reversal: the original is free again
        Assert.IsType<NoContentResult>(await Api(t).Delete(v, default));
    }

    [Fact]
    public async Task The_lock_date_applies()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var v = Json(await Api(t).Create(Voucher(Dr(id(C.Bank), 500), Cr(id(C.Cash), 500)), default)).GetProperty("id").GetInt32();
        var db = t.Fresh();
        db.AccountingSettings.Single().LockDate = Day; db.SaveChanges();
        await Assert.ThrowsAsync<LedgerLockedException>(() => Api(t).Create(Voucher(Dr(id(C.Bank), 1), Cr(id(C.Cash), 1)), default));
        await Assert.ThrowsAsync<LedgerLockedException>(() => Api(t).Delete(v, default));
        await Assert.ThrowsAsync<LedgerLockedException>(() => Api(t).Update(v, new Save(Day.AddDays(5), "x", new() { Dr(id(C.Bank), 1), Cr(id(C.Cash), 1) }), default));
        Json(await Api(t).Reverse(v, new(Day.AddDays(1), null), default));       // undone after the lock date: allowed
        Assert.Equal(0, Balance(t.Fresh(), C.Bank));
    }

    [Fact]
    public async Task List_filters_by_date_and_search()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        await Api(t).Create(new Save(Day, "Rent accrual", new() { Dr(id(C.Bank), 1), Cr(id(C.Cash), 1) }), default);
        await Api(t).Create(new Save(Day.AddDays(10), "Depreciation March", new() { Dr(id(C.Depreciation), 5), Cr(id(C.AccDepreciation), 5) }), default);
        Assert.Equal(2, Json(await Api(t).List(null, null, null, ct: default)).GetProperty("total").GetInt32());
        Assert.Equal(1, Json(await Api(t).List(Day.AddDays(1), null, null, ct: default)).GetProperty("total").GetInt32());
        Assert.Equal(1, Json(await Api(t).List(null, Day, null, ct: default)).GetProperty("total").GetInt32());
        var found = Json(await Api(t).List(null, null, "Rent", ct: default)).GetProperty("vouchers").EnumerateArray().Single();
        Assert.Equal("JV-0001", found.GetProperty("reference").GetString());
    }

    [Fact]
    public async Task Vouchers_belong_to_their_company_only()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var v = Json(await Api(t).Create(Voucher(Dr(id(C.Bank), 5), Cr(id(C.Cash), 5)), default)).GetProperty("id").GetInt32();
        using var other = t.Create(t.TenantId + 1000);
        Assert.IsType<NotFoundObjectResult>(await new JournalVouchersController(other).Get(v, default));
        Assert.Contains("choose an account", Error(await new JournalVouchersController(other).Create(Voucher(Dr(id(C.Bank), 5), Cr(id(C.Cash), 5)), default)));
    }
}
