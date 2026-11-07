using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;
using static ReactPosApi.UnitTests.ACCT.Seed;

namespace ReactPosApi.UnitTests.ACCT;

/// <summary>Moving average cost (Services/ACCT/LedgerPoster.cs, StockCost), case by case</summary>
public class StockCostTests
{
    private static async Task<StockCost> Cost(TestDb t) => await StockCost.LoadAsync(t.Fresh(), t.TenantId, null, default);
    private static int LineId(TestDb t, Sale s) => t.Fresh().SaleItems.Single(i => i.SaleId == s.Id).Id;

    [Fact]
    public async Task Sale_takes_the_average_of_what_came_in_before_it()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 10, 100, Day.AddDays(-3));
        Purchase(t.Db, pid, 10, 200, Day.AddDays(-2));
        var s = Sale(t.Db, pid, 5, 400, Day);
        Assert.Equal(750, (await Cost(t)).SaleLine(LineId(t, s)));   // 5 x 150
    }

    [Fact]
    public async Task Stock_bought_after_the_sale_does_not_change_its_cost()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 10, 100, Day.AddDays(-3));
        var s = Sale(t.Db, pid, 5, 400, Day);
        Purchase(t.Db, pid, 10, 999, Day.AddDays(2));
        Assert.Equal(500, (await Cost(t)).SaleLine(LineId(t, s)));
    }

    [Fact]
    public async Task Sales_return_comes_back_at_the_average_cost()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 4, 100, Day.AddDays(-3));
        Sale(t.Db, pid, 2, 400, Day);
        var r = new SalesReturn { Reference = "R", CustomerName = "Ali", ReturnDate = Day.AddDays(1), GrandTotal = 400,
            Items = new() { new SalesReturnItem { ProductId = pid, Quantity = 1, Subtotal = 400 } } };
        t.Db.SalesReturns.Add(r); t.Db.SaveChanges();
        var itemId = t.Fresh().SalesReturnItems.Single().Id;
        Assert.Equal(100, (await Cost(t)).ReturnLine(itemId));
    }

    [Fact]
    public async Task Selling_more_than_in_stock_uses_the_last_known_average()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 2, 100, Day.AddDays(-3));
        Sale(t.Db, pid, 2, 400, Day);
        var s = Sale(t.Db, pid, 3, 400, Day.AddDays(1));     // nothing left
        Assert.Equal(300, (await Cost(t)).SaleLine(LineId(t, s)));
    }

    [Fact]
    public async Task A_product_never_bought_costs_nothing()
    {
        using var t = new TestDb();
        var s = Sale(t.Db, Product(t.Db), 3, 400, Day);
        Assert.Equal(0, (await Cost(t)).SaleLine(LineId(t, s)));
    }

    [Fact]
    public async Task Purchase_return_leaves_at_its_own_value_and_changes_the_average()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        var p = Purchase(t.Db, pid, 10, 100, Day.AddDays(-3));
        var r = new PurchaseReturn { Reference = "PR", PurchaseId = p.Id, SupplierName = "Acme", ReturnDate = Day.AddDays(-2), Total = 500,
            Items = new() { new PurchaseReturnItem { PurchaseItemId = p.Items!.First().Id, ProductId = pid, Quantity = 5, UnitCost = 100, Total = 500 } } };
        t.Db.PurchaseReturns.Add(r); t.Db.SaveChanges();
        var s = Sale(t.Db, pid, 5, 400, Day);
        Assert.Equal(500, (await Cost(t)).SaleLine(LineId(t, s)));
    }

    [Fact]
    public async Task Stock_entry_comes_in_at_its_purchase_value()
    {
        using var t = new TestDb();
        var pid = Product(t.Db, price: 80);
        var e = new StockEntry { ProductId = pid, Quantity = 10, Date = Day.AddDays(-1) };
        t.Db.StockEntries.Add(e); t.Db.SaveChanges();
        t.Db.Purchases.Add(new Purchase { SupplierName = "Stock Entry", Reference = $"STOCK-{e.Id}", Date = e.Date, Status = "Received", Total = 600,
            Items = new List<PurchaseItem> { new PurchaseItem { ProductName = "x", Quantity = 10, TotalCost = 600 } } });
        t.Db.SaveChanges();
        var s = Sale(t.Db, pid, 2, 400, Day);
        Assert.Equal(120, (await Cost(t)).SaleLine(LineId(t, s)));   // 600 / 10 = 60 each
    }

    [Fact]
    public async Task Stock_entry_without_its_purchase_is_valued_at_the_product_price()
    {
        using var t = new TestDb();
        var pid = Product(t.Db, price: 80);
        t.Db.StockEntries.Add(new StockEntry { ProductId = pid, Quantity = 10, Date = Day.AddDays(-1) }); t.Db.SaveChanges();
        var s = Sale(t.Db, pid, 2, 400, Day);
        Assert.Equal(160, (await Cost(t)).SaleLine(LineId(t, s)));
    }

    [Fact]
    public async Task Queued_sales_do_not_take_stock()
    {
        using var t = new TestDb();
        var pid = Product(t.Db);
        Purchase(t.Db, pid, 2, 100, Day.AddDays(-3));
        Sale(t.Db, pid, 2, 400, Day, status: SaleStatus.Queued);
        Purchase(t.Db, pid, 2, 200, Day.AddDays(1));
        var s = Sale(t.Db, pid, 4, 400, Day.AddDays(2));
        Assert.Equal(600, (await Cost(t)).SaleLine(LineId(t, s)));   // the queued sale left all 4 in stock: 4 x 150
    }

    [Fact]
    public async Task Only_the_requested_products_are_costed()
    {
        using var t = new TestDb();
        var a = Product(t.Db, "A"); var b = Product(t.Db, "B");
        Purchase(t.Db, a, 1, 100, Day.AddDays(-1)); Purchase(t.Db, b, 1, 100, Day.AddDays(-1));
        var sa = Sale(t.Db, a, 1, 400, Day); var sb = Sale(t.Db, b, 1, 400, Day);
        var cost = await StockCost.LoadAsync(t.Fresh(), t.TenantId, new HashSet<int> { a }, default);
        Assert.Equal(100, cost.SaleLine(LineId(t, sa)));
        Assert.Equal(0, cost.SaleLine(LineId(t, sb)));
    }
}
