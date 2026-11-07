using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// The product catalogue of the logged-in company (every query goes through the tenant filter of AppDbContext).
/// category = SolutionGroup, family = SolutionProductLine (tiers in CatalogFamilyTiers), product = CatalogProduct,
/// SKU = Product (the inventory row), prices = the active CatalogPriceList.
/// </summary>
public class CatalogService
{
    /// <summary>The plans the drawing reader knows (CatalogTiers.Code)</summary>
    public const string Standard = "standard", Premium = "premium";

    private readonly AppDbContext _db;
    public CatalogService(AppDbContext db) => _db = db;

    /// <summary>Name used for matching: lower case, single spaces, trimmed</summary>
    public static string Key(string? name) => Regex.Replace((name ?? "").Trim().ToLowerInvariant(), @"\s+", " ");

    /// <summary>Product name of a SKU: its name without the colour at the end ("Innswitch Piano 1 Gang White" ->
    /// "Innswitch Piano 1 Gang"). Same rule as Database/CatalogMigrate.sql.</summary>
    public static string ProductNameOf(string skuName, string? color)
    {
        var name = Regex.Replace((skuName ?? "").Trim(), @"\s+", " ");
        var c = (color ?? "").Trim();
        if (c.Length > 0 && name.Length > c.Length && name.EndsWith(" " + c, StringComparison.OrdinalIgnoreCase))
            name = name[..^c.Length].TrimEnd();
        return name;
    }

    // ── tiers ────────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The company's tiers; a new company gets Standard and Premium</summary>
    public async Task<List<CatalogTier>> TiersAsync(CancellationToken ct = default)
    {
        var tiers = await _db.CatalogTiers.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync(ct);
        if (tiers.Count > 0) return tiers;
        _db.CatalogTiers.AddRange(new CatalogTier { Name = "Standard", Code = Standard, SortOrder = 1 },
                                  new CatalogTier { Name = "Premium", Code = Premium, SortOrder = 2 });
        await _db.SaveChangesAsync(ct);
        return await _db.CatalogTiers.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync(ct);
    }

    /// <summary>The Standard / Premium ticks of a family (Product Categorization tab) as tier rows</summary>
    public async Task SetFamilyTiersAsync(int familyId, bool inStandard, bool inPremium, CancellationToken ct = default)
    {
        var tiers = await TiersAsync(ct);
        var want = tiers.Where(t => (t.Code == Standard && inStandard) || (t.Code == Premium && inPremium)).Select(t => t.Id).ToHashSet();
        var have = await _db.CatalogFamilyTiers.Where(x => x.FamilyId == familyId).ToListAsync(ct);
        _db.CatalogFamilyTiers.RemoveRange(have.Where(x => !want.Contains(x.TierId) && tiers.Any(t => t.Id == x.TierId && (t.Code == Standard || t.Code == Premium))));
        foreach (var tierId in want.Where(id => have.All(x => x.TierId != id)))
            _db.CatalogFamilyTiers.Add(new CatalogFamilyTier { FamilyId = familyId, TierId = tierId });
        await _db.SaveChangesAsync(ct);
    }

    // ── inventory products (SKUs) linked to catalogue products ───────────────────────────────────────────
    /// <summary>
    /// Inventory products not linked yet become SKUs: linked to the catalogue product with their name without colour
    /// (or its alias), else a new catalogue product; the first SKU of a product is its default. Prices stay on the
    /// inventory product (Products.Price).
    /// </summary>
    public async Task SyncSkusAsync(CancellationToken ct = default)
    {
        await TiersAsync(ct);
        var loose = await _db.Products.Where(p => p.CatalogProductId == null).ToListAsync(ct);
        if (loose.Count > 0)
        {
            var products = await _db.CatalogProducts.ToListAsync(ct);
            var byKey = new Dictionary<string, CatalogProduct>();
            foreach (var p in products) byKey.TryAdd(Key(p.Name), p);
            var byAlias = (await _db.CatalogAliases.Where(a => a.TargetKind == "product").ToListAsync(ct))
                .GroupBy(a => a.AliasKey).ToDictionary(g => g.Key, g => g.First().CatalogProductId!.Value);
            foreach (var sku in loose)
            {
                var key = Key(ProductNameOf(sku.ProductName, sku.Color));
                if (byKey.ContainsKey(key) || byAlias.ContainsKey(key)) continue;
                var created = new CatalogProduct { Name = ProductNameOf(sku.ProductName, sku.Color), Description = sku.Description };
                _db.CatalogProducts.Add(created);
                byKey[key] = created;
            }
            await EnsureColorsAsync(loose.Select(s => s.Color), ct);
            await _db.SaveChangesAsync(ct);
            var colors = await ColorIdsAsync(ct);
            foreach (var sku in loose)
            {
                var key = Key(ProductNameOf(sku.ProductName, sku.Color));
                sku.CatalogProductId = byKey.TryGetValue(key, out var p) ? p.Id : byAlias[key];
                sku.ColorId = colors.TryGetValue(Key(sku.Color), out var c) ? c : null;
            }
            await _db.SaveChangesAsync(ct);
        }
        await EnsureDefaultSkusAsync(ct);
    }

    public async Task EnsureColorsAsync(IEnumerable<string?> names, CancellationToken ct = default)
    {
        var have = await ColorIdsAsync(ct);
        foreach (var name in names.Select(n => (n ?? "").Trim()).Where(n => n.Length > 0).DistinctBy(Key))
            if (!have.ContainsKey(Key(name)))
            {
                _db.CatalogColors.Add(new CatalogColor { Name = name });
                have[Key(name)] = 0;
            }
    }

    public async Task<Dictionary<string, int>> ColorIdsAsync(CancellationToken ct = default) =>
        (await _db.CatalogColors.ToListAsync(ct)).GroupBy(c => Key(c.Name)).ToDictionary(g => g.Key, g => g.First().Id);

    /// <summary>A product with SKUs but no default: its first SKU</summary>
    public async Task EnsureDefaultSkusAsync(CancellationToken ct = default)
    {
        var skus = await _db.Products.Where(p => p.CatalogProductId != null)
            .Select(p => new { p.Id, p.CatalogProductId, p.IsDefaultSku }).ToListAsync(ct);
        var ids = skus.GroupBy(s => s.CatalogProductId).Where(g => !g.Any(x => x.IsDefaultSku)).Select(g => g.Min(x => x.Id)).ToList();
        if (ids.Count > 0)
            await _db.Products.Where(p => ids.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefaultSku, true), ct);
    }

    // ── what the drawing reader works with ───────────────────────────────────────────────────────────────
    public static string UsageOf(bool inStandard, bool inPremium) =>
        inStandard && inPremium ? "both" : inStandard ? "standard" : inPremium ? "premium" : "none";

    /// <summary>The company's catalogue for one plan, in the reader's format: products (with their family, usage,
    /// default SKU and the plan's package quantities per size), the families, and the products of each drawing rule.</summary>
    public async Task<object> ReaderCatalogAsync(string plan, CancellationToken ct = default)
    {
        await SyncSkusAsync(ct);
        var tiers = await TiersAsync(ct);
        var tier = tiers.FirstOrDefault(t => t.Code == plan);
        var standardId = tiers.FirstOrDefault(t => t.Code == Standard)?.Id;
        var premiumId = tiers.FirstOrDefault(t => t.Code == Premium)?.Id;

        var sizes = await _db.PackageSizes.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var groups = await _db.SolutionGroups.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        var families = await _db.SolutionProductLines.AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToListAsync(ct);
        var familyTiers = (await _db.CatalogFamilyTiers.AsNoTracking().ToListAsync(ct)).ToLookup(x => x.FamilyId, x => x.TierId);
        string Usage(int? familyId) => familyId is int f && families.Any(x => x.Id == f)
            ? UsageOf(familyTiers[f].Contains(standardId ?? -1), familyTiers[f].Contains(premiumId ?? -1)) : "none";

        var products = await _db.CatalogProducts.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        var skus = (await _db.Products.AsNoTracking().Where(p => p.IsDefaultSku && p.CatalogProductId != null)
            .Select(p => new { p.Id, p.ProductName, p.CatalogProductId }).ToListAsync(ct)).ToDictionary(p => p.CatalogProductId!.Value);
        var quantities = tier == null ? new Dictionary<int, Dictionary<string, decimal>>()
            : (await _db.CatalogPackageLines.AsNoTracking().Where(l => l.TierId == tier.Id).ToListAsync(ct))
                .GroupBy(l => l.CatalogProductId).ToDictionary(g => g.Key, g => g.ToDictionary(l => l.PackageSizeId.ToString(), l => l.Quantity));
        var rules = (await _db.CatalogDrawingRuleItems.AsNoTracking().OrderBy(r => r.Rank).ThenBy(r => r.Id).ToListAsync(ct))
            .GroupBy(r => r.RuleName).ToDictionary(g => g.Key, g => g.GroupBy(r => r.Gang).ToDictionary(x => x.Key, x => x.Select(r => r.CatalogProductId).ToList()));

        return new
        {
            sizes,
            items = products.Select(p =>
            {
                var family = families.FirstOrDefault(f => f.Id == p.FamilyId);
                var sku = skus.GetValueOrDefault(p.Id);
                return new
                {
                    p.Id, p.Name, p.Unit,
                    groupName = family != null ? groups.GetValueOrDefault(family.GroupId) : null,
                    productLineName = family?.Name,
                    usage = Usage(p.FamilyId),
                    productId = sku?.Id,                 // the default SKU: what the quotation adds
                    productName = sku?.ProductName,
                    quantities = quantities.GetValueOrDefault(p.Id) ?? new Dictionary<string, decimal>(),
                };
            }),
            lines = families.Select(f => new { f.Name, groupName = groups.GetValueOrDefault(f.GroupId), usage = Usage(f.Id) }),
            ruleItems = rules,
        };
    }

    /// <summary>Price of each product for a quotation: its default SKU's inventory price (Products.Price); a product
    /// with no SKU (not in the inventory) has its fallback price, if one was entered. Null = no price.</summary>
    public async Task<Dictionary<int, (decimal? Price, int? SkuId)>> PricesAsync(IEnumerable<int> catalogProductIds, CancellationToken ct = default)
    {
        var ids = catalogProductIds.Distinct().ToList();
        var products = await _db.CatalogProducts.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var skus = await _db.Products.AsNoTracking().Where(p => p.IsDefaultSku && p.CatalogProductId != null && ids.Contains(p.CatalogProductId!.Value))
            .Select(p => new { p.Id, p.CatalogProductId, p.Price }).ToListAsync(ct);
        var result = new Dictionary<int, (decimal?, int?)>();
        foreach (var id in ids)
        {
            if (!products.TryGetValue(id, out var p)) continue;
            var sku = skus.FirstOrDefault(s => s.CatalogProductId == id);
            result[id] = sku != null ? (sku.Price, sku.Id) : (p.FallbackPrice, null);
        }
        return result;
    }
}
