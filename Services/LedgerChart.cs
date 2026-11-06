using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// The company's chart of accounts. The posting rules find accounts by SystemKey; a missing one is created
/// (a new company gets the whole default chart on its first posting). People may rename accounts and add their own.
/// Money accounts are made per payment method (Cash, Bank Transfer, Online ...) and expense accounts per expense category.
/// </summary>
public class LedgerChart
{
    public const string Assets = "assets", CashBank = "cash-bank", Liabilities = "liabilities", Equity = "equity",
        Income = "income", Expenses = "expenses", ExpenseCategories = "expense-categories";
    public const string Cash = "cash", Receivable = "receivable", Stock = "stock", SupplierReturns = "supplier-returns",
        InputTax = "input-tax", FixedAssets = "fixed-assets", AccDepreciation = "acc-depreciation",
        Payable = "payable", TaxPayable = "tax-payable", Loans = "loans",
        OwnerCapital = "owner-capital", OwnerStock = "owner-stock", Drawings = "drawings", Retained = "retained",
        Sales = "sales", ShippingIncome = "shipping-income", InstallationIncome = "installation-income", SalesReturns = "sales-returns",
        PurchaseDiscounts = "purchase-discounts",
        Cogs = "cogs", Freight = "freight", NonStockPurchases = "non-stock-purchases", Depreciation = "depreciation", Rounding = "rounding";

    private record Def(string Key, string Code, string Name, string Root, string? Parent, bool Group = false);

    private static readonly Def[] Defaults =
    {
        new(Assets, "1000", "Assets", LedgerRootType.Asset, null, true),
        new(CashBank, "1100", "Cash & bank", LedgerRootType.Asset, Assets, true),
        new(Cash, "1110", "Cash in hand", LedgerRootType.Asset, CashBank),
        new(Receivable, "1200", "Customers owe you (receivable)", LedgerRootType.Asset, Assets),
        new(Stock, "1300", "Stock on hand", LedgerRootType.Asset, Assets),
        new(SupplierReturns, "1400", "Supplier returns to be refunded", LedgerRootType.Asset, Assets),
        new(InputTax, "1500", "Tax paid on purchases", LedgerRootType.Asset, Assets),
        new(FixedAssets, "1600", "Business equipment", LedgerRootType.Asset, Assets),
        new(AccDepreciation, "1610", "Accumulated depreciation", LedgerRootType.Asset, Assets),
        new(Liabilities, "2000", "Liabilities", LedgerRootType.Liability, null, true),
        new(Payable, "2100", "You owe suppliers (payable)", LedgerRootType.Liability, Liabilities),
        new(TaxPayable, "2200", "Sales tax collected", LedgerRootType.Liability, Liabilities),
        new(Loans, "2300", "Loans", LedgerRootType.Liability, Liabilities),
        new(Equity, "3000", "Equity", LedgerRootType.Equity, null, true),
        new(OwnerCapital, "3100", "Owner's capital", LedgerRootType.Equity, Equity),
        new(OwnerStock, "3200", "Stock brought in by the owner", LedgerRootType.Equity, Equity),
        new(Drawings, "3300", "Owner's drawings", LedgerRootType.Equity, Equity),
        new(Retained, "3900", "Retained profit", LedgerRootType.Equity, Equity),
        new(Income, "4000", "Income", LedgerRootType.Income, null, true),
        new(Sales, "4100", "Sales", LedgerRootType.Income, Income),
        new(ShippingIncome, "4200", "Shipping charged to customers", LedgerRootType.Income, Income),
        new(InstallationIncome, "4300", "Installation charges", LedgerRootType.Income, Income),
        new(SalesReturns, "4400", "Sales returns", LedgerRootType.Income, Income),
        new(PurchaseDiscounts, "4500", "Discounts from suppliers", LedgerRootType.Income, Income),
        new(Expenses, "5000", "Expenses", LedgerRootType.Expense, null, true),
        new(Cogs, "5100", "Cost of goods sold", LedgerRootType.Expense, Expenses),
        new(Freight, "5200", "Shipping on purchases", LedgerRootType.Expense, Expenses),
        new(NonStockPurchases, "5300", "Purchases not kept in stock", LedgerRootType.Expense, Expenses),
        new(Depreciation, "5400", "Depreciation", LedgerRootType.Expense, Expenses),
        new(ExpenseCategories, "5500", "Running expenses", LedgerRootType.Expense, Expenses, true),
        new(Rounding, "5900", "Rounding differences", LedgerRootType.Expense, Expenses),
    };

    /// <summary>Payment methods that are cash (the rest are bank / digital money accounts)</summary>
    public static bool IsCash(string? method) =>
        string.IsNullOrWhiteSpace(method) || method.Trim().Equals("Cash", StringComparison.OrdinalIgnoreCase)
        || method.Trim().Equals("Not recorded", StringComparison.OrdinalIgnoreCase);

    private readonly AppDbContext _db;
    private readonly int _tenantId;
    private readonly Dictionary<string, LedgerAccount> _byKey = new();
    public LedgerChart(AppDbContext db, int tenantId) { _db = db; _tenantId = tenantId; }

    /// <summary>Loads the chart; creates the default accounts that are missing</summary>
    public async Task LoadAsync(CancellationToken ct)
    {
        _byKey.Clear();
        foreach (var a in await _db.LedgerAccounts.Where(a => a.SystemKey != null).ToListAsync(ct)) _byKey[a.SystemKey!] = a;
        var codes = (await _db.LedgerAccounts.Select(a => a.Code).ToListAsync(ct)).ToHashSet();
        foreach (var d in Defaults.Where(d => !_byKey.ContainsKey(d.Key)))
        {
            var code = d.Code;
            while (codes.Contains(code)) code = (int.Parse(code) + 1).ToString();   // a person used the code for their own account
            var account = new LedgerAccount
            {
                TenantId = _tenantId, Code = code, Name = d.Name, RootType = d.Root, IsGroup = d.Group, SystemKey = d.Key,
                ParentId = d.Parent != null ? _byKey[d.Parent].Id : null,
            };
            _db.LedgerAccounts.Add(account);
            await _db.SaveLedgerAsync(ct);     // saved one by one: the children need the parent's id
            _byKey[d.Key] = account; codes.Add(code);
        }
    }

    public int Id(string key) => _byKey[key].Id;

    /// <summary>The money account of a payment method: cash, or one account per other method (created when first used)</summary>
    public async Task<int> MoneyAsync(string? method, CancellationToken ct)
    {
        if (IsCash(method)) return Id(Cash);
        var name = method!.Trim();
        return await ChildAsync("bank:" + name.ToLowerInvariant(), name, CashBank, ct);
    }

    /// <summary>The expense account of an expense category (created when first used, renamed with the category)</summary>
    public async Task<int> ExpenseCategoryAsync(int categoryId, string? name, CancellationToken ct)
    {
        var id = await ChildAsync($"exp-cat:{categoryId}", string.IsNullOrWhiteSpace(name) ? $"Expense category {categoryId}" : name.Trim(), ExpenseCategories, ct);
        var account = _byKey[$"exp-cat:{categoryId}"];
        if (!string.IsNullOrWhiteSpace(name) && account.Name != name.Trim()) account.Name = name.Trim().Length > 150 ? name.Trim()[..150] : name.Trim();
        return id;
    }

    private async Task<int> ChildAsync(string key, string name, string parentKey, CancellationToken ct)
    {
        if (_byKey.TryGetValue(key, out var found)) return found.Id;
        var existing = await _db.LedgerAccounts.FirstOrDefaultAsync(a => a.SystemKey == key, ct);
        if (existing == null)
        {
            var parent = _byKey[parentKey];
            var codes = (await _db.LedgerAccounts.Select(a => a.Code).ToListAsync(ct)).ToHashSet();
            var n = int.Parse(parent.Code) + 1;
            while (codes.Contains(n.ToString())) n++;
            existing = new LedgerAccount { TenantId = _tenantId, Code = n.ToString(), Name = name.Length > 150 ? name[..150] : name, RootType = parent.RootType, ParentId = parent.Id, SystemKey = key };
            _db.LedgerAccounts.Add(existing);
            await _db.SaveLedgerAsync(ct);
        }
        _byKey[key] = existing;
        return existing.Id;
    }
}
