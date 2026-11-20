using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;
using ReactPosApi.Services;
using ReactPosApi.UnitTests.ACCT;
using Xunit;

namespace ReactPosApi.UnitTests.INVT;

/// <summary>Inventory > Sale Prices: purchase price from the latest received purchase, sale price set by people,
/// profit = sale - purchase (INVT_PPRC, Services/INVT/PurchasePriceService.cs)</summary>
public class PurchasePriceTests
{
    private static int Product(TestDb t, string name, decimal price)
    {
        var p = new Product { ProductName = name, Price = price, ProductType = "single" };
        t.Db.Products.Add(p); t.Db.SaveChanges();
        return p.Id;
    }

    private static async Task<PurchasePriceService.Row> RowOf(TestDb t, int productId) =>
        (await PurchasePriceService.ListAsync(t.Fresh())).Single(r => r.ProductId == productId);

    [Theory]
    [InlineData(25000, 25000)]
    [InlineData(150, 150)]
    [InlineData(99.50, 99.50)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]            // never below 0
    public void A_product_not_bought_yet_costs_its_sale_price(decimal salePrice, decimal purchase) =>
        Assert.Equal(purchase, PurchasePriceService.StartingPrice(salePrice));

    [Fact]
    public async Task Every_product_gets_its_purchase_price_once()
    {
        using var t = new TestDb();
        var a = Product(t, "Rice 5kg", 1800);
        var b = Product(t, "Soap", 150);
        Assert.Equal(2, await PurchasePriceService.EnsureAllAsync(t.Db));
        Assert.Equal(1800, t.Db.ProductPurchasePrices.Single(x => x.ProductId == a).PurchasePrice);
        Assert.Equal(150, t.Db.ProductPurchasePrices.Single(x => x.ProductId == b).PurchasePrice);
        Assert.Equal(0, await PurchasePriceService.EnsureAllAsync(t.Db));     // nothing new the second time
    }

    [Fact]
    public async Task Not_bought_yet_purchase_price_is_the_sale_price_and_profit_is_0()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        var row = await RowOf(t, a);
        Assert.Equal(150, row.SalePrice);
        Assert.Equal(150, row.PurchasePrice);
        Assert.Equal(0, row.Profit);
        Assert.Null(row.PurchasedFrom);
        Assert.False(row.IsSet);
    }

    [Fact]
    public async Task Purchase_price_is_the_unit_cost_of_the_latest_received_purchase()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        Seed.Purchase(t.Db, a, 10, 100, Seed.Day.AddDays(-5));
        var latest = Seed.Purchase(t.Db, a, 10, 110, Seed.Day);
        Seed.Purchase(t.Db, a, 10, 90, Seed.Day.AddDays(1), status: "Pending");     // not received: doesn't count
        var row = await RowOf(t, a);
        Assert.Equal(110, row.PurchasePrice);
        Assert.Equal(40, row.Profit);                  // 150 - 110
        Assert.Equal(26.7m, row.ProfitPercent);        // 40 / 150
        Assert.Equal(latest.Reference, row.PurchasedFrom);
        Assert.True(row.IsSet);
    }

    [Fact]
    public async Task A_deleted_purchase_no_longer_sets_the_purchase_price()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        Seed.Purchase(t.Db, a, 10, 100, Seed.Day.AddDays(-5));
        var latest = Seed.Purchase(t.Db, a, 10, 110, Seed.Day);
        await PurchasePriceService.EnsureAllAsync(t.Db);
        var db = t.Fresh();
        db.Purchases.Remove(db.Purchases.Include(p => p.Items).Single(p => p.Id == latest.Id));
        db.SaveChanges();
        Assert.Equal(100, (await RowOf(t, a)).PurchasePrice);
    }

    [Fact]
    public async Task Saving_a_sale_price_changes_the_product_price_and_the_profit()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        Seed.Purchase(t.Db, a, 10, 100, Seed.Day);
        await PurchasePriceService.SaveSalePricesAsync(t.Fresh(), new[] { new PurchasePriceService.SaleChange(a, 135) });
        Assert.Equal(135, t.Fresh().Products.Single(p => p.Id == a).Price);
        var row = await RowOf(t, a);
        Assert.Equal(100, row.PurchasePrice);          // still the purchase
        Assert.Equal(35, row.Profit);
    }

    [Fact]
    public async Task A_new_sale_price_moves_the_cost_of_a_product_not_bought_yet()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        await PurchasePriceService.SaveSalePricesAsync(t.Fresh(), new[] { new PurchasePriceService.SaleChange(a, 180) });
        var row = await RowOf(t, a);
        Assert.Equal(180, row.PurchasePrice);
        Assert.Equal(0, row.Profit);
    }

    [Fact]
    public async Task A_cost_typed_for_a_new_product_is_kept_until_it_is_bought()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        await PurchasePriceService.SaveAsync(t.Db, new[] { new PurchasePriceService.Change(a, 120) }, "Ali");
        Assert.Equal(120, (await RowOf(t, a)).PurchasePrice);
        Seed.Purchase(t.Fresh(), a, 5, 115, Seed.Day);
        Assert.Equal(115, (await RowOf(t, a)).PurchasePrice);      // the purchase decides once it is bought
    }

    [Fact]
    public async Task A_price_below_0_is_refused()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PurchasePriceService.SaveSalePricesAsync(t.Db, new[] { new PurchasePriceService.SaleChange(a, -1) }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => PurchasePriceService.SaveAsync(t.Db, new[] { new PurchasePriceService.Change(a, -1) }, "x"));
    }

    [Fact]
    public async Task Another_company_s_product_is_refused()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        using var other = t.Create(t.TenantId + 1000);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => PurchasePriceService.SaveSalePricesAsync(other, new[] { new PurchasePriceService.SaleChange(a, 5) }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => PurchasePriceService.SaveAsync(other, new[] { new PurchasePriceService.Change(a, 5) }, "x"));
    }

    [Fact]
    public async Task Prices_belong_to_their_company()
    {
        using var t = new TestDb();
        Product(t, "Soap", 150);
        await PurchasePriceService.EnsureAllAsync(t.Db);
        using var other = t.Create(t.TenantId + 1000);
        Assert.Empty(other.ProductPurchasePrices.ToList());
        Assert.Empty(await PurchasePriceService.ListAsync(other));
    }
}
