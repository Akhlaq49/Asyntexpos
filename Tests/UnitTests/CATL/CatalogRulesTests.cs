using ReactPosApi.Controllers;
using ReactPosApi.Services;
using Xunit;

namespace ReactPosApi.UnitTests.CATL;

/// <summary>Product catalogue helpers (Services/CATL/CatalogService.cs, Controllers/CATL/SolutionPackagesController.cs)</summary>
public class CatalogRulesTests
{
    [Theory]
    [InlineData("  Piano  Switch ", "piano switch")]
    [InlineData(null, "")]
    [InlineData("A\tB\nC", "a b c")]
    public void Key_ignores_case_and_spacing(string? name, string key) => Assert.Equal(key, CatalogService.Key(name));

    [Theory]
    [InlineData("Innswitch Piano 1 Gang White", "White", "Innswitch Piano 1 Gang")]
    [InlineData("Innswitch Piano 1 Gang white", "White", "Innswitch Piano 1 Gang")]
    [InlineData("Gas Sensor", "", "Gas Sensor")]
    [InlineData("Gas Sensor", null, "Gas Sensor")]
    [InlineData("White Board", "White", "White Board")]        // the colour only counts at the end
    public void Product_name_is_the_sku_name_without_its_colour(string sku, string? color, string product) =>
        Assert.Equal(product, CatalogService.ProductNameOf(sku, color));

    [Theory]
    [InlineData(true, true, "both")]
    [InlineData(true, false, "standard")]
    [InlineData(false, true, "premium")]
    [InlineData(false, false, "none")]
    public void Usage_from_the_plans_ticked(bool standard, bool premium, string usage) =>
        Assert.Equal(usage, CatalogService.UsageOf(standard, premium));

    private sealed class Row { public int Id; public int Order; }

    [Theory]
    [InlineData(2, -1, new[] { 2, 1, 3 })]
    [InlineData(2, 1, new[] { 1, 3, 2 })]
    [InlineData(1, -1, new[] { 1, 2, 3 })]     // already first
    [InlineData(3, 1, new[] { 1, 2, 3 })]      // already last
    [InlineData(9, 1, new[] { 1, 2, 3 })]      // not in the list
    public void Move_swaps_with_the_neighbour_and_renumbers(int id, int dir, int[] expected)
    {
        var list = new List<Row> { new() { Id = 1, Order = 5 }, new() { Id = 2, Order = 5 }, new() { Id = 3, Order = 9 } };
        SolutionPackagesController.Swap(list, id, dir, r => r.Id, (r, o) => r.Order = o);
        Assert.Equal(expected, list.Select(r => r.Id));
        if (expected.SequenceEqual(new[] { 1, 2, 3 }) && id != 2) return;
        Assert.Equal(new[] { 1, 2, 3 }, list.Select(r => r.Order));
    }
}
