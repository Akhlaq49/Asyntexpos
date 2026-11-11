using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;
using static ReactPosApi.UnitTests.ACCT.Seed;
using C = ReactPosApi.Services.LedgerChart;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>Journal voucher posting rule (Services/ACCT/LedgerPoster.cs): its lines go to the ledger as typed in</summary>
public class JournalVoucherPostingTests
{
    private static async Task<Func<string, int>> ChartAsync(TestDb t)
    {
        await new LedgerPoster(t.Db).RebuildAsync(default);           // makes the company's chart of accounts
        var ids = t.Fresh().LedgerAccounts.AsNoTracking().Where(a => a.SystemKey != null).ToDictionary(a => a.SystemKey!, a => a.Id);
        return key => ids[key];
    }

    private static JournalVoucher Voucher(AppDbContext db, DateTime date, params (int Account, decimal Debit, decimal Credit)[] lines)
    {
        var v = new JournalVoucher { Date = date, Reference = $"JV-{db.JournalVouchers.Count() + 1:0000}", Description = "Reclass" };
        var n = 0;
        foreach (var (account, debit, credit) in lines) v.Lines.Add(new JournalVoucherLine { LineNo = ++n, AccountId = account, Debit = debit, Credit = credit });
        db.JournalVouchers.Add(v); db.SaveChanges();
        return v;
    }

    [Fact]
    public async Task Voucher_posts_each_line_as_typed_in()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var v = Voucher(t.Fresh(), Day, (id(C.Bank), 500, 0), (id(C.Cash), 0, 500));
        var e = Entry(t.Fresh(), VoucherType.JournalVoucher, v.Id);
        Assert.Equal(500, e[C.Bank]);
        Assert.Equal(-500, e[C.Cash]);
        var entry = t.Fresh().JournalEntries.Single(x => x.VoucherType == VoucherType.JournalVoucher);
        Assert.Equal(v.Reference, entry.Reference);
        Assert.Equal("Reclass", entry.Description);
        AssertAllBalanced(t.Fresh());
    }

    [Fact]
    public async Task Editing_a_voucher_replaces_its_entry_and_deleting_it_removes_the_entry()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var v = Voucher(t.Fresh(), Day, (id(C.Bank), 500, 0), (id(C.Cash), 0, 500));

        var db = t.Fresh();
        foreach (var l in db.JournalVoucherLines.Where(l => l.JournalVoucherId == v.Id)) { if (l.Debit > 0) l.Debit = 300; else l.Credit = 300; }
        db.SaveChanges();
        Assert.Equal(300, Entry(t.Fresh(), VoucherType.JournalVoucher, v.Id)[C.Bank]);

        db = t.Fresh();
        db.JournalVouchers.Remove(db.JournalVouchers.Single());
        db.SaveChanges();
        Assert.Empty(Entry(t.Fresh(), VoucherType.JournalVoucher, v.Id));
    }

    [Fact]
    public async Task Voucher_keeps_the_customer_on_a_receivable_line()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var db = t.Fresh();
        var v = new JournalVoucher { Date = Day, Reference = "JV-0001", Description = "Moved to the right customer" };
        v.Lines.Add(new JournalVoucherLine { LineNo = 1, AccountId = id(C.Receivable), Debit = 200, PartyType = LedgerPartyType.Customer, PartyId = 7, PartyName = "Bilal" });
        v.Lines.Add(new JournalVoucherLine { LineNo = 2, AccountId = id(C.Receivable), Credit = 200, PartyType = LedgerPartyType.Customer, PartyId = 6, PartyName = "Ali" });
        db.JournalVouchers.Add(v); db.SaveChanges();
        var lines = t.Fresh().JournalLines.AsNoTracking().Where(l => l.PartyType == LedgerPartyType.Customer).ToList();
        Assert.Equal(200, lines.Single(l => l.PartyName == "Bilal").Debit);
        Assert.Equal(200, lines.Single(l => l.PartyName == "Ali").Credit);
    }

    [Fact]
    public async Task Voucher_dated_on_or_before_the_lock_date_cant_be_added()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var db = t.Fresh();
        db.AccountingSettings.Single().LockDate = Day; db.SaveChanges();
        Assert.Throws<LedgerLockedException>(() => Voucher(t.Fresh(), Day, (id(C.Bank), 100, 0), (id(C.Cash), 0, 100)));
        Assert.Empty(t.Fresh().JournalVouchers.ToList());
        Voucher(t.Fresh(), Day.AddDays(1), (id(C.Bank), 100, 0), (id(C.Cash), 0, 100));   // after the lock date: allowed
        Assert.Single(t.Fresh().JournalVouchers.ToList());
    }

    [Fact]
    public async Task Rebuild_keeps_the_vouchers()
    {
        using var t = new TestDb();
        var id = await ChartAsync(t);
        var v = Voucher(t.Fresh(), Day, (id(C.Depreciation), 1000, 0), (id(C.AccDepreciation), 0, 1000));
        await new LedgerPoster(t.Fresh()).RebuildAsync(default);
        var e = Entry(t.Fresh(), VoucherType.JournalVoucher, v.Id);
        Assert.Equal(1000, e[C.Depreciation]);
        Assert.Equal(-1000, e[C.AccDepreciation]);
    }
}
