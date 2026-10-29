using Microsoft.EntityFrameworkCore;
using ReactPosApi.Models;

namespace ReactPosApi.Data;

/// <summary>
/// The starting solution data (from "Smartinn Product categorization.xlsx" and "packages.xlsx"),
/// given to every company once - after that it is edited under Inventory > Solution Packages.
/// </summary>
public static class SolutionPackageDefaults
{
    public static readonly string[] Sizes = { "1 BHK", "2 BHK", "5 Marla", "10 Marla", "1 Kanal", "2 Kanal", "5 Kanal", "10 Kanal" };

    // group -> (product line, standard, premium)
    private static readonly (string Group, (string Name, bool Standard, bool Premium)[] Lines)[] Categorization =
    {
        ("Touch Panel", new[] { ("Hpad X Pro", false, true), ("Hpad Echo", true, true), ("Hpad Q2", true, true), ("Mini Gateway", true, false), ("Matter Gateway", false, true) }),
        ("Lighting Control", new[] { ("Piano Series", true, false), ("Matrix Series", false, true), ("Fusion Series", false, true) }),
        ("Smart Access", new[] { ("Door Lock i5", true, false), ("Door Lock i9 Pro", false, true) }),
        ("Shading Control", new[] { ("Curtain Track", true, true), ("Curtain Motor 1.5Nm, 6Nm", true, true), ("Roller Blind Motor 1.2Nm, 6Nm", true, true) }),
        ("Entertainment & Climate Control", new[] { ("Zigbee IR Blaster (for AC and TV control)", true, true), ("Temp. and Humidity Sensor", false, true) }),
        ("Smart Lighting Control", new[] { ("Smart Lights 7w", true, false), ("Smart Lights 10w", false, true) }),
        ("Smart Sensors", new[] { ("Door Sensor", true, false), ("Motion Sensor", true, false), ("Smart Gas Sensor", true, true), ("Smart Smoke Sensor", false, true), ("Smart Panic Button", false, true), ("Human Presence Sensor", false, true) }),
        ("Smart Intercom", new[] { ("Outdoor Station", false, true) }),
    };

    // item, its categorization line (null = not in the categorization sheet), quantity per size in the order of Sizes
    private static readonly (string Name, string? Line, int[] Qty)[] Items =
    {
        ("Smart Panel Hpad X Pro", "Hpad X Pro", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Smart Panel Hpad X", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Smart Panel Echo", "Hpad Echo", new[] { 0, 0, 0, 0, 1, 1, 2, 2 }),
        ("Smart Panel Q2", "Hpad Q2", new[] { 1, 1, 2, 2, 1, 2, 2, 6 }),
        ("Smart Panel Qbrid", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Wireless Zigbee Gateway", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Smart Lock i5", "Door Lock i5", new[] { 1, 1, 1, 1, 1, 1, 0, 0 }),
        ("Smart Lock i7", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Smart Lock i9 Pro", "Door Lock i9 Pro", new[] { 0, 0, 0, 0, 0, 0, 1, 1 }),
        ("Luxury Smart Switch Marina", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Piano Switch 1 Gang", "Piano Series", new[] { 0, 0, 0, 2, 2, 4, 6, 8 }),
        ("Piano Switch 2 Gang", "Piano Series", new[] { 1, 2, 2, 4, 4, 7, 10, 12 }),
        ("Piano Switch 3 Gang", "Piano Series", new[] { 5, 7, 13, 18, 23, 30, 45, 60 }),
        ("Floating Switch 1 Gang", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Floating Switch 2 Gang", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Floating Switch 3 Gang", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Floating Switch 4 Gang", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Smart Curtain Motor 1.5Nm", "Curtain Motor 1.5Nm, 6Nm", new[] { 0, 0, 0, 0, 3, 4, 4, 6 }),
        ("Smart Curtain Motor 6Nm", "Curtain Motor 1.5Nm, 6Nm", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Curtain Rail 1 meter", "Curtain Track", new[] { 0, 0, 0, 0, 12, 20, 20, 30 }),
        ("Smart Roller Blind 1.8Nm", "Roller Blind Motor 1.2Nm, 6Nm", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Smart Roller Blind 6Nm", "Roller Blind Motor 1.2Nm, 6Nm", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Anti Glare Smart Light 7w", "Smart Lights 7w", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Anti Glare Smart Light 10w", "Smart Lights 10w", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Door Window Sensor", "Door Sensor", new[] { 0, 0, 0, 0, 4, 5, 0, 7 }),
        ("Motion Sensor", "Motion Sensor", new[] { 0, 0, 0, 0, 1, 2, 0, 4 }),
        ("Water Leakage Sensor", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Smoke Sensor", "Smart Smoke Sensor", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Temp and Humidity Sensor", "Temp. and Humidity Sensor", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Gas Sensor", "Smart Gas Sensor", new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("IR Remote Control", "Zigbee IR Blaster (for AC and TV control)", new[] { 2, 3, 3, 6, 7, 9, 10, 15 }),
        ("Intercom Indoor Unit", null, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ("Intercom Outdoor Unit Gate Keeper", "Outdoor Station", new[] { 0, 0, 0, 0, 1, 1, 0, 0 }),
    };

    /// <summary>Gives the company the starting data, unless it already has (or ever had) solution data</summary>
    public static void Ensure(AppDbContext db, int tenantId)
    {
        if (db.PackageSizes.IgnoreQueryFilters().Any(s => s.TenantId == tenantId)
            || db.SolutionGroups.IgnoreQueryFilters().Any(g => g.TenantId == tenantId)) return;

        var lines = new Dictionary<string, SolutionProductLine>();
        var order = 0;
        foreach (var (group, groupLines) in Categorization)
        {
            var g = new SolutionGroup { TenantId = tenantId, Name = group, SortOrder = ++order };
            var lineOrder = 0;
            foreach (var (name, standard, premium) in groupLines)
            {
                var l = new SolutionProductLine { TenantId = tenantId, Name = name, InStandard = standard, InPremium = premium, SortOrder = ++lineOrder };
                g.ProductLines.Add(l);
                lines[name] = l;
            }
            db.SolutionGroups.Add(g);
        }

        var sizes = Sizes.Select((name, i) => new PackageSize { TenantId = tenantId, Name = name, SortOrder = i + 1 }).ToList();
        db.PackageSizes.AddRange(sizes);

        order = 0;
        foreach (var (name, line, qty) in Items)
        {
            var item = new PackageItem { TenantId = tenantId, Name = name, ProductLine = line == null ? null : lines[line], SortOrder = ++order };
            for (var i = 0; i < sizes.Count; i++)
                if (qty[i] > 0) item.Quantities.Add(new PackageQuantity { TenantId = tenantId, PackageSize = sizes[i], Quantity = qty[i] });
            db.PackageItems.Add(item);
        }
        db.SaveChanges();
    }
}
