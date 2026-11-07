using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;
using ReactPosApi.Services;
using Xunit;

namespace ReactPosApi.UnitTests.CATL;

/// <summary>Categorization and package-items files (Services/CATL/CatalogImportService.cs), case by case</summary>
public class CatalogImportTests
{
    private static List<List<string?>> Rows(params string?[][] rows) => rows.Select(r => r.ToList()).ToList();
    private static CatalogImportService Import(TestDb t) => new(t.Db, new CatalogService(t.Db));

    private static readonly List<List<string?>> Categorization = Rows(
        new[] { "Product Name", "Standard Solution", "Premium Solution" },
        new[] { "Lighting Control", null, null },
        new[] { "Smart Switches", "✓", "✓" },
        new[] { "Safety", null, null },
        new[] { "Gas Sensors", "x", null });

    private static async Task<int> Inventory(TestDb t, string name, string? color = null)
    {
        t.Db.Products.Add(new Product { ProductName = color == null ? name : $"{name} {color}", Color = color, Price = 100, ProductType = "single" });
        await t.Db.SaveChangesAsync();
        await new CatalogService(t.Db).SyncSkusAsync();
        return t.Db.CatalogProducts.Single(p => p.Name == name).Id;
    }

    [Fact]
    public async Task Categorization_of_a_new_company_creates_categories_families_and_their_plans()
    {
        using var t = new TestDb();
        var report = await Import(t).ImportCategorizationAsync("cat.xlsx", Categorization);
        Assert.Equal(2, (int)report["categories"]!);
        Assert.Equal(2, (int)report["familiesCreated"]!);
        var tiers = t.Db.CatalogTiers.ToDictionary(x => x.Id, x => x.Code);
        var switches = t.Db.SolutionProductLines.Single(f => f.Name == "Smart Switches");
        var sensors = t.Db.SolutionProductLines.Single(f => f.Name == "Gas Sensors");
        Assert.Equal(new[] { "premium", "standard" }, t.Db.CatalogFamilyTiers.Where(x => x.FamilyId == switches.Id).AsEnumerable().Select(x => tiers[x.TierId]).OrderBy(x => x));
        Assert.Equal(new[] { "standard" }, t.Db.CatalogFamilyTiers.Where(x => x.FamilyId == sensors.Id).AsEnumerable().Select(x => tiers[x.TierId]));
    }

    [Fact]
    public async Task Categorization_of_an_existing_company_holds_unknown_families_for_mapping()
    {
        using var t = new TestDb();
        await Import(t).ImportCategorizationAsync("cat.xlsx", Categorization);
        var report = await Import(t).ImportCategorizationAsync("cat.xlsx", Rows(
            new[] { "Product Name", "Standard Solution", "Premium Solution" }, new[] { "Safety", null, null }, new[] { "Smoke Sensors", "✓", null }));
        Assert.Equal(1, (int)report["unmatched"]!);
        Assert.Single(t.Db.CatalogUnmatched.Where(u => u.Name == "Smoke Sensors"));
    }

    [Fact]
    public async Task Categorization_without_its_headers_is_refused()
    {
        using var t = new TestDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Import(t).ImportCategorizationAsync("x.xlsx", Rows(new[] { "Name", "Std" }, new[] { "A", "x" })));
    }

    [Fact]
    public async Task Package_items_put_quantities_in_every_plan_the_family_is_in_and_ignore_prices()
    {
        using var t = new TestDb();
        await Import(t).ImportCategorizationAsync("cat.xlsx", Categorization);
        var sw = await Inventory(t, "Smart Switch", "White");
        var gs = await Inventory(t, "Gas Sensor");
        var fam = t.Db.SolutionProductLines.ToDictionary(f => f.Name, f => f.Id);
        foreach (var (id, f) in new[] { (sw, "Smart Switches"), (gs, "Gas Sensors") }) t.Db.CatalogProducts.Single(p => p.Id == id).FamilyId = fam[f];
        await t.Db.SaveChangesAsync();

        var report = await Import(t).ImportPackagesAsync("items.xlsx", Rows(
            new[] { "Product ", "Unit Price ", "5 Marla\nAutomation", "Price", "1 Kanal" },
            new[] { "Smart Switch", "99999", "6", "1", "12.5" },
            new[] { "Gas Sensor", "1", "1", "1", "2" }));
        Assert.Equal(new[] { "5 Marla", "1 Kanal" }, t.Db.PackageSizes.OrderBy(s => s.SortOrder).Select(s => s.Name));   // price columns skipped
        var tiers = t.Db.CatalogTiers.ToDictionary(x => x.Code, x => x.Id);
        var kanal = t.Db.PackageSizes.Single(s => s.Name == "1 Kanal").Id;
        decimal? Q(int p, string tier) => t.Db.CatalogPackageLines.SingleOrDefault(l => l.CatalogProductId == p && l.PackageSizeId == kanal && l.TierId == tiers[tier])?.Quantity;
        Assert.Equal(12.5m, Q(sw, "standard"));
        Assert.Equal(12.5m, Q(sw, "premium"));
        Assert.Equal(2m, Q(gs, "standard"));
        Assert.Null(Q(gs, "premium"));
        Assert.Null(t.Db.CatalogProducts.Single(p => p.Id == gs).FallbackPrice);       // the file's price is not stored
        Assert.Equal(0, (int)report["unmatched"]!);
    }

    [Fact]
    public async Task Package_item_found_without_a_family_waits_with_its_product_chosen()
    {
        using var t = new TestDb();
        var p = await Inventory(t, "Gas Sensor");
        await Import(t).ImportPackagesAsync("items.xlsx", Rows(new[] { "Product", "5 Marla" }, new[] { "Gas Sensor", "1" }));
        var held = t.Db.CatalogUnmatched.Single();
        Assert.Contains($"\"productId\":{p}", held.RowJson);
        Assert.Empty(t.Db.CatalogPackageLines);
    }

    [Fact]
    public async Task Package_item_not_in_the_inventory_waits_and_can_be_kept_as_not_in_the_inventory()
    {
        using var t = new TestDb();
        await Import(t).ImportCategorizationAsync("cat.xlsx", Categorization);
        await Import(t).ImportPackagesAsync("items.xlsx", Rows(new[] { "Product", "5 Marla" }, new[] { "IR Blaster", "3" }));
        var held = t.Db.CatalogUnmatched.Single();
        var family = t.Db.SolutionProductLines.Single(f => f.Name == "Gas Sensors").Id;
        await Import(t).MapAsync(held.Id, new CatalogImportService.MapDto(null, null, true, null, family));
        var product = t.Db.CatalogProducts.Single(p => p.Name == "IR Blaster");
        Assert.False(t.Db.Products.Any(s => s.CatalogProductId == product.Id));       // no inventory SKU
        Assert.Equal(3, t.Db.CatalogPackageLines.Single(l => l.CatalogProductId == product.Id).Quantity);
        Assert.Empty(t.Db.CatalogUnmatched);
    }

    [Fact]
    public async Task Mapping_a_name_to_a_product_saves_it_as_another_name_for_the_next_import()
    {
        using var t = new TestDb();
        await Import(t).ImportCategorizationAsync("cat.xlsx", Categorization);
        var p = await Inventory(t, "Gas Sensor");
        await Import(t).ImportPackagesAsync("items.xlsx", Rows(new[] { "Product", "5 Marla" }, new[] { "Gas Sensr", "1" }));
        var held = t.Db.CatalogUnmatched.Single();
        await Import(t).MapAsync(held.Id, new CatalogImportService.MapDto(p, null, false, null, t.Db.SolutionProductLines.Single(f => f.Name == "Gas Sensors").Id));
        Assert.Single(t.Db.CatalogAliases.Where(a => a.CatalogProductId == p && a.Alias == "Gas Sensr"));
        var report = await Import(t).ImportPackagesAsync("items.xlsx", Rows(new[] { "Product", "5 Marla" }, new[] { "Gas Sensr", "4" }));
        Assert.Equal(1, (int)report["matched"]!);
    }

    [Fact]
    public async Task The_file_is_the_whole_list_a_product_left_out_is_no_longer_suggested()
    {
        using var t = new TestDb();
        await Import(t).ImportCategorizationAsync("cat.xlsx", Categorization);
        var a = await Inventory(t, "Gas Sensor");
        var b = await Inventory(t, "Smart Switch");
        var fam = t.Db.SolutionProductLines.Single(f => f.Name == "Smart Switches").Id;
        foreach (var id in new[] { a, b }) t.Db.CatalogProducts.Single(p => p.Id == id).FamilyId = fam;
        await t.Db.SaveChangesAsync();
        await Import(t).ImportPackagesAsync("items.xlsx", Rows(new[] { "Product", "5 Marla" }, new[] { "Gas Sensor", "1" }, new[] { "Smart Switch", "2" }));
        var report = await Import(t).ImportPackagesAsync("items.xlsx", Rows(new[] { "Product", "5 Marla" }, new[] { "Smart Switch", "2" }));
        Assert.False(t.Db.CatalogPackageLines.Any(l => l.CatalogProductId == a));
        Assert.True(t.Db.CatalogPackageLines.Any(l => l.CatalogProductId == b));
        Assert.Contains("Gas Sensor", report["productsRemoved"]!.ToJsonString());
    }

    [Fact]
    public async Task Package_items_without_size_columns_are_refused()
    {
        using var t = new TestDb();
        t.Db.PackageSizes.Add(new PackageSize { Name = "5 Marla", SortOrder = 1 }); await t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Import(t).ImportPackagesAsync("x.xlsx", Rows(new[] { "Product", "Something" }, new[] { "A", "1" })));
    }

    [Fact]
    public async Task Import_profile_defaults_and_a_saved_profile()
    {
        using var t = new TestDb();
        var import = Import(t);
        Assert.Contains("product name", (await import.ProfileAsync(CatalogImportService.Categorization)).ToJsonString());
        var custom = System.Text.Json.Nodes.JsonNode.Parse("{\"headers\":{\"name\":[\"item\"]}}")!.AsObject();
        await import.SaveProfileAsync(CatalogImportService.Packages, custom);
        Assert.Contains("item", (await import.ProfileAsync(CatalogImportService.Packages)).ToJsonString());
        Assert.Equal(new[] { CatalogImportService.Categorization, CatalogImportService.Packages }, CatalogImportService.Kinds);
    }
}
