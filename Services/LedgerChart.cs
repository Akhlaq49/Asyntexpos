using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// The company's chart of accounts, in the company's numbering (6 digits: xx0000 = a group, the rest are accounts).
/// The posting rules find accounts by SystemKey; a missing rule account is created. A new company gets the whole
/// chart; the accounts no rule posts to ("coa:" keys) can be deleted. Each expense category gets its own account
/// under 661000 General Administration.
/// </summary>
public class LedgerChart
{
    public const string Assets = "assets", CashBank = "cash-bank", Liabilities = "liabilities", Equity = "equity",
        Income = "income", Expenses = "expenses", ExpenseCategories = "expense-categories";
    public const string Cash = "cash", Bank = "bank", Receivable = "receivable", Stock = "stock", SupplierReturns = "supplier-returns",
        InputTax = "input-tax", FixedAssets = "fixed-assets", AccDepreciation = "acc-depreciation",
        Payable = "payable", TaxPayable = "tax-payable", Loans = "loans",
        OwnerCapital = "owner-capital", OwnerStock = "owner-stock", Drawings = "drawings", Retained = "retained",
        Sales = "sales", ShippingIncome = "shipping-income", InstallationIncome = "installation-income", SalesReturns = "sales-returns",
        PurchaseDiscounts = "purchase-discounts",
        Cogs = "cogs", Freight = "freight", NonStockPurchases = "non-stock-purchases", Depreciation = "depreciation", Rounding = "rounding";

    /// <summary>Key prefix of the chart's accounts that no posting rule uses (a person may delete them)</summary>
    public const string Optional = "coa:";

    private record Def(string Key, string Code, string Name, string Root, string? Parent, bool Group = false);
    private const string A = LedgerRootType.Asset, L = LedgerRootType.Liability, E = LedgerRootType.Equity, I = LedgerRootType.Income, X = LedgerRootType.Expense;

    /// <summary>Parents before children</summary>
    private static readonly Def[] Defaults =
    {
        new(Assets, "100000", "Assets", A, null, true),
        new("g:110000", "110000", "Asset", A, Assets, true),
        new(FixedAssets, "111001", "Property & equipment", A, "g:110000"),
        new("g:112000", "112000", "Accumulated Dep", A, Assets, true),
        new(AccDepreciation, "112001", "Accumulated depreciation", A, "g:112000"),
        new("coa:120000", "120000", "Intangible assets", A, Assets, true),
        new("coa:121001", "121001", "Intangible assets", A, "coa:120000"),
        new("coa:122000", "122000", "Acc Dep - Intangible assets", A, Assets, true),
        new("coa:122001", "122001", "Acc Dep-Intangible assets", A, "coa:122000"),
        new(CashBank, "151000", "Cash & Cash Equivalent (CCE)", A, Assets, true),
        new(Cash, "151001", "Cash in Hand", A, CashBank),
        new(Bank, "151002", "Cash at Bank", A, CashBank),
        new("coa:151100", "151100", "Deposit Accounts", A, Assets, true),
        new("coa:151101", "151101", "Fixed Deposits", A, "coa:151100"),
        new("g:152000", "152000", "Inventory (Inv)", A, Assets, true),
        new(Stock, "152001", "Inventory", A, "g:152000"),
        new("g:153000", "153000", "Trade Receivables (TR)", A, Assets, true),
        new(Receivable, "153001", "TR-Receivables", A, "g:153000"),
        new("coa:154000", "154000", "Advances (ADV)", A, Assets, true),
        new("coa:154001", "154001", "ADV-Advances to vendors", A, "coa:154000"),
        new("coa:156000", "156000", "Prepayments (PREP)", A, Assets, true),
        new("coa:156001", "156001", "PREP- Prepayments Rent", A, "coa:156000"),
        new("coa:156002", "156002", "PREP- Prepayments IT", A, "coa:156000"),
        new("g:157000", "157000", "Other receivables (OTH REC)", A, Assets, true),
        new(SupplierReturns, "157001", "OTH REC", A, "g:157000"),
        new(InputTax, "157002", "GST-Tax", A, "g:157000"),

        new(Liabilities, "300000", "Liabilities", L, null, true),
        new("g:311000", "311000", "Loans", L, Liabilities, true),
        new(Loans, "311001", "Short Term Loans", L, "g:311000"),
        new("g:312000", "312000", "Trade payable (TP)", L, Liabilities, true),
        new(Payable, "312001", "Trade payables", L, "g:312000"),
        new("coa:313000", "313000", "Advances (ADV (L))", L, Liabilities, true),
        new("coa:313001", "313001", "ADV from Customers", L, "coa:313000"),
        new("coa:313002", "313002", "(ADV (L))Customers-Sales", L, "coa:313000"),
        new("coa:315000", "315000", "Accurals (ACC)", L, Liabilities, true),
        new("coa:315001", "315001", "ACC-Payroll and employee benefits", L, "coa:315000"),
        new("coa:315003", "315003", "ACC-Bonus", L, "coa:315000"),
        new("g:316000", "316000", "Other payables (Oth Pay)", L, Liabilities, true),
        new(TaxPayable, "316001", "Sales tax payable", L, "g:316000"),
        new("coa:317000", "317000", "Unearned income (Unea Inc)", L, Liabilities),

        new(Equity, "330000", "Equity", E, null, true),
        new("g:331000", "331000", "Funding", E, Equity, true),
        new(OwnerCapital, "331001", "Share Capital", E, "g:331000"),
        new(OwnerStock, "331002", "Additional Capital Contribution", E, "g:331000"),
        new(Drawings, "331003", "Owner's drawings", E, "g:331000"),
        new(Retained, "332001", "Retained earnings", E, Equity),

        new(Income, "500000", "Income", I, null, true),
        new("g:510000", "510000", "Revenue", I, Income, true),
        new(Sales, "511001", "Operating Revenue", I, "g:510000"),
        new(ShippingIncome, "511002", "Shipping charged to customers", I, "g:510000"),
        new(InstallationIncome, "511003", "Installation charges", I, "g:510000"),
        new(SalesReturns, "511004", "Sales returns", I, "g:510000"),
        new(PurchaseDiscounts, "812001", "Miscellaneous Income", I, Income),

        new(Expenses, "600000", "Expenses", X, null, true),
        new("g:610000", "610000", "Cost of Sales (COS)", X, Expenses, true),
        new(Cogs, "611001", "Cost of goods sold", X, "g:610000"),
        new(Freight, "611002", "Shipping on purchases", X, "g:610000"),
        new(NonStockPurchases, "611003", "Purchases not kept in stock", X, "g:610000"),
        new("coa:621000", "621000", "Employee Salaries & Benefits (ESB)", X, Expenses),
        new("coa:631000", "631000", "Rent (RT)", X, Expenses),
        new("coa:632000", "632000", "Office Expenses (Off Exp)", X, Expenses),
        new("coa:633000", "633000", "Maintenance (Mant)", X, Expenses),
        new("coa:635000", "635000", "IT Cost (IT)", X, Expenses),
        new("coa:641000", "641000", "Dues & Fees", X, Expenses),
        new("coa:651100", "651100", "G & A Travel & Hospitality (T&H)", X, Expenses),
        new("coa:651200", "651200", "Hospitality & Entertainment Expenses", X, Expenses),
        new(ExpenseCategories, "661000", "General Administration", X, Expenses, true),
        new(Depreciation, "691000", "Depreciation", X, Expenses),
        new("coa:711000", "711000", "Marketing & Other G&A", X, Expenses),
        new(Rounding, "751000", "Other Expences (OE)", X, Expenses),
        new("coa:761000", "761000", "Finance Charges (FC)", X, Expenses),
    };

    /// <summary>Payment methods that are cash (the rest go to Cash at Bank)</summary>
    public static bool IsCash(string? method) =>
        string.IsNullOrWhiteSpace(method) || method.Trim().Equals("Cash", StringComparison.OrdinalIgnoreCase)
        || method.Trim().Equals("Not recorded", StringComparison.OrdinalIgnoreCase);

    private readonly AppDbContext _db;
    private readonly int _tenantId;
    private readonly Dictionary<string, LedgerAccount> _byKey = new();
    public LedgerChart(AppDbContext db, int tenantId) { _db = db; _tenantId = tenantId; }

    /// <summary>Loads the chart: a company without accounts gets the whole chart; otherwise the posting rules'
    /// accounts that are missing are created</summary>
    public async Task LoadAsync(CancellationToken ct)
    {
        if (!await _db.LedgerAccounts.AnyAsync(ct)) { await ApplyDefaultsAsync(ct); return; }
        _byKey.Clear();
        foreach (var a in await _db.LedgerAccounts.Where(a => a.SystemKey != null).ToListAsync(ct)) _byKey[a.SystemKey!] = a;
        var codes = (await _db.LedgerAccounts.Select(a => a.Code).ToListAsync(ct)).ToHashSet();
        foreach (var d in Defaults.Where(d => !d.Key.StartsWith(Optional) && !_byKey.ContainsKey(d.Key)))
        {
            var account = new LedgerAccount
            {
                TenantId = _tenantId, Code = FreeCode(d.Code, codes), Name = d.Name, RootType = d.Root, IsGroup = d.Group, SystemKey = d.Key,
                ParentId = d.Parent != null && _byKey.TryGetValue(d.Parent, out var parent) ? parent.Id : null,
            };
            _db.LedgerAccounts.Add(account);
            await _db.SaveLedgerAsync(ct);     // one by one: the children need the parent's id
            _byKey[d.Key] = account; codes.Add(account.Code);
        }
    }

    /// <summary>
    /// The whole chart, as defined above: missing accounts are created, the existing ones get their code, name and
    /// group. Also moves an older chart over (bank accounts per payment method become Cash at Bank; expense category
    /// accounts are numbered under 661000). Run for a new company and once when the chart changes (no entries then).
    /// </summary>
    public async Task ApplyDefaultsAsync(CancellationToken ct)
    {
        var all = await _db.LedgerAccounts.ToListAsync(ct);
        _byKey.Clear();
        foreach (var a in all.Where(a => a.SystemKey != null)) _byKey[a.SystemKey!] = a;
        // the old per-method bank accounts (their entries are posted again to Cash at Bank)
        var oldBanks = all.Where(a => a.SystemKey != null && a.SystemKey.StartsWith("bank:")).ToList();
        if (oldBanks.Count > 0 && !await _db.JournalLines.AnyAsync(l => oldBanks.Select(b => b.Id).Contains(l.AccountId), ct))
        {
            _db.LedgerAccounts.RemoveRange(oldBanks);
            foreach (var b in oldBanks) { all.Remove(b); _byKey.Remove(b.SystemKey!); }
            await _db.SaveLedgerAsync(ct);
        }
        var defined = Defaults.Select(d => d.Key).ToHashSet();
        foreach (var d in Defaults)
        {
            var codes = all.Where(a => a.SystemKey != d.Key).Select(a => a.Code).ToHashSet();
            if (!_byKey.TryGetValue(d.Key, out var account))
            {
                account = new LedgerAccount { TenantId = _tenantId, SystemKey = d.Key, Code = FreeCode(d.Code, codes) };
                _db.LedgerAccounts.Add(account);
                all.Add(account);
                _byKey[d.Key] = account;
            }
            else if (!codes.Contains(d.Code)) account.Code = d.Code;
            account.Name = d.Name; account.RootType = d.Root; account.IsGroup = d.Group;
            account.ParentId = d.Parent != null ? _byKey[d.Parent].Id : null;
            await _db.SaveLedgerAsync(ct);
        }
        // expense category accounts: numbered under 661000, in the order they were made
        var group = _byKey[ExpenseCategories];
        var n = int.Parse(group.Code);
        foreach (var a in all.Where(a => a.SystemKey != null && a.SystemKey.StartsWith("exp-cat:")).OrderBy(a => a.Id))
        {
            var codes = all.Where(x => x.Id != a.Id).Select(x => x.Code).ToHashSet();
            do n++; while (codes.Contains(n.ToString()));
            a.Code = n.ToString(); a.ParentId = group.Id; a.RootType = LedgerRootType.Expense;
        }
        // groups of the old chart that are no longer defined and are now empty
        foreach (var g in all.Where(a => a.IsGroup && a.SystemKey != null && !defined.Contains(a.SystemKey) && !all.Any(c => c.ParentId == a.Id)).ToList())
            _db.LedgerAccounts.Remove(g);
        await _db.SaveLedgerAsync(ct);
    }

    private static string FreeCode(string code, HashSet<string> taken)
    {
        while (taken.Contains(code)) code = (long.Parse(code) + 1).ToString();   // a person used the code for their own account
        return code;
    }

    public int Id(string key) => _byKey[key].Id;

    /// <summary>The money account of a payment method: Cash in Hand, or Cash at Bank for every other method</summary>
    public Task<int> MoneyAsync(string? method, CancellationToken ct) => Task.FromResult(Id(IsCash(method) ? Cash : Bank));

    /// <summary>The expense account of an expense category (created when first used, renamed with the category)</summary>
    public async Task<int> ExpenseCategoryAsync(int categoryId, string? name, CancellationToken ct)
    {
        var key = $"exp-cat:{categoryId}";
        var title = string.IsNullOrWhiteSpace(name) ? $"Expense category {categoryId}" : name.Trim();
        if (title.Length > 150) title = title[..150];
        if (!_byKey.TryGetValue(key, out var account))
        {
            account = await _db.LedgerAccounts.FirstOrDefaultAsync(a => a.SystemKey == key, ct);
            if (account == null)
            {
                var parent = _byKey[ExpenseCategories];
                var codes = (await _db.LedgerAccounts.Select(a => a.Code).ToListAsync(ct)).ToHashSet();
                account = new LedgerAccount { TenantId = _tenantId, Code = FreeCode((long.Parse(parent.Code) + 1).ToString(), codes), Name = title,
                    RootType = parent.RootType, ParentId = parent.Id, SystemKey = key };
                _db.LedgerAccounts.Add(account);
                await _db.SaveLedgerAsync(ct);
            }
            _byKey[key] = account;
        }
        if (account.Name != title) account.Name = title;
        return account.Id;
    }
}
