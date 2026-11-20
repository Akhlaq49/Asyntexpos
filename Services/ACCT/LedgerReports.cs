using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// Reports read only the ledger (JournalLines): trial balance, profit and loss (accrual or cash basis), balance sheet,
/// account ledger, journal, customer / supplier balances. The balance sheet balances only when every entry did, so
/// it is also a check. Dates: "to" / "as of" include the whole day.
/// </summary>
public class LedgerReports
{
    private readonly AppDbContext _db;
    public LedgerReports(AppDbContext db) => _db = db;

    public record AccountRow(int Id, string Code, string Name, string RootType, int? ParentId, bool IsGroup, string? SystemKey, bool IsActive);
    private record Line(int EntryId, DateTime Date, string VoucherType, int VoucherId, string? Reference, string? Description,
        int AccountId, decimal Debit, decimal Credit, string? PartyType, int? PartyId, string? PartyName, string? Note);

    public async Task EnsureFreshAsync(CancellationToken ct) => await new LedgerPoster(_db).EnsureFreshAsync(ct);

    public async Task<List<AccountRow>> AccountsAsync(CancellationToken ct) =>
        (await _db.LedgerAccounts.AsNoTracking().OrderBy(a => a.Code).ToListAsync(ct))
        .Select(a => new AccountRow(a.Id, a.Code, a.Name, a.RootType, a.ParentId, a.IsGroup, a.SystemKey, a.IsActive)).ToList();

    private async Task<List<Line>> LinesAsync(DateTime? from, DateTime? toExclusive, CancellationToken ct)
    {
        var q = from l in _db.JournalLines.AsNoTracking()
                join e in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id
                select new { l, e };
        if (from != null) q = q.Where(x => x.e.Date >= from);
        if (toExclusive != null) q = q.Where(x => x.e.Date < toExclusive);
        return (await q.ToListAsync(ct)).Select(x => new Line(x.e.Id, x.e.Date, x.e.VoucherType, x.e.VoucherId, x.e.Reference, x.e.Description,
            x.l.AccountId, x.l.Debit, x.l.Credit, x.l.PartyType, x.l.PartyId, x.l.PartyName, x.l.Note)).ToList();
    }

    private static DateTime? End(DateTime? to) => to?.Date.AddDays(1);
    /// <summary>The amount in the account's normal direction (debit for Asset / Expense, credit for the others)</summary>
    private static decimal Natural(string rootType, decimal debitMinusCredit) => LedgerRootType.IsDebitNormal(rootType) ? debitMinusCredit : -debitMinusCredit;

    // ── trial balance ────────────────────────────────────────────────────────────────────────────────────────
    public async Task<object> TrialBalanceAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var accounts = await AccountsAsync(ct);
        var lines = await LinesAsync(null, End(to), ct);
        var rows = accounts.Where(a => !a.IsGroup).Select(a =>
        {
            var mine = lines.Where(l => l.AccountId == a.Id).ToList();
            var opening = from == null ? 0 : mine.Where(l => l.Date < from).Sum(l => l.Debit - l.Credit);
            var period = from == null ? mine : mine.Where(l => l.Date >= from).ToList();
            var debit = period.Sum(l => l.Debit); var credit = period.Sum(l => l.Credit);
            var closing = opening + debit - credit;
            return new { a.Id, a.Code, a.Name, a.RootType, opening, debit, credit, closing,
                closingDebit = closing > 0 ? closing : 0, closingCredit = closing < 0 ? -closing : 0 };
        }).Where(r => r.opening != 0 || r.debit != 0 || r.credit != 0).ToList();
        return new
        {
            from, to, rows,
            totals = new { debit = rows.Sum(r => r.debit), credit = rows.Sum(r => r.credit), closingDebit = rows.Sum(r => r.closingDebit), closingCredit = rows.Sum(r => r.closingCredit) },
            balanced = rows.Sum(r => r.closingDebit) == rows.Sum(r => r.closingCredit),
        };
    }

    // ── year-based reports (accumulated balances, comparison, GL balances by month) ─────────────────────────────
    // Balance sheet accounts carry their balance from year to year. Income and expense accounts start each financial year
    // at zero: what they held before is shown on one line, "Profit of earlier years" (equity), so the totals still balance.
    // Amounts are signed: + debit, - credit.

    public const string EarlierProfitName = "Profit of earlier years (carried forward)";

    public async Task<int> FiscalYearStartMonthAsync(CancellationToken ct) =>
        (await _db.AccountingSettings.AsNoTracking().Select(s => (int?)s.FiscalYearStartMonth).FirstOrDefaultAsync(ct)) is int m and >= 1 and <= 12 ? m : 1;

    /// <summary>First day of the financial year that contains <paramref name="date"/></summary>
    public static DateTime FiscalYearStart(DateTime date, int startMonth)
    {
        var start = new DateTime(date.Year, startMonth, 1);
        return date.Date >= start ? start : start.AddYears(-1);
    }

    private static decimal Sum(IEnumerable<Line> lines) => lines.Sum(l => l.Debit - l.Credit);

    /// <summary>
    /// Each account from the start of the financial year that contains "from": balance carried forward into the year,
    /// balance of the earlier periods of the year (year start to "from"), their total, the debits and credits from "from"
    /// to "to", the debits and credits of the whole year up to "to", and the accumulated balance at "to".
    /// </summary>
    public async Task<object> AccountBalancesAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        from = from.Date;
        var year = FiscalYearStart(from, await FiscalYearStartMonthAsync(ct));
        var accounts = await AccountsAsync(ct);
        var byAccount = (await LinesAsync(null, End(to), ct)).GroupBy(l => l.AccountId).ToDictionary(g => g.Key, g => g.ToList());
        var rows = new List<object>();
        decimal earlierProfit = 0;
        foreach (var a in accounts.Where(a => !a.IsGroup))
        {
            var mine = byAccount.GetValueOrDefault(a.Id) ?? new List<Line>();
            var before = Sum(mine.Where(l => l.Date < year));
            var pl = LedgerRootType.IsProfitAndLoss(a.RootType);
            if (pl) earlierProfit += before;
            var carry = pl ? 0 : before;
            var previous = Sum(mine.Where(l => l.Date >= year && l.Date < from));
            var period = mine.Where(l => l.Date >= from).ToList();
            var yearLines = mine.Where(l => l.Date >= year).ToList();
            decimal debit = period.Sum(l => l.Debit), credit = period.Sum(l => l.Credit);
            decimal cumDebit = yearLines.Sum(l => l.Debit), cumCredit = yearLines.Sum(l => l.Credit);
            if (carry == 0 && previous == 0 && cumDebit == 0 && cumCredit == 0) continue;
            rows.Add(new { a.Id, a.Code, a.Name, a.RootType, carryforward = carry, previousPeriods = previous, accumulatedPrevious = carry + previous,
                debit, credit, cumulativeDebit = cumDebit, cumulativeCredit = cumCredit, accumulated = carry + cumDebit - cumCredit });
        }
        if (earlierProfit != 0)
            rows.Add(new { Id = 0, Code = "", Name = EarlierProfitName, RootType = LedgerRootType.Equity, carryforward = earlierProfit, previousPeriods = 0m,
                accumulatedPrevious = earlierProfit, debit = 0m, credit = 0m, cumulativeDebit = 0m, cumulativeCredit = 0m, accumulated = earlierProfit });
        return new { from, to, fiscalYearStart = year, rows };
    }

    /// <summary>
    /// The reporting period next to a comparison period: balance sheet accounts at the end of each period, income and
    /// expense accounts for the period itself; the difference (reporting - comparison) and the change in percent.
    /// </summary>
    public async Task<object> ComparisonAsync(DateTime from, DateTime to, DateTime cmpFrom, DateTime cmpTo, CancellationToken ct)
    {
        var accounts = await AccountsAsync(ct);
        var lines = await LinesAsync(null, End(to > cmpTo ? to : cmpTo), ct);
        var byAccount = lines.GroupBy(l => l.AccountId).ToDictionary(g => g.Key, g => g.ToList());
        // one period: balance sheet accounts up to its end, income / expense within it, earlier income / expense on one line
        (Dictionary<int, decimal> Amounts, decimal Earlier) Period(DateTime f, DateTime t)
        {
            var end = End(t)!.Value;
            var amounts = new Dictionary<int, decimal>();
            decimal earlier = 0;
            foreach (var a in accounts.Where(a => !a.IsGroup))
            {
                var mine = (byAccount.GetValueOrDefault(a.Id) ?? new List<Line>()).Where(l => l.Date < end).ToList();
                if (LedgerRootType.IsProfitAndLoss(a.RootType))
                {
                    earlier += Sum(mine.Where(l => l.Date < f.Date));
                    amounts[a.Id] = Sum(mine.Where(l => l.Date >= f.Date));
                }
                else amounts[a.Id] = Sum(mine);
            }
            return (amounts, earlier);
        }
        var current = Period(from, to);
        var comparison = Period(cmpFrom, cmpTo);
        static object Row(int id, string code, string name, string root, decimal cur, decimal cmp) => new
        {
            Id = id, Code = code, Name = name, RootType = root, current = cur, comparison = cmp, difference = cur - cmp,
            percent = cmp == 0 ? (decimal?)null : Math.Round((cur - cmp) / Math.Abs(cmp) * 100, 1),
        };
        var rows = accounts.Where(a => !a.IsGroup)
            .Select(a => (a, cur: current.Amounts[a.Id], cmp: comparison.Amounts[a.Id]))
            .Where(x => x.cur != 0 || x.cmp != 0)
            .Select(x => Row(x.a.Id, x.a.Code, x.a.Name, x.a.RootType, x.cur, x.cmp)).ToList();
        if (current.Earlier != 0 || comparison.Earlier != 0)
            rows.Add(Row(0, "", "Profit of earlier periods", LedgerRootType.Equity, current.Earlier, comparison.Earlier));
        return new { from, to, cmpFrom, cmpTo, rows };
    }

    /// <summary>
    /// Selected accounts by month for one financial year: balance carried forward, then each month's debits, credits,
    /// balance (debits - credits) and cumulative balance. Several accounts are added together.
    /// </summary>
    public async Task<object> GlBalancesAsync(IReadOnlyCollection<int> accountIds, int fiscalYear, CancellationToken ct)
    {
        var startMonth = await FiscalYearStartMonthAsync(ct);
        var start = new DateTime(fiscalYear, startMonth, 1);
        var end = start.AddYears(1);
        var accounts = (await AccountsAsync(ct)).Where(a => !a.IsGroup && accountIds.Contains(a.Id)).ToList();
        var ids = accounts.Select(a => a.Id).ToHashSet();
        var pl = accounts.Where(a => LedgerRootType.IsProfitAndLoss(a.RootType)).Select(a => a.Id).ToHashSet();
        var lines = (await LinesAsync(null, end, ct)).Where(l => ids.Contains(l.AccountId)).ToList();
        var carry = Sum(lines.Where(l => l.Date < start && !pl.Contains(l.AccountId)));
        var running = carry;
        var periods = Enumerable.Range(0, 12).Select(i =>
        {
            var f = start.AddMonths(i); var t = f.AddMonths(1);
            var mine = lines.Where(l => l.Date >= f && l.Date < t).ToList();
            decimal debit = mine.Sum(l => l.Debit), credit = mine.Sum(l => l.Credit);
            running += debit - credit;
            return new { period = i + 1, from = f, to = t.AddDays(-1), debit, credit, balance = debit - credit, cumulative = running };
        }).ToList();
        return new
        {
            fiscalYear, fiscalYearStart = start, fiscalYearEnd = end.AddDays(-1),
            accounts = accounts.Select(a => new { a.Id, a.Code, a.Name, a.RootType }),
            carryforward = carry, periods,
            totals = new { debit = periods.Sum(p => p.debit), credit = periods.Sum(p => p.credit), balance = periods.Sum(p => p.balance), closing = running },
        };
    }

    // ── profit and loss ──────────────────────────────────────────────────────────────────────────────────────
    /// <summary>accrual: income when sold, cost when sold. cash: a sale's income, tax and cost count as it is paid
    /// (each payment's share of the sale total); everything else (expenses, returns ...) on its date.</summary>
    public async Task<object> ProfitAndLossAsync(DateTime from, DateTime to, string basis, CancellationToken ct)
    {
        var cash = string.Equals(basis, "cash", StringComparison.OrdinalIgnoreCase);
        var accounts = (await AccountsAsync(ct)).ToDictionary(a => a.Id);
        var plIds = accounts.Values.Where(a => LedgerRootType.IsProfitAndLoss(a.RootType)).Select(a => a.Id).ToHashSet();
        var amounts = new Dictionary<int, decimal>();     // account -> debit - credit
        void Add(int account, decimal v) => amounts[account] = amounts.GetValueOrDefault(account) + v;
        if (!cash)
        {
            foreach (var l in (await LinesAsync(from.Date, End(to), ct)).Where(l => plIds.Contains(l.AccountId))) Add(l.AccountId, l.Debit - l.Credit);
        }
        else
        {
            foreach (var (account, value) in await CashBasisAsync(from.Date, End(to)!.Value, accounts.Values, plIds, ct)) Add(account, value);
        }
        object Rows(string root) => amounts.Where(x => accounts[x.Key].RootType == root && Math.Round(x.Value, 2) != 0)
            .Select(x => new { accountId = x.Key, accounts[x.Key].Code, accounts[x.Key].Name, accounts[x.Key].SystemKey, amount = Math.Round(Natural(root, x.Value), 2) })
            .OrderBy(x => x.Code).ToList();
        decimal Sum(string root) => Math.Round(amounts.Where(x => accounts[x.Key].RootType == root).Sum(x => Natural(root, x.Value)), 2);
        decimal Key(string key) => Math.Round(amounts.Where(x => accounts[x.Key].SystemKey == key).Sum(x => x.Value), 2);
        var income = Sum(LedgerRootType.Income); var expense = Sum(LedgerRootType.Expense);
        // gross profit: sales income less returns less the cost of what was sold
        var grossProfit = -Key(LedgerChart.Sales) - Key(LedgerChart.ShippingIncome) - Key(LedgerChart.SalesReturns) - Key(LedgerChart.Cogs);
        return new { from, to, basis = cash ? "cash" : "accrual", income = Rows(LedgerRootType.Income), expenses = Rows(LedgerRootType.Expense),
            totalIncome = income, totalExpenses = expense, grossProfit, netProfit = income - expense };
    }

    /// <summary>
    /// Cash basis: income and expenses count when the money moves, in proportion to what has been paid.
    ///   Sale: as the customer pays it (its payments' share of the sale total).
    ///   Purchase: as the supplier is paid for it (shipping, discount and items not kept in stock), return credits not counted.
    ///   Any other entry: the part paid in cash / bank counts on its date; the part left on Receivables / Payables /
    ///   supplier returns counts when that customer's / supplier's account is paid by a later voucher, oldest first.
    ///   An entry that moves no money (a reclass from one expense to another, depreciation, an accrual) is not on the
    ///   cash basis at all; the accrual basis shows everything.
    /// Returns account -> (debit - credit) for the period [from, toExclusive).
    /// </summary>
    private async Task<Dictionary<int, decimal>> CashBasisAsync(DateTime from, DateTime toExclusive, IEnumerable<AccountRow> accounts, HashSet<int> plIds, CancellationToken ct)
    {
        var keyOf = accounts.Where(a => a.SystemKey != null).ToDictionary(a => a.Id, a => a.SystemKey!);
        bool IsMoney(int id) => keyOf.TryGetValue(id, out var k) && k is LedgerChart.Cash or LedgerChart.Bank;
        bool IsOnAccount(int id) => keyOf.TryGetValue(id, out var k) && k is LedgerChart.Receivable or LedgerChart.Payable or LedgerChart.SupplierReturns;
        var amounts = new Dictionary<int, decimal>();
        var entries = (await LinesAsync(null, toExclusive, ct)).GroupBy(l => l.EntryId).Select(g => g.ToList()).OrderBy(g => g[0].Date).ThenBy(g => g[0].EntryId).ToList();

        // the share of an entry that counts in the period, given what was paid before the period and up to its end
        void Count(List<Line> entry, decimal shareBefore, decimal shareUpTo)
        {
            var share = Math.Clamp(shareUpTo, 0, 1) - Math.Clamp(shareBefore, 0, 1);
            if (share == 0) return;
            foreach (var l in entry.Where(l => plIds.Contains(l.AccountId))) amounts[l.AccountId] = amounts.GetValueOrDefault(l.AccountId) + (l.Debit - l.Credit) * share;
        }

        // sales and purchases: by the payments recorded against them
        var saleIds = entries.Where(e => e[0].VoucherType == VoucherType.Sale).Select(e => e[0].VoucherId).ToList();
        var saleTotals = await _db.Sales.AsNoTracking().Where(s => saleIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.GrandTotal, ct);
        var salePaid = (await _db.SalePayments.AsNoTracking().Where(p => saleIds.Contains(p.SaleId) && p.PaymentDate < toExclusive)
            .Select(p => new { p.SaleId, p.PayingAmount, p.PaymentDate }).ToListAsync(ct)).ToLookup(p => p.SaleId);
        var purchaseIds = entries.Where(e => e[0].VoucherType == VoucherType.Purchase).Select(e => e[0].VoucherId).ToList();
        var purchaseTotals = await _db.Purchases.AsNoTracking().Where(p => purchaseIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Total, ct);
        var purchasePaid = (await _db.PurchasePayments.AsNoTracking().Where(p => purchaseIds.Contains(p.PurchaseId) && p.Date < toExclusive).ToListAsync(ct))
            .ToLookup(p => p.PurchaseId);

        // the parts of entries left on a customer's / supplier's account, waiting to be paid (oldest first)
        var waiting = new Dictionary<string, List<(List<Line> Entry, decimal Amount, decimal Share)>>();
        static string Party(Line l) => $"{l.AccountId}|{l.PartyId}|{(l.PartyName ?? "").Trim().ToLowerInvariant()}";

        foreach (var entry in entries)
        {
            var e = entry[0];
            var hasPl = entry.Any(l => plIds.Contains(l.AccountId));
            if (e.VoucherType == VoucherType.Sale)
            {
                var total = saleTotals.GetValueOrDefault(e.VoucherId);
                if (hasPl && total > 0)
                    Count(entry, salePaid[e.VoucherId].Where(p => p.PaymentDate < from).Sum(p => p.PayingAmount) / total, salePaid[e.VoucherId].Sum(p => p.PayingAmount) / total);
                continue;
            }
            if (e.VoucherType == VoucherType.Purchase)
            {
                var total = purchaseTotals.GetValueOrDefault(e.VoucherId);
                if (hasPl && total > 0)
                    Count(entry, PurchasePaymentRules.CashPaid(purchasePaid[e.VoucherId].Where(p => p.Date < from)) / total, PurchasePaymentRules.CashPaid(purchasePaid[e.VoucherId]) / total);
                continue;
            }
            if (e.VoucherType is VoucherType.SalePayment or VoucherType.PurchasePayment) continue;      // they pay a sale / purchase (above)

            var money = Math.Abs(entry.Where(l => IsMoney(l.AccountId)).Sum(l => l.Debit - l.Credit));
            var onAccount = entry.Where(l => IsOnAccount(l.AccountId)).GroupBy(Party).Select(g => (Key: g.Key, Net: g.Sum(l => l.Debit - l.Credit))).Where(g => g.Net != 0).ToList();
            if (hasPl)
            {
                // the other side of the income / expense: money, a customer's / supplier's account, or something else that is
                // no money (accumulated depreciation, an accrual, a loan ...): that part never shows on the cash basis
                var other = entry.Where(l => !plIds.Contains(l.AccountId) && !IsMoney(l.AccountId) && !IsOnAccount(l.AccountId))
                    .GroupBy(l => l.AccountId).Sum(g => Math.Abs(g.Sum(l => l.Debit - l.Credit)));
                var offset = money + onAccount.Sum(g => Math.Abs(g.Net)) + other;
                if (offset == 0) continue;      // only income / expense accounts moved (a reclass): no money, not on the cash basis
                if (money > 0 && e.Date >= from) Count(entry, 0, money / offset);         // the part paid now
                foreach (var g in onAccount)                                                // the part left owing: paid later
                {
                    if (!waiting.TryGetValue(g.Key, out var queue)) waiting[g.Key] = queue = new();
                    queue.Add((entry, g.Net, Math.Abs(g.Net) / offset));
                }
                continue;
            }
            // a voucher that pays (or collects) what is owed with money: it settles the oldest waiting parts of that account
            if (money == 0) continue;
            foreach (var g in onAccount)
            {
                if (!waiting.TryGetValue(g.Key, out var queue)) continue;
                var left = Math.Abs(g.Net);
                for (int i = 0; i < queue.Count && left > 0; i++)
                {
                    var (owed, amount, share) = queue[i];
                    if (Math.Sign(amount) == Math.Sign(g.Net) || amount == 0) continue;     // same direction: not a payment of it
                    var paid = Math.Min(left, Math.Abs(amount));
                    if (e.Date >= from) Count(owed, 0, share * paid / Math.Abs(amount));
                    left -= paid;
                    queue[i] = (owed, amount - Math.Sign(amount) * paid, share * (Math.Abs(amount) - paid) / Math.Abs(amount));
                }
                queue.RemoveAll(q => q.Amount == 0);
            }
        }
        return amounts;
    }

    // ── balance sheet ────────────────────────────────────────────────────────────────────────────────────────
    public async Task<object> BalanceSheetAsync(DateTime asOf, CancellationToken ct)
    {
        var accounts = (await AccountsAsync(ct)).ToDictionary(a => a.Id);
        var lines = await LinesAsync(null, End(asOf), ct);
        var byAccount = lines.GroupBy(l => l.AccountId).ToDictionary(g => g.Key, g => g.ToList());
        var assets = new List<object>(); var liabilities = new List<object>(); var equity = new List<object>();
        decimal totalAssets = 0, totalLiabilities = 0, totalEquity = 0;
        // customers / suppliers: each one on the side its own balance is on (an advance is not netted against a debt)
        var split = new Dictionary<string, (string Plus, string Minus)>
        {
            [LedgerChart.Receivable] = ("Customers owe you", "Customers paid in advance / refunds owed"),
            [LedgerChart.Payable] = ("Paid to suppliers in advance", "You owe suppliers"),
            [LedgerChart.SupplierReturns] = ("Suppliers owe you (returns)", "Owed back to suppliers (returns)"),
        };
        foreach (var a in accounts.Values.Where(a => !a.IsGroup && !LedgerRootType.IsProfitAndLoss(a.RootType)).OrderBy(a => a.Code))
        {
            var mine = byAccount.GetValueOrDefault(a.Id) ?? new List<Line>();
            if (a.SystemKey != null && split.TryGetValue(a.SystemKey, out var labels))
            {
                var parties = mine.GroupBy(l => (l.PartyId?.ToString() ?? "") + "|" + (l.PartyName ?? "").Trim().ToLowerInvariant())
                    .Select(g => new { name = g.Select(l => l.PartyName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "(no name)", partyId = g.First().PartyId, balance = Math.Round(g.Sum(l => l.Debit - l.Credit), 2) })
                    .Where(p => p.balance != 0).ToList();
                var plus = parties.Where(p => p.balance > 0).OrderByDescending(p => p.balance).ToList();
                var minus = parties.Where(p => p.balance < 0).OrderBy(p => p.balance).Select(p => new { p.name, p.partyId, balance = -p.balance }).ToList();
                if (plus.Count > 0) { var t = plus.Sum(p => p.balance); assets.Add(new { accountId = a.Id, a.Code, name = labels.Plus, amount = t, parties = plus }); totalAssets += t; }
                if (minus.Count > 0) { var t = minus.Sum(p => p.balance); liabilities.Add(new { accountId = a.Id, a.Code, name = labels.Minus, amount = t, parties = minus }); totalLiabilities += t; }
                continue;
            }
            var balance = Math.Round(mine.Sum(l => l.Debit - l.Credit), 2);
            if (balance == 0) continue;
            var amount = Natural(a.RootType, balance);
            var row = new { accountId = a.Id, a.Code, a.Name, amount, parties = (object?)null };
            switch (a.RootType)
            {
                case LedgerRootType.Asset: assets.Add(row); totalAssets += amount; break;
                case LedgerRootType.Liability: liabilities.Add(row); totalLiabilities += amount; break;
                default: equity.Add(row); totalEquity += amount; break;
            }
        }
        // income - expenses to date: the profit kept in the business (no year has been closed into Retained profit yet)
        var plIds = accounts.Values.Where(a => LedgerRootType.IsProfitAndLoss(a.RootType)).Select(a => a.Id).ToHashSet();
        var profit = Math.Round(-lines.Where(l => plIds.Contains(l.AccountId)).Sum(l => l.Debit - l.Credit), 2);
        equity.Add(new { accountId = 0, Code = "", Name = "Profit to date", amount = profit, parties = (object?)null });
        totalEquity += profit;
        return new
        {
            asOf, assets, liabilities, equity,
            totalAssets, totalLiabilities, totalEquity, totalLiabilitiesAndEquity = totalLiabilities + totalEquity,
            balanced = totalAssets == totalLiabilities + totalEquity,
        };
    }

    // ── account ledger (with a running balance) ──────────────────────────────────────────────────────────────
    public async Task<object> AccountLedgerAsync(int accountId, DateTime? from, DateTime? to, string? party, CancellationToken ct)
    {
        var account = await _db.LedgerAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct) ?? throw new KeyNotFoundException("Account not found.");
        var lines = (await LinesAsync(null, End(to), ct)).Where(l => l.AccountId == accountId);
        if (!string.IsNullOrWhiteSpace(party)) lines = lines.Where(l => string.Equals((l.PartyName ?? "").Trim(), party.Trim(), StringComparison.OrdinalIgnoreCase));
        var list = lines.OrderBy(l => l.Date).ThenBy(l => l.EntryId).ToList();
        var opening = from == null ? 0 : list.Where(l => l.Date < from).Sum(l => l.Debit - l.Credit);
        var running = opening;
        var rows = list.Where(l => from == null || l.Date >= from).Select(l =>
        {
            running += l.Debit - l.Credit;
            return new { l.EntryId, l.Date, l.VoucherType, l.VoucherId, l.Reference, l.Description, l.PartyName, l.Note, l.Debit, l.Credit,
                balance = Natural(account.RootType, running) };
        }).ToList();
        return new { account = new { account.Id, account.Code, account.Name, account.RootType }, from, to, party,
            opening = Natural(account.RootType, opening), rows, closing = Natural(account.RootType, running),
            totalDebit = rows.Sum(r => r.Debit), totalCredit = rows.Sum(r => r.Credit) };
    }

    // ── journal (the entries with their lines) ───────────────────────────────────────────────────────────────
    public async Task<object> JournalAsync(DateTime? from, DateTime? to, string? voucherType, string? search, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.JournalEntries.AsNoTracking().AsQueryable();
        if (from != null) q = q.Where(e => e.Date >= from);
        if (to != null) { var end = End(to); q = q.Where(e => e.Date < end); }
        if (!string.IsNullOrWhiteSpace(voucherType)) q = q.Where(e => e.VoucherType == voucherType);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(e => (e.Reference ?? "").Contains(search) || (e.Description ?? "").Contains(search));
        var total = await q.CountAsync(ct);
        pageSize = Math.Clamp(pageSize, 10, 200); page = Math.Max(1, page);
        var entries = await q.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize).Include(e => e.Lines).ToListAsync(ct);
        var names = await _db.LedgerAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => new { a.Code, a.Name }, ct);
        return new
        {
            total, page, pageSize,
            entries = entries.Select(e => new
            {
                e.Id, e.Date, e.VoucherType, e.VoucherId, e.Reference, e.Description,
                lines = e.Lines.OrderByDescending(l => l.Debit).Select(l => new { l.AccountId, account = names.GetValueOrDefault(l.AccountId)?.Name, code = names.GetValueOrDefault(l.AccountId)?.Code,
                    l.Debit, l.Credit, l.PartyType, l.PartyName, l.Note }),
            }),
        };
    }

    /// <summary>One entry (voucher) with all its lines: the drill-down from an account's posting to the whole voucher</summary>
    public async Task<object?> EntryAsync(int entryId, CancellationToken ct)
    {
        var e = await _db.JournalEntries.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == entryId, ct);
        if (e == null) return null;
        var ids = e.Lines.Select(l => l.AccountId).Distinct().ToList();
        var names = await _db.LedgerAccounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => new { a.Code, a.Name }, ct);
        return new
        {
            e.Id, e.Date, e.VoucherType, e.VoucherId, e.Reference, e.Description,
            lines = e.Lines.OrderByDescending(l => l.Debit).ThenBy(l => l.Id).Select(l => new { l.AccountId, account = names.GetValueOrDefault(l.AccountId)?.Name,
                code = names.GetValueOrDefault(l.AccountId)?.Code, l.Debit, l.Credit, l.PartyType, l.PartyName, l.Note }),
        };
    }

    // ── customer / supplier / lender balances ────────────────────────────────────────────────────────────────
    public async Task<object> PartiesAsync(string partyType, DateTime? asOf, CancellationToken ct)
    {
        var lines = (await LinesAsync(null, End(asOf), ct)).Where(l => l.PartyType == partyType).ToList();
        var accounts = await _db.LedgerAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);
        return lines.GroupBy(l => (l.PartyName ?? "").Trim().ToLowerInvariant())
            .Select(g => new
            {
                name = g.Select(l => l.PartyName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "(no name)",
                // customers: + they owe you; suppliers / lenders: + you owe them
                balance = Math.Round(partyType == LedgerPartyType.Customer ? g.Sum(l => l.Debit - l.Credit) : g.Sum(l => l.Credit - l.Debit), 2),
                accounts = g.GroupBy(l => l.AccountId).Select(a => new { accountId = a.Key, accounts[a.Key].Name, balance = Math.Round(a.Sum(l => l.Debit - l.Credit), 2) }),
                lastDate = g.Max(l => l.Date),
            })
            .Where(p => p.balance != 0).OrderByDescending(p => Math.Abs(p.balance)).ToList();
    }

    public async Task<object> CheckAsync(CancellationToken ct)
    {
        var settings = await _db.AccountingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var unbalanced = await _db.JournalEntries.AsNoTracking().Where(e => e.Lines.Sum(l => l.Debit) != e.Lines.Sum(l => l.Credit))
            .Select(e => new { e.Id, e.VoucherType, e.VoucherId, e.Reference }).Take(50).ToListAsync(ct);
        return new { settings?.LockDate, fiscalYearStartMonth = settings?.FiscalYearStartMonth ?? 1, settings?.RebuiltAt, settings?.LastError, settings?.NeedsRebuild, rulesVersion = settings?.RulesVersion,
            entries = await _db.JournalEntries.CountAsync(ct), unbalanced, stockMismatches = await StockMismatchesAsync(ct) };
    }

    /// <summary>Products whose quantity differs from their stock movements (purchases, stock entries, sales, returns):
    /// the quantity was changed on the product itself, so the ledger (which counts documents) can't see it.
    /// Fix: a stock entry for the difference.</summary>
    public async Task<List<object>> StockMismatchesAsync(CancellationToken ct)
    {
        // summed here, not in SQL: the quantities are decimals (SQLite can't sum those)
        var bought = (await (from i in _db.PurchaseItems join p in _db.Purchases on i.PurchaseId equals p.Id
                             where p.Status == "Received" && i.ProductId != null select new { Id = i.ProductId!.Value, i.Quantity }).ToListAsync(ct))
            .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var entries = (await _db.StockEntries.Select(e => new { e.ProductId, e.Quantity }).ToListAsync(ct))
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => (decimal)g.Sum(x => x.Quantity));
        var sold = (await (from i in _db.SaleItems join s in _db.Sales on i.SaleId equals s.Id
                           where s.Status != "Cancelled" && i.ProductId > 0 select new { i.ProductId, i.Quantity }).ToListAsync(ct))
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => (decimal)g.Sum(x => x.Quantity));
        var returned = (await _db.SalesReturnItems.Where(i => i.ProductId != null).Select(i => new { Id = i.ProductId!.Value, i.Quantity }).ToListAsync(ct))
            .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => (decimal)g.Sum(x => x.Quantity));
        var sentBack = (await _db.PurchaseReturnItems.Where(i => i.ProductId != null).Select(i => new { Id = i.ProductId!.Value, i.Quantity }).ToListAsync(ct))
            .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        return (await _db.Products.AsNoTracking().Select(p => new { p.Id, p.ProductName, p.SKU, p.Quantity }).ToListAsync(ct))
            .Select(p => new { p, moved = bought.GetValueOrDefault(p.Id) + entries.GetValueOrDefault(p.Id) - sold.GetValueOrDefault(p.Id) + returned.GetValueOrDefault(p.Id) - sentBack.GetValueOrDefault(p.Id) })
            .Where(x => x.moved != x.p.Quantity)
            .Select(x => (object)new { productId = x.p.Id, x.p.ProductName, x.p.SKU, quantity = x.p.Quantity, fromMovements = x.moved, difference = x.p.Quantity - x.moved })
            .ToList();
    }
}
