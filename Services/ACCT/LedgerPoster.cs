using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ReactPosApi.Controllers;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>Documents a save changed: posted again after the save (AppDbContext.SaveChangesAsync)</summary>
public class LedgerTouches
{
    internal readonly HashSet<(string Type, int Id)> Vouchers = new();
    internal readonly List<(EntityEntry Entry, string Type, string IdProperty)> Added = new();
    internal readonly HashSet<int> Products = new();
    internal readonly List<DateTime> Dates = new();
    internal bool Stock;
    public bool IsEmpty => Vouchers.Count == 0 && Added.Count == 0 && !Stock;
}

/// <summary>
/// Posting rules: each document that moves money or stock has one balanced journal entry, built from the document
/// as it is now (a change = the entry is replaced; a deleted document = no entry). Stock leaves at its moving average
/// cost, so a change to stock (purchase, sale, return, stock entry) re-costs the later sales of those products.
/// Entries dated on or before the lock date can't change. Any other failure never blocks the document being saved:
/// the company's ledger is marked to be rebuilt instead (reports rebuild it).
///
///   document                  debit                                  credit
///   Sale                      Receivable (customer) total            Sales, Sales tax collected, Shipping / Installation charged
///                             Cost of goods sold                     Stock (quantity x average cost)
///   SalePayment               Cash / bank                            Receivable (customer)
///   SalesReturn               Sales returns, tax, shipping           Receivable (customer); refund paid: Receivable / Cash
///                             Stock (at average cost)                Cost of goods sold
///   Purchase (received)       Stock, non-stock purchases,            Payable (supplier), discounts from suppliers
///                             tax paid, shipping on purchases
///   Opening stock / stock entry  Stock                               Stock brought in by the owner
///   PurchasePayment           Payable (supplier)                     Cash / bank   (a refund the other way; a return credit: Supplier returns)
///   PurchaseReturn            Supplier returns to be refunded        Stock
///   PurchaseReturnRefund      Cash / bank                            Supplier returns to be refunded
///   Expense                   its category's expense account         Cash
///   Capital &amp; loans         owner's capital, drawings, loans, equipment, depreciation (see FinanceEntry)
///   Journal voucher           the accounts and amounts typed in on it, line by line (reclass, correction, accrual)
/// </summary>
public class LedgerPoster
{
    /// <summary>Change this when the rules change: every company's ledger is then rebuilt on its next report</summary>
    public const int RulesVersion = 3;     // 2: the company's own account codes (LedgerChart); 3: re-posted sales keep every product's cost

    private readonly AppDbContext _db;
    private readonly int _tenant;
    private LedgerChart _chart = null!;
    private StockCost? _cost;

    public LedgerPoster(AppDbContext db) { _db = db; _tenant = db.CurrentTenantId; }

    // ── what a save touched ──────────────────────────────────────────────────────────────────────────────────
    public static LedgerTouches Collect(AppDbContext db)
    {
        var t = new LedgerTouches();
        foreach (var e in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            switch (e.Entity)
            {
                case Sale x: Touch(t, e, VoucherType.Sale, "Id", true); t.Dates.Add(x.SaleDate); break;
                case SaleItem x: Touch(t, e, VoucherType.Sale, "SaleId", true); Product(t, e, "ProductId"); break;
                case SalePayment: Touch(t, e, VoucherType.SalePayment, "Id", false); break;
                case SalesReturn x: Touch(t, e, VoucherType.SalesReturn, "Id", true); t.Dates.Add(x.ReturnDate); break;
                case SalesReturnItem: Touch(t, e, VoucherType.SalesReturn, "SalesReturnId", true); Product(t, e, "ProductId"); break;
                case Purchase x: Touch(t, e, VoucherType.Purchase, "Id", true); t.Dates.Add(x.Date); break;
                case PurchaseItem: Touch(t, e, VoucherType.Purchase, "PurchaseId", true); Product(t, e, "ProductId"); break;
                case PurchasePayment: Touch(t, e, VoucherType.PurchasePayment, "Id", false); break;
                case PurchaseReturn x: Touch(t, e, VoucherType.PurchaseReturn, "Id", true); t.Dates.Add(x.ReturnDate); break;
                case PurchaseReturnItem: Touch(t, e, VoucherType.PurchaseReturn, "PurchaseReturnId", true); Product(t, e, "ProductId"); break;
                case PurchaseReturnRefund: Touch(t, e, VoucherType.PurchaseReturnRefund, "Id", false); break;
                case Expense: Touch(t, e, VoucherType.Expense, "Id", false); break;
                case FinanceEntry: Touch(t, e, VoucherType.FinanceEntry, "Id", false); break;
                case JournalVoucher: Touch(t, e, VoucherType.JournalVoucher, "Id", false); break;
                case JournalVoucherLine: Touch(t, e, VoucherType.JournalVoucher, "JournalVoucherId", false); break;
                case StockEntry x:
                    t.Stock = true; Product(t, e, "ProductId"); t.Dates.Add(x.Date);
                    if (e.State == EntityState.Modified && e.Property("Date").OriginalValue is DateTime d) t.Dates.Add(d);
                    break;
            }
        }
        return t;
    }

    private static void Touch(LedgerTouches t, EntityEntry e, string type, string idProperty, bool stock)
    {
        if (stock) t.Stock = true;
        if (e.State == EntityState.Added && idProperty == "Id") { t.Added.Add((e, type, idProperty)); return; }
        if (e.Property(idProperty).CurrentValue is int id && id > 0) t.Vouchers.Add((type, id));
        else t.Added.Add((e, type, idProperty));        // a line of a new document: its id is known after the save
        if (e.State == EntityState.Modified && e.Property(idProperty).OriginalValue is int old && old > 0) t.Vouchers.Add((type, old));
    }

    private static void Product(LedgerTouches t, EntityEntry e, string property)
    {
        if (e.Property(property).CurrentValue is int p && p > 0) t.Products.Add(p);
        if (e.State == EntityState.Modified && e.Property(property).OriginalValue is int o && o > 0) t.Products.Add(o);
    }

    // ── after the save ───────────────────────────────────────────────────────────────────────────────────────
    public async Task SyncAsync(LedgerTouches t, CancellationToken ct)
    {
        foreach (var (entry, type, prop) in t.Added)
            if (entry.Property(prop).CurrentValue is int id && id > 0) t.Vouchers.Add((type, id));
        var settings = await SettingsAsync(ct);
        try
        {
            await _chart_LoadAsync(ct);
            var vouchers = new HashSet<(string, int)>(t.Vouchers);
            if (t.Stock) await AddRecostedAsync(t, vouchers, ct);
            foreach (var (type, id) in vouchers) await PostAsync(type, id, settings.LockDate, ct);
            await _db.SaveLedgerAsync(ct);
        }
        catch (LedgerLockedException) { throw; }
        catch (Exception ex)
        {
            // never block the document: drop what was half-posted, rebuild the ledger on the next report
            foreach (var e in _db.ChangeTracker.Entries().Where(e => e.Entity is JournalEntry or JournalLine or LedgerAccount && e.State != EntityState.Unchanged).ToList())
                e.State = EntityState.Detached;
            settings.NeedsRebuild = true;
            settings.LastError = Trim($"{DateTime.UtcNow:u} {ex.GetType().Name}: {ex.Message}", 500);
            await _db.SaveLedgerAsync(ct);
        }
    }

    private async Task _chart_LoadAsync(CancellationToken ct)
    {
        _chart = new LedgerChart(_db, _tenant);
        await _chart.LoadAsync(ct);
    }

    /// <summary>Stock changed: the products' costs are worked out again, and the sales / sales returns of those
    /// products from the earliest changed date are posted again (their cost of goods sold may have moved)</summary>
    private async Task AddRecostedAsync(LedgerTouches t, HashSet<(string, int)> vouchers, CancellationToken ct)
    {
        var products = new HashSet<int>(t.Products);
        var ids = (string type) => vouchers.Where(v => v.Item1 == type).Select(v => v.Item2).ToList();
        var sales = ids(VoucherType.Sale); var sret = ids(VoucherType.SalesReturn); var purch = ids(VoucherType.Purchase); var pret = ids(VoucherType.PurchaseReturn);
        products.UnionWith(await _db.SaleItems.IgnoreQueryFilters().Where(i => i.TenantId == _tenant && sales.Contains(i.SaleId)).Select(i => i.ProductId).ToListAsync(ct));
        products.UnionWith(await _db.SalesReturnItems.IgnoreQueryFilters().Where(i => i.TenantId == _tenant && sret.Contains(i.SalesReturnId) && i.ProductId != null).Select(i => i.ProductId!.Value).ToListAsync(ct));
        products.UnionWith(await _db.PurchaseItems.IgnoreQueryFilters().Where(i => i.TenantId == _tenant && purch.Contains(i.PurchaseId) && i.ProductId != null).Select(i => i.ProductId!.Value).ToListAsync(ct));
        products.UnionWith(await _db.PurchaseReturnItems.IgnoreQueryFilters().Where(i => i.TenantId == _tenant && pret.Contains(i.PurchaseReturnId) && i.ProductId != null).Select(i => i.ProductId!.Value).ToListAsync(ct));
        products.Remove(0);
        if (products.Count == 0) return;
        // earliest date: the documents' dates now and the dates their entries had
        var dates = new List<DateTime>(t.Dates);
        foreach (var type in new[] { VoucherType.Sale, VoucherType.SalesReturn, VoucherType.Purchase, VoucherType.PurchaseReturn })
        {
            var list = ids(type);
            if (list.Count > 0) dates.AddRange(await _db.JournalEntries.Where(j => j.VoucherType == type && list.Contains(j.VoucherId)).Select(j => j.Date).ToListAsync(ct));
        }
        var from = dates.Count > 0 ? dates.Min().Date : DateTime.MinValue;
        foreach (var id in await _db.Sales.Where(s => s.SaleDate >= from && s.Items.Any(i => products.Contains(i.ProductId))).Select(s => s.Id).ToListAsync(ct))
            vouchers.Add((VoucherType.Sale, id));
        foreach (var id in await _db.SalesReturns.Where(r => r.ReturnDate >= from && r.Items.Any(i => i.ProductId != null && products.Contains(i.ProductId.Value))).Select(r => r.Id).ToListAsync(ct))
            vouchers.Add((VoucherType.SalesReturn, id));
        // a sale posted again is posted whole: the cost of every product on it is needed, not only of the products that
        // changed (else its other lines would get no cost). Each product's average is worked out on its own.
        sales = ids(VoucherType.Sale); sret = ids(VoucherType.SalesReturn);
        products.UnionWith(await _db.SaleItems.IgnoreQueryFilters().Where(i => i.TenantId == _tenant && sales.Contains(i.SaleId)).Select(i => i.ProductId).ToListAsync(ct));
        products.UnionWith(await _db.SalesReturnItems.IgnoreQueryFilters().Where(i => i.TenantId == _tenant && sret.Contains(i.SalesReturnId) && i.ProductId != null).Select(i => i.ProductId!.Value).ToListAsync(ct));
        products.Remove(0);
        _cost = await StockCost.LoadAsync(_db, _tenant, products, ct);
    }

    private async Task<AccountingSettings> SettingsAsync(CancellationToken ct)
    {
        var s = await _db.AccountingSettings.FirstOrDefaultAsync(ct);
        if (s == null)
        {
            s = new AccountingSettings { TenantId = _tenant, NeedsRebuild = true };
            _db.AccountingSettings.Add(s);
        }
        return s;
    }

    // ── rebuild: every document of the company posted again (first use, rules changed, or after a failure) ──────
    public async Task RebuildAsync(CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        await _db.SaveLedgerAsync(ct);
        await _db.JournalEntries.Where(j => j.TenantId == _tenant).ExecuteDeleteAsync(ct);      // lines go with them (cascade)
        if (settings.RulesVersion < RulesVersion) await new LedgerChart(_db, _tenant).ApplyDefaultsAsync(ct);   // the chart as defined now
        await _chart_LoadAsync(ct);
        _cost = await StockCost.LoadAsync(_db, _tenant, null, ct);
        var all = new List<(string, int)>();
        all.AddRange((await _db.Sales.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.Sale, id)));
        all.AddRange((await _db.SalePayments.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.SalePayment, id)));
        all.AddRange((await _db.SalesReturns.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.SalesReturn, id)));
        all.AddRange((await _db.Purchases.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.Purchase, id)));
        all.AddRange((await _db.PurchasePayments.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.PurchasePayment, id)));
        all.AddRange((await _db.PurchaseReturns.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.PurchaseReturn, id)));
        all.AddRange((await _db.PurchaseReturnRefunds.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.PurchaseReturnRefund, id)));
        all.AddRange((await _db.Expenses.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.Expense, id)));
        all.AddRange((await _db.FinanceEntries.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.FinanceEntry, id)));
        all.AddRange((await _db.JournalVouchers.Select(x => x.Id).ToListAsync(ct)).Select(id => (VoucherType.JournalVoucher, id)));
        var n = 0;
        foreach (var (type, id) in all)
        {
            await PostAsync(type, id, null, ct, rebuilding: true);
            if (++n % 200 == 0) { await _db.SaveLedgerAsync(ct); _db.ChangeTracker.Clear(); settings = await SettingsAsync(ct); await _chart_LoadAsync(ct); }
        }
        settings.RulesVersion = RulesVersion;
        settings.NeedsRebuild = false;
        settings.RebuiltAt = DateTime.UtcNow;
        settings.LastError = null;
        await _db.SaveLedgerAsync(ct);
    }

    /// <summary>Rebuilds the ledger when it was never built, the rules changed or a posting failed</summary>
    public async Task EnsureFreshAsync(CancellationToken ct)
    {
        var s = await _db.AccountingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s == null || s.NeedsRebuild || s.RulesVersion != RulesVersion) await RebuildAsync(ct);
    }

    // ── one document ─────────────────────────────────────────────────────────────────────────────────────────
    private async Task PostAsync(string type, int id, DateTime? lockDate, CancellationToken ct, bool rebuilding = false)
    {
        var entry = await BuildAsync(type, id, ct);
        var existing = rebuilding ? null : await _db.JournalEntries.Include(j => j.Lines).FirstOrDefaultAsync(j => j.VoucherType == type && j.VoucherId == id, ct);
        if (lockDate != null && (existing != null || entry != null))
        {
            var touchesLocked = (existing != null && existing.Date.Date <= lockDate.Value.Date) || (entry != null && entry.Date.Date <= lockDate.Value.Date);
            if (touchesLocked && !Same(existing, entry))
                throw new LedgerLockedException($"The books are locked up to {lockDate.Value:dd MMM yyyy}: {Describe(type)} dated on or before then can't be added, changed or deleted.");
        }
        if (existing != null && entry != null && Same(existing, entry)) return;
        if (existing != null) _db.JournalEntries.Remove(existing);
        if (entry != null) _db.JournalEntries.Add(entry);
    }

    private static string Describe(string type) => type switch
    {
        VoucherType.SalePayment => "a sale payment", VoucherType.SalesReturn => "a sales return", VoucherType.PurchasePayment => "a supplier payment",
        VoucherType.PurchaseReturn => "a purchase return", VoucherType.PurchaseReturnRefund => "a supplier refund", VoucherType.FinanceEntry => "a capital / loan entry",
        VoucherType.Expense => "an expense", VoucherType.JournalVoucher => "a journal voucher", VoucherType.Sale => "a sale", VoucherType.Purchase => "a purchase", _ => "a document",
    };

    private static bool Same(JournalEntry? a, JournalEntry? b)
    {
        if (a == null || b == null) return a == b;
        if (a.Date != b.Date || a.Lines.Count != b.Lines.Count || a.Reference != b.Reference || a.Description != b.Description) return false;
        static string K(JournalLine l) => $"{l.AccountId}|{l.Debit:0.00}|{l.Credit:0.00}|{l.PartyType}|{l.PartyId}|{l.PartyName}|{l.Note}";
        return a.Lines.Select(K).OrderBy(x => x).SequenceEqual(b.Lines.Select(K).OrderBy(x => x));
    }

    private async Task<JournalEntry?> BuildAsync(string type, int id, CancellationToken ct) => type switch
    {
        VoucherType.Sale => await SaleAsync(id, ct),
        VoucherType.SalePayment => await SalePaymentAsync(id, ct),
        VoucherType.SalesReturn => await SalesReturnAsync(id, ct),
        VoucherType.Purchase => await PurchaseAsync(id, ct),
        VoucherType.PurchasePayment => await PurchasePaymentAsync(id, ct),
        VoucherType.PurchaseReturn => await PurchaseReturnAsync(id, ct),
        VoucherType.PurchaseReturnRefund => await PurchaseReturnRefundAsync(id, ct),
        VoucherType.Expense => await ExpenseAsync(id, ct),
        VoucherType.FinanceEntry => await FinanceAsync(id, ct),
        VoucherType.JournalVoucher => await JournalVoucherAsync(id, ct),
        _ => null,
    };

    /// <summary>Lines of one entry: + debit, - credit; zero amounts are left out; what doesn't balance goes to Rounding</summary>
    private sealed class Lines
    {
        private readonly List<JournalLine> _lines = new();
        private readonly int _tenant;
        public Lines(int tenant) => _tenant = tenant;
        public Lines Add(int account, decimal amount, string? partyType = null, int? partyId = null, string? partyName = null, string? note = null)
        {
            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (amount == 0) return this;
            _lines.Add(new JournalLine
            {
                TenantId = _tenant, AccountId = account, Debit = amount > 0 ? amount : 0, Credit = amount < 0 ? -amount : 0,
                PartyType = partyType, PartyId = partyId, PartyName = Trim(partyName, 200), Note = Trim(note, 300),
            });
            return this;
        }
        public JournalEntry? Entry(int tenant, DateTime date, string type, int id, string? reference, string? description, int roundingAccount)
        {
            var diff = _lines.Sum(l => l.Debit - l.Credit);
            if (diff != 0) Add(roundingAccount, -diff, note: "difference between the document's total and its lines");
            if (_lines.Count == 0) return null;
            return new JournalEntry { TenantId = tenant, Date = date, VoucherType = type, VoucherId = id, Reference = Trim(reference, 100), Description = Trim(description, 300), Lines = _lines };
        }
    }

    private Lines L() => new(_tenant);
    private JournalEntry? Done(Lines l, DateTime date, string type, int id, string? reference, string? description) =>
        l.Entry(_tenant, date, type, id, reference, description, _chart.Id(LedgerChart.Rounding));

    private static string? Trim(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
    private static bool Internal(string? supplier) => TransactionsController.InternalSuppliers.Contains(supplier ?? "");
    private static bool Cancelled(string? status) => string.Equals(status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase);

    private async Task<StockCost> CostAsync(CancellationToken ct) => _cost ??= await StockCost.LoadAsync(_db, _tenant, null, ct);

    // ── the rules ────────────────────────────────────────────────────────────────────────────────────────────
    private async Task<JournalEntry?> SaleAsync(int id, CancellationToken ct)
    {
        var s = await _db.Sales.Include(x => x.Items).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null || SaleStatus.IsQueued(s.Status) || Cancelled(s.Status)) return null;   // queued: not sold yet (payments are advances)
        var tax = s.OrderTax + s.Items.Sum(i => i.TaxAmount * i.Quantity);
        var income = s.GrandTotal - tax - s.Shipping - s.InstallationCharges;
        var c = LedgerPartyType.Customer;
        var l = L().Add(_chart.Id(LedgerChart.Receivable), s.GrandTotal, c, s.CustomerId, s.CustomerName)
            .Add(_chart.Id(LedgerChart.Sales), -income)
            .Add(_chart.Id(LedgerChart.TaxPayable), -tax)
            .Add(_chart.Id(LedgerChart.ShippingIncome), -s.Shipping)
            .Add(_chart.Id(LedgerChart.InstallationIncome), -s.InstallationCharges);
        var stock = s.Items.Where(i => i.ProductId > 0).ToList();
        if (stock.Count > 0)
        {
            var cost = await CostAsync(ct);
            var total = stock.Sum(i => cost.SaleLine(i.Id));
            l.Add(_chart.Id(LedgerChart.Cogs), total).Add(_chart.Id(LedgerChart.Stock), -total);
        }
        return Done(l, s.SaleDate, VoucherType.Sale, s.Id, s.Reference, $"Sale to {s.CustomerName}");
    }

    private async Task<JournalEntry?> SalePaymentAsync(int id, CancellationToken ct)
    {
        var p = await _db.SalePayments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return null;
        var s = await _db.Sales.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == _tenant && x.Id == p.SaleId)
            .Select(x => new { x.Reference, x.CustomerId, x.CustomerName }).FirstOrDefaultAsync(ct);
        var l = L().Add(await _chart.MoneyAsync(p.PaymentType, ct), p.PayingAmount, note: p.PaymentType)
            .Add(_chart.Id(LedgerChart.Receivable), -p.PayingAmount, LedgerPartyType.Customer, s?.CustomerId, s?.CustomerName);
        return Done(l, p.PaymentDate, VoucherType.SalePayment, p.Id, p.Reference ?? s?.Reference, $"Payment from {s?.CustomerName} for {s?.Reference}");
    }

    private async Task<JournalEntry?> SalesReturnAsync(int id, CancellationToken ct)
    {
        var r = await _db.SalesReturns.Include(x => x.Items).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return null;
        var itemTax = r.Items.Sum(i => i.TaxPercent > 0 ? i.Subtotal - i.Subtotal / (1 + i.TaxPercent / 100m) : 0);
        var tax = r.OrderTax + itemTax;
        var c = LedgerPartyType.Customer;
        var l = L().Add(_chart.Id(LedgerChart.SalesReturns), r.GrandTotal - tax - r.Shipping)
            .Add(_chart.Id(LedgerChart.TaxPayable), tax)
            .Add(_chart.Id(LedgerChart.ShippingIncome), r.Shipping)
            .Add(_chart.Id(LedgerChart.Receivable), -r.GrandTotal, c, r.CustomerId, r.CustomerName);
        if (r.Paid != 0)        // the refund paid to the customer (cash)
            l.Add(_chart.Id(LedgerChart.Receivable), r.Paid, c, r.CustomerId, r.CustomerName, "refund paid")
             .Add(_chart.Id(LedgerChart.Cash), -r.Paid, note: "refund paid");
        var stock = r.Items.Where(i => i.ProductId > 0).ToList();
        if (stock.Count > 0)
        {
            var cost = await CostAsync(ct);
            var total = stock.Sum(i => cost.ReturnLine(i.Id));
            l.Add(_chart.Id(LedgerChart.Stock), total).Add(_chart.Id(LedgerChart.Cogs), -total);
        }
        return Done(l, r.ReturnDate, VoucherType.SalesReturn, r.Id, r.Reference, $"Return from {r.CustomerName}" + (r.SaleReference != null ? $" ({r.SaleReference})" : ""));
    }

    private async Task<JournalEntry?> PurchaseAsync(int id, CancellationToken ct)
    {
        var p = await _db.Purchases.Include(x => x.Items).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null || p.Status != "Received") return null;     // pending / ordered / cancelled: nothing received yet
        var items = p.Items ?? new List<PurchaseItem>();
        var l = L();
        if (Internal(p.SupplierName))
        {
            var value = items.Sum(i => i.TotalCost);       // opening stock / stock added in Manage Stock: brought in by the owner
            l.Add(_chart.Id(LedgerChart.Stock), value).Add(_chart.Id(LedgerChart.OwnerStock), -value);
            return Done(l, p.Date, VoucherType.Purchase, p.Id, p.Reference, p.SupplierName + (p.Notes != null ? $": {p.Notes}" : ""));
        }
        var s = LedgerPartyType.Supplier;
        l.Add(_chart.Id(LedgerChart.Stock), items.Where(i => i.ProductId != null).Sum(i => i.TotalCost))
         .Add(_chart.Id(LedgerChart.NonStockPurchases), items.Where(i => i.ProductId == null).Sum(i => i.TotalCost))
         .Add(_chart.Id(LedgerChart.InputTax), p.OrderTax)
         .Add(_chart.Id(LedgerChart.Freight), p.Shipping, note: p.PurchaseOrderId != null ? "share of the order's tax, shipping and discount" : null)
         .Add(_chart.Id(LedgerChart.PurchaseDiscounts), -p.Discount)
         .Add(_chart.Id(LedgerChart.Payable), -p.Total, s, null, p.SupplierName);
        return Done(l, p.Date, VoucherType.Purchase, p.Id, p.Reference, $"Purchase from {p.SupplierName}");
    }

    private async Task<JournalEntry?> PurchasePaymentAsync(int id, CancellationToken ct)
    {
        var p = await _db.PurchasePayments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null) return null;
        var purchase = await _db.Purchases.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == _tenant && x.Id == p.PurchaseId)
            .Select(x => new { x.Reference, x.SupplierName }).FirstOrDefaultAsync(ct);
        if (Internal(purchase?.SupplierName)) return null;
        var amount = p.Type == PurchasePaymentType.Refund ? -p.Amount : p.Amount;
        var s = LedgerPartyType.Supplier;
        var l = L().Add(_chart.Id(LedgerChart.Payable), amount, s, null, purchase?.SupplierName, p.Method);
        if (p.Method == PurchasePaymentRules.ReturnCredit)      // a purchase return deducted from what is owed: no money moves
            l.Add(_chart.Id(LedgerChart.SupplierReturns), -amount, s, null, purchase?.SupplierName, p.Method);
        else l.Add(await _chart.MoneyAsync(p.Method, ct), -amount, note: p.Method);
        var what = p.Type == PurchasePaymentType.Refund ? "Refund from" : p.Method == PurchasePaymentRules.ReturnCredit ? "Return credit with" : "Payment to";
        return Done(l, p.Date, VoucherType.PurchasePayment, p.Id, p.Reference ?? purchase?.Reference, $"{what} {purchase?.SupplierName} for {purchase?.Reference}");
    }

    private async Task<JournalEntry?> PurchaseReturnAsync(int id, CancellationToken ct)
    {
        var r = await _db.PurchaseReturns.Include(x => x.Items).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r == null) return null;
        var l = L().Add(_chart.Id(LedgerChart.SupplierReturns), r.Total, LedgerPartyType.Supplier, null, r.SupplierName)
            .Add(_chart.Id(LedgerChart.Stock), -r.Items.Where(i => i.ProductId != null).Sum(i => i.Total))
            .Add(_chart.Id(LedgerChart.NonStockPurchases), -r.Items.Where(i => i.ProductId == null).Sum(i => i.Total));
        return Done(l, r.ReturnDate, VoucherType.PurchaseReturn, r.Id, r.Reference, $"Goods returned to {r.SupplierName} ({r.PurchaseReference})");
    }

    private async Task<JournalEntry?> PurchaseReturnRefundAsync(int id, CancellationToken ct)
    {
        var f = await _db.PurchaseReturnRefunds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f == null) return null;
        var r = await _db.PurchaseReturns.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == _tenant && x.Id == f.PurchaseReturnId)
            .Select(x => new { x.Reference, x.SupplierName }).FirstOrDefaultAsync(ct);
        var l = L().Add(await _chart.MoneyAsync(f.Method, ct), f.Amount, note: f.Method)
            .Add(_chart.Id(LedgerChart.SupplierReturns), -f.Amount, LedgerPartyType.Supplier, null, r?.SupplierName);
        return Done(l, f.Date, VoucherType.PurchaseReturnRefund, f.Id, f.Reference ?? r?.Reference, $"Refund from {r?.SupplierName} for return {r?.Reference}");
    }

    private async Task<JournalEntry?> ExpenseAsync(int id, CancellationToken ct)
    {
        var e = await _db.Expenses.Include(x => x.ExpenseCategory).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return null;
        var l = L().Add(await _chart.ExpenseCategoryAsync(e.ExpenseCategoryId, e.ExpenseCategory?.Name, ct), e.Amount, note: e.ExpenseName)
            .Add(_chart.Id(LedgerChart.Cash), -e.Amount);
        return Done(l, e.Date, VoucherType.Expense, e.Id, e.Reference, string.IsNullOrWhiteSpace(e.ExpenseName) ? e.ExpenseCategory?.Name : e.ExpenseName);
    }

    private async Task<JournalEntry?> FinanceAsync(int id, CancellationToken ct)
    {
        var f = await _db.FinanceEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f == null) return null;
        var money = f.Type == FinanceEntryType.Depreciation ? 0 : await _chart.MoneyAsync(f.Method, ct);
        var l = L();
        var name = f.Name.Trim();
        switch (f.Type)
        {
            case FinanceEntryType.OwnerInvestment: l.Add(money, f.Amount, note: f.Method).Add(_chart.Id(LedgerChart.OwnerCapital), -f.Amount, note: name); break;
            case FinanceEntryType.OwnerDrawing: l.Add(_chart.Id(LedgerChart.Drawings), f.Amount, note: name).Add(money, -f.Amount, note: f.Method); break;
            case FinanceEntryType.LoanReceived: l.Add(money, f.Amount, note: f.Method).Add(_chart.Id(LedgerChart.Loans), -f.Amount, LedgerPartyType.Lender, null, name); break;
            case FinanceEntryType.LoanRepayment: l.Add(_chart.Id(LedgerChart.Loans), f.Amount, LedgerPartyType.Lender, null, name).Add(money, -f.Amount, note: f.Method); break;
            case FinanceEntryType.AssetPurchase: l.Add(_chart.Id(LedgerChart.FixedAssets), f.Amount, note: name).Add(money, -f.Amount, note: f.Method); break;
            case FinanceEntryType.Depreciation: l.Add(_chart.Id(LedgerChart.Depreciation), f.Amount, note: name).Add(_chart.Id(LedgerChart.AccDepreciation), -f.Amount, note: name); break;
            default: return null;
        }
        return Done(l, f.Date, VoucherType.FinanceEntry, f.Id, f.Reference, $"{f.Type}: {name}");
    }

    /// <summary>Posted line by line as typed in (the API makes sure debits = credits)</summary>
    private async Task<JournalEntry?> JournalVoucherAsync(int id, CancellationToken ct)
    {
        var v = await _db.JournalVouchers.Include(x => x.Lines).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v == null) return null;
        var l = L();
        foreach (var line in v.Lines.OrderBy(x => x.LineNo).ThenBy(x => x.Id))
            l.Add(line.AccountId, line.Debit - line.Credit, line.PartyType, line.PartyId, line.PartyName, line.Note);
        return Done(l, v.Date, VoucherType.JournalVoucher, v.Id, v.Reference, v.Description);
    }
}

/// <summary>
/// Moving average cost per product, replayed from its stock movements in date order: what comes in (purchases,
/// opening stock, stock entries, sales returns) raises the stock value; what goes out (sales, purchase returns)
/// takes the average cost at that moment. Gives the cost of each sale line and sales return line.
/// </summary>
public class StockCost
{
    private readonly Dictionary<int, decimal> _saleLines = new(), _returnLines = new();
    public decimal SaleLine(int saleItemId) => _saleLines.GetValueOrDefault(saleItemId);
    public decimal ReturnLine(int returnItemId) => _returnLines.GetValueOrDefault(returnItemId);

    private record Move(int ProductId, DateTime Date, int Order, int Id, decimal Qty, decimal Value, int Kind);
    private const int In = 0, SaleOut = 1, ReturnIn = 2, ValueOut = 3;

    public static async Task<StockCost> LoadAsync(AppDbContext db, int tenant, HashSet<int>? products, CancellationToken ct)
    {
        bool P(int? id) => id != null && (products == null || products.Contains(id.Value));
        var moves = new List<Move>();
        // received purchases (opening stock included); stock-entry purchases have no product on their line: their entry is below
        foreach (var x in await (from i in db.PurchaseItems join p in db.Purchases on i.PurchaseId equals p.Id
                                 where p.Status == "Received" && i.ProductId != null select new { i.Id, i.ProductId, i.Quantity, i.TotalCost, p.Date }).ToListAsync(ct))
            if (P(x.ProductId)) moves.Add(new(x.ProductId!.Value, x.Date, 0, x.Id, x.Quantity, x.TotalCost, In));
        // stock entries: valued like their matching purchase ("STOCK-<id>")
        var stockPurchases = (await db.Purchases.Where(p => p.SupplierName == "Stock Entry" && p.Reference.StartsWith("STOCK-")).Select(p => new { p.Reference, p.Total, p.Status }).ToListAsync(ct))
            .Where(p => p.Status == "Received").GroupBy(p => p.Reference).ToDictionary(g => g.Key, g => g.First().Total);
        var prices = await db.Products.IgnoreQueryFilters().Where(p => p.TenantId == tenant).ToDictionaryAsync(p => p.Id, p => p.Price, ct);
        foreach (var e in await db.StockEntries.Select(e => new { e.Id, e.ProductId, e.Quantity, e.Date }).ToListAsync(ct))
            if (P(e.ProductId))
            {
                var value = stockPurchases.TryGetValue($"STOCK-{e.Id}", out var v) ? v : e.Quantity * prices.GetValueOrDefault(e.ProductId);
                moves.Add(new(e.ProductId, e.Date, 0, e.Id, e.Quantity, value, e.Quantity >= 0 ? In : ValueOut));
            }
        foreach (var x in await (from i in db.SalesReturnItems join r in db.SalesReturns on i.SalesReturnId equals r.Id
                                 where i.ProductId != null select new { i.Id, i.ProductId, i.Quantity, r.ReturnDate }).ToListAsync(ct))
            if (P(x.ProductId)) moves.Add(new(x.ProductId!.Value, x.ReturnDate, 1, x.Id, x.Quantity, 0, ReturnIn));
        foreach (var x in await (from i in db.PurchaseReturnItems join r in db.PurchaseReturns on i.PurchaseReturnId equals r.Id
                                 where i.ProductId != null select new { i.Id, i.ProductId, i.Quantity, i.Total, r.ReturnDate }).ToListAsync(ct))
            if (P(x.ProductId)) moves.Add(new(x.ProductId!.Value, x.ReturnDate, 2, x.Id, -x.Quantity, -x.Total, ValueOut));
        foreach (var x in await (from i in db.SaleItems join s in db.Sales on i.SaleId equals s.Id
                                 where s.Status != SaleStatus.Queued && s.Status != "Cancelled" && i.ProductId > 0
                                 select new { i.Id, i.ProductId, i.Quantity, s.SaleDate }).ToListAsync(ct))
            if (P(x.ProductId)) moves.Add(new(x.ProductId, x.SaleDate, 3, x.Id, -x.Quantity, 0, SaleOut));

        var cost = new StockCost();
        foreach (var product in moves.GroupBy(m => m.ProductId))
        {
            decimal qty = 0, value = 0, lastAverage = 0;
            foreach (var m in product.OrderBy(m => m.Date).ThenBy(m => m.Order).ThenBy(m => m.Id))
            {
                var average = qty > 0 ? value / qty : lastAverage;
                switch (m.Kind)
                {
                    case In: qty += m.Qty; value += m.Value; break;
                    case ValueOut: qty += m.Qty; value += m.Value; break;          // leaves at its own value (purchase return, stock removed)
                    case ReturnIn:
                        var back = Math.Round(m.Qty * average, 2);
                        cost._returnLines[m.Id] = back; qty += m.Qty; value += back; break;
                    case SaleOut:
                        var out_ = Math.Round(-m.Qty * average, 2);
                        cost._saleLines[m.Id] = out_; qty += m.Qty; value -= out_; break;
                }
                if (qty > 0) lastAverage = value / qty;
                else value = 0;      // nothing (or less than nothing) left: no value left; the last average is kept
            }
        }
        return cost;
    }
}
