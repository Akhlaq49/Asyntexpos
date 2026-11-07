using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>Documents for the ledger tests, saved through the normal save (so they post their entries)</summary>
public static class Seed
{
    public static readonly DateTime Day = new(2026, 3, 10);

    public static int Product(AppDbContext db, string name = "Switch", decimal price = 250)
    {
        var p = new Product { ProductName = name, SKU = name.ToUpperInvariant(), Price = price, Quantity = 0, ProductType = "single" };
        db.Products.Add(p);
        db.SaveChanges();
        return p.Id;
    }

    public static Purchase Purchase(AppDbContext db, int productId, decimal qty, decimal unitCost, DateTime date, string supplier = "Acme",
        decimal orderTax = 0, decimal shipping = 0, decimal discount = 0, decimal? total = null, string status = "Received", decimal nonStock = 0)
    {
        var items = new List<PurchaseItem> { new() { ProductId = productId, ProductName = "x", Quantity = qty, PurchasePrice = unitCost, UnitCost = unitCost, TotalCost = qty * unitCost } };
        if (nonStock > 0) items.Add(new PurchaseItem { ProductId = null, ProductName = "service", Quantity = 1, PurchasePrice = nonStock, UnitCost = nonStock, TotalCost = nonStock });
        var p = new Purchase
        {
            SupplierName = supplier, Reference = $"P-{Guid.NewGuid():N}"[..10], Date = date, Status = status,
            OrderTax = orderTax, Shipping = shipping, Discount = discount, Total = total ?? qty * unitCost + nonStock + orderTax + shipping - discount, Items = items,
        };
        db.Purchases.Add(p);
        db.SaveChanges();
        return p;
    }

    public static Sale Sale(AppDbContext db, int productId, int qty, decimal unitPrice, DateTime date, decimal orderTax = 0, decimal shipping = 0,
        decimal installation = 0, string status = "Completed", string customer = "Ali")
    {
        var s = new Sale
        {
            Reference = $"S-{Guid.NewGuid():N}"[..10], CustomerName = customer, SaleDate = date, Status = status,
            OrderTax = orderTax, Shipping = shipping, InstallationCharges = installation, GrandTotal = qty * unitPrice + orderTax + shipping + installation,
            Items = new List<SaleItem> { new() { ProductId = productId, ProductName = "x", Quantity = qty, PurchasePrice = unitPrice, UnitCost = unitPrice, TotalCost = qty * unitPrice } },
        };
        db.Sales.Add(s);
        db.SaveChanges();
        return s;
    }

    public static SalePayment SalePayment(AppDbContext db, int saleId, decimal amount, string method = "Cash", DateTime? date = null)
    {
        var p = new SalePayment { SaleId = saleId, Reference = "PAY", ReceivedAmount = amount, PayingAmount = amount, PaymentType = method, PaymentDate = date ?? Day };
        db.SalePayments.Add(p);
        db.SaveChanges();
        return p;
    }

    /// <summary>The journal entry of a document, its lines keyed by account system key (debit - credit)</summary>
    public static Dictionary<string, decimal> Entry(AppDbContext db, string voucherType, int voucherId)
    {
        var accounts = db.LedgerAccounts.AsNoTracking().ToDictionary(a => a.Id, a => a.SystemKey ?? a.Name);
        var e = db.JournalEntries.AsNoTracking().Include(x => x.Lines).SingleOrDefault(x => x.VoucherType == voucherType && x.VoucherId == voucherId);
        return e == null ? new() : e.Lines.GroupBy(l => accounts[l.AccountId]).ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));
    }

    /// <summary>Balance (debit - credit) of an account by its system key</summary>
    public static decimal Balance(AppDbContext db, string key)
    {
        var id = db.LedgerAccounts.AsNoTracking().Where(a => a.SystemKey == key).Select(a => a.Id).SingleOrDefault();
        return db.JournalLines.AsNoTracking().Where(l => l.AccountId == id).AsEnumerable().Sum(l => l.Debit - l.Credit);
    }

    public static void AssertAllBalanced(AppDbContext db)
    {
        foreach (var e in db.JournalEntries.AsNoTracking().Include(x => x.Lines).ToList())
            Xunit.Assert.True(e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit), $"{e.VoucherType} {e.VoucherId} does not balance");
    }
}
