using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Controllers;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;

namespace ReactPosApi.UnitTests.INVT;

/// <summary>Purchase prices of the inventory products (INVT_PPRC, Services/INVT/PurchasePriceService.cs)</summary>
public class PurchasePriceTests
{
    private static int Product(TestDb t, string name, decimal price)
    {
        var p = new Product { ProductName = name, Price = price, ProductType = "single" };
        t.Db.Products.Add(p); t.Db.SaveChanges();
        return p.Id;
    }

    [Theory]
    [InlineData(25000, 25000)]
    [InlineData(150, 150)]
    [InlineData(99.50, 99.50)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]            // never below 0
    public void Starting_price_is_the_sale_price_with_no_margin(decimal salePrice, decimal purchase) =>
        Assert.Equal(purchase, PurchasePriceService.StartingPrice(salePrice));

    [Fact]
    public async Task Every_product_gets_its_starting_purchase_price_once()
    {
        using var t = new TestDb();
        var a = Product(t, "Gas Sensor", 18684);
        var b = Product(t, "Cheap", 900);
        Assert.Equal(2, await PurchasePriceService.EnsureAllAsync(t.Db));
        Assert.Equal(18684, t.Db.ProductPurchasePrices.Single(x => x.ProductId == a).PurchasePrice);
        Assert.Equal(900, t.Db.ProductPurchasePrices.Single(x => x.ProductId == b).PurchasePrice);
        Assert.Equal(0, await PurchasePriceService.EnsureAllAsync(t.Db));     // nothing new the second time
    }

    [Fact]
    public async Task A_changed_purchase_price_is_kept_and_not_reset()
    {
        using var t = new TestDb();
        var a = Product(t, "Gas Sensor", 18684);
        await PurchasePriceService.SaveAsync(t.Db, new[] { new PurchasePriceService.Change(a, 12000) }, "Ali");
        await PurchasePriceService.EnsureAllAsync(t.Db);
        var row = t.Fresh().ProductPurchasePrices.Single(x => x.ProductId == a);
        Assert.Equal(12000, row.PurchasePrice);
        Assert.Equal("Ali", row.UpdatedBy);
    }

    [Fact]
    public async Task List_shows_end_user_price_purchase_price_and_margin_of_every_product()
    {
        using var t = new TestDb();
        Product(t, "Gas Sensor", 18684);
        Product(t, "Piano Switch", 17311);
        var rows = await PurchasePriceService.ListAsync(t.Db);
        Assert.Equal(new[] { "Gas Sensor", "Piano Switch" }, rows.Select(r => r.ProductName));
        Assert.All(rows, r => Assert.Equal(0, r.Margin));            // not entered yet: the sale price
        Assert.All(rows, r => Assert.False(r.IsSet));
    }

    [Fact]
    public async Task A_price_nobody_entered_follows_the_sale_price()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        await PurchasePriceService.EnsureAllAsync(t.Db);
        t.Db.Products.Single(p => p.Id == a).Price = 180;
        t.Db.SaveChanges();
        Assert.Equal(1, await PurchasePriceService.EnsureAllAsync(t.Db));
        Assert.Equal(180, t.Fresh().ProductPurchasePrices.Single(x => x.ProductId == a).PurchasePrice);
    }

    [Fact]
    public async Task An_entered_price_no_longer_follows_the_sale_price()
    {
        using var t = new TestDb();
        var a = Product(t, "Soap", 150);
        await PurchasePriceService.SaveAsync(t.Db, new[] { new PurchasePriceService.Change(a, 150) }, "Ali");   // same amount, but entered
        t.Db.Products.Single(p => p.Id == a).Price = 180;
        t.Db.SaveChanges();
        await PurchasePriceService.EnsureAllAsync(t.Db);
        var row = (await PurchasePriceService.ListAsync(t.Fresh())).Single();
        Assert.Equal(150, row.PurchasePrice);
        Assert.Equal(30, row.Margin);
        Assert.True(row.IsSet);
    }

    [Fact]
    public async Task A_price_below_0_is_refused()
    {
        using var t = new TestDb();
        var a = Product(t, "Gas Sensor", 18684);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PurchasePriceService.SaveAsync(t.Db, new[] { new PurchasePriceService.Change(a, -1) }, "x"));
    }

    [Fact]
    public async Task Another_company_s_product_is_refused()
    {
        using var t = new TestDb();
        var a = Product(t, "Gas Sensor", 18684);
        using var other = t.Create(t.TenantId + 1000);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => PurchasePriceService.SaveAsync(other, new[] { new PurchasePriceService.Change(a, 5) }, "x"));
    }

    [Fact]
    public async Task Purchase_prices_belong_to_their_company()
    {
        using var t = new TestDb();
        Product(t, "Gas Sensor", 18684);
        await PurchasePriceService.EnsureAllAsync(t.Db);
        using var other = t.Create(t.TenantId + 1000);
        Assert.Empty(other.ProductPurchasePrices.ToList());
        Assert.Empty(await PurchasePriceService.ListAsync(other));
    }
}
