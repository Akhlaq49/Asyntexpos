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
    [InlineData(25000, 15000)]
    [InlineData(10000, 0)]
    [InlineData(4000, 0)]          // never below 0
    [InlineData(0, 0)]
    [InlineData(10000.50, 0.50)]
    public void Starting_price_is_the_end_user_price_less_10000(decimal endUser, decimal purchase) =>
        Assert.Equal(purchase, PurchasePriceService.StartingPrice(endUser));

    [Fact]
    public async Task Every_product_gets_its_starting_purchase_price_once()
    {
        using var t = new TestDb();
        var a = Product(t, "Gas Sensor", 18684);
        var b = Product(t, "Cheap", 900);
        Assert.Equal(2, await PurchasePriceService.EnsureAllAsync(t.Db));
        Assert.Equal(8684, t.Db.ProductPurchasePrices.Single(x => x.ProductId == a).PurchasePrice);
        Assert.Equal(0, t.Db.ProductPurchasePrices.Single(x => x.ProductId == b).PurchasePrice);
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
        Assert.All(rows, r => Assert.Equal(10000, r.Margin));
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

    [Fact]
    public async Task Purchase_order_from_a_quotation_takes_the_purchase_price_as_its_cost()
    {
        using var t = new TestDb();
        var a = Product(t, "Gas Sensor", 18684);
        await PurchasePriceService.SaveAsync(t.Db, new[] { new PurchasePriceService.Change(a, 11000) }, "x");
        var sale = new Sale { Reference = "SL1", CustomerName = "Ali", Status = SaleStatus.Queued, SaleDate = DateTime.UtcNow };
        t.Db.Sales.Add(sale); t.Db.SaveChanges();
        var q = new Quotation { Reference = "QT1", CustomerName = "Ali", SaleId = sale.Id, GrandTotal = 37368,
            Items = new() { new QuotationItem { ProductId = a, ProductName = "Gas Sensor", Quantity = 2, PurchasePrice = 18684, UnitCost = 18684, TotalCost = 37368 } } };
        t.Db.Quotations.Add(q); t.Db.SaveChanges();
        var result = await new QuotationPurchasePlanController(t.Fresh()).Plan(q.Id);
        var json = JsonSerializer.SerializeToElement(((OkObjectResult)result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var line = json.GetProperty("lines").EnumerateArray().Single();
        Assert.Equal(11000, line.GetProperty("purchasePrice").GetDecimal());
        Assert.Equal(18684, line.GetProperty("price").GetDecimal());
        Assert.Equal(2, line.GetProperty("suggested").GetInt32());
    }
}
