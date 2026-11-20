using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// Purchase and sale prices of the inventory products (Inventory > Sale Prices).
/// The purchase price (INVT_PPRC, one row per product) comes from the purchases: it is the unit cost on the
/// product's latest received purchase. A product never purchased has its sale price as its purchase price (no
/// margin) until it is bought. The sale price is the product's own price, set by people on Inventory > Sale Prices;
/// profit = sale price - purchase price.
/// </summary>
public static class PurchasePriceService
{
    /// <summary>UpdatedBy of a purchase price no purchase has set yet (it follows the sale price)</summary>
    public const string NotSetBy = "system";
    /// <summary>UpdatedBy of a purchase price taken from a purchase: "purchase PR-001"</summary>
    public const string FromPurchasePrefix = "purchase ";

    /// <summary>A product nobody has bought yet costs its sale price (no margin)</summary>
    public static decimal StartingPrice(decimal salePrice) => Math.Max(0, salePrice);

    /// <summary>True once the purchase price comes from a purchase (or was entered by a person)</summary>
    public static bool IsSet(ProductPurchasePrice row) => row.UpdatedBy != NotSetBy;

    /// <summary>Brings every product's purchase price up to date: a row for each product, the unit cost of its
    /// latest received purchase, and the sale price for products not bought yet. Returns how many rows changed.</summary>
    public static async Task<int> EnsureAllAsync(AppDbContext db, CancellationToken ct = default)
    {
        var missing = await db.Products.Where(p => !db.ProductPurchasePrices.Any(x => x.ProductId == p.Id))
            .Select(p => new { p.Id, p.Price }).ToListAsync(ct);
        foreach (var p in missing)
            db.ProductPurchasePrices.Add(new ProductPurchasePrice { ProductId = p.Id, PurchasePrice = StartingPrice(p.Price), UpdatedBy = NotSetBy });
        if (missing.Count > 0) await db.SaveChangesAsync(ct);

        // the latest received purchase line of every product (deleted purchases are left out by the query filters)
        var lines = await (from i in db.PurchaseItems
                           join p in db.Purchases on i.PurchaseId equals p.Id
                           where i.ProductId != null && p.Status == "Received"
                           select new { ProductId = i.ProductId!.Value, p.Date, PurchaseId = p.Id, ItemId = i.Id, i.UnitCost, i.PurchasePrice, p.Reference })
                          .ToListAsync(ct);
        var latest = lines.GroupBy(l => l.ProductId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(l => l.Date).ThenByDescending(l => l.PurchaseId).ThenByDescending(l => l.ItemId).First());

        var changed = missing.Count;
        foreach (var row in await db.ProductPurchasePrices.Include(x => x.Product).ToListAsync(ct))
        {
            if (row.Product == null) continue;
            decimal cost; string by; DateTime at;
            if (latest.TryGetValue(row.ProductId, out var l))
            {
                cost = Math.Round(Math.Max(0, l.UnitCost > 0 ? l.UnitCost : l.PurchasePrice), 2);
                by = Trim(FromPurchasePrefix + l.Reference);
                at = l.Date;
            }
            else if (row.UpdatedBy == NotSetBy || (row.UpdatedBy ?? "").StartsWith(FromPurchasePrefix))
            {
                // not bought (any more): the sale price, no margin
                cost = StartingPrice(row.Product.Price); by = NotSetBy; at = DateTime.UtcNow;
            }
            else continue;   // entered by a person (a new product's cost on a purchase order) and not bought yet: kept
            if (row.PurchasePrice == cost && row.UpdatedBy == by) continue;
            row.PurchasePrice = cost; row.UpdatedBy = by; row.UpdatedAt = at;
            changed++;
        }
        if (changed > missing.Count) await db.SaveChangesAsync(ct);
        return changed;
    }

    /// <summary>One product on Inventory > Sale Prices. PurchasedFrom: the purchase its purchase price comes from
    /// (null = not bought yet, the sale price is used)</summary>
    public record Row(int ProductId, string ProductName, string? Sku, string? Model, string? Color, decimal SalePrice, decimal PurchasePrice,
        decimal Profit, decimal ProfitPercent, string? PurchasedFrom, DateTime? PurchasedOn, bool IsSet, DateTime SalePriceUpdatedAt);

    public static async Task<List<Row>> ListAsync(AppDbContext db, CancellationToken ct = default)
    {
        await EnsureAllAsync(db, ct);
        return (await db.Products.AsNoTracking().Include(p => p.PurchasePriceInfo).OrderBy(p => p.ProductName).ToListAsync(ct))
            .Select(p =>
            {
                var pp = p.PurchasePriceInfo!;
                var fromPurchase = (pp.UpdatedBy ?? "").StartsWith(FromPurchasePrefix);
                var profit = p.Price - pp.PurchasePrice;
                return new Row(p.Id, p.ProductName, p.SKU, p.Model, p.Color, p.Price, pp.PurchasePrice, profit,
                    p.Price > 0 ? Math.Round(profit / p.Price * 100, 1) : 0,
                    fromPurchase ? pp.UpdatedBy![FromPurchasePrefix.Length..] : null, fromPurchase ? pp.UpdatedAt : null,
                    IsSet(pp), p.UpdatedAt);
            }).ToList();
    }

    public record Change(int ProductId, decimal PurchasePrice);
    public record SaleChange(int ProductId, decimal SalePrice);

    /// <summary>Saves sale prices (the products' own price); a price below 0 or a product of another company is refused</summary>
    public static async Task SaveSalePricesAsync(AppDbContext db, IEnumerable<SaleChange> changes, CancellationToken ct = default)
    {
        var list = changes.ToList();
        if (list.Any(c => c.SalePrice < 0)) throw new InvalidOperationException("A sale price can't be below 0.");
        var ids = list.Select(c => c.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        if (products.Count != ids.Count) throw new KeyNotFoundException("A product was not found.");
        foreach (var c in list)
        {
            var p = products[c.ProductId];
            var price = Math.Round(c.SalePrice, 2);
            if (p.Price == price) continue;
            p.Price = price;
            p.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await EnsureAllAsync(db, ct);     // products not bought yet: their purchase price follows the new sale price
    }

    /// <summary>Sets purchase prices by hand (a new product's cost typed on a purchase order); a received purchase
    /// of the product replaces it. A price below 0 or a product of another company is refused.</summary>
    public static async Task SaveAsync(AppDbContext db, IEnumerable<Change> changes, string? by, CancellationToken ct = default)
    {
        var list = changes.ToList();
        if (list.Any(c => c.PurchasePrice < 0)) throw new InvalidOperationException("A purchase price can't be below 0.");
        var ids = list.Select(c => c.ProductId).Distinct().ToList();
        var known = await db.Products.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        if (known.Count != ids.Count) throw new KeyNotFoundException("A product was not found.");
        await EnsureAllAsync(db, ct);
        var rows = await db.ProductPurchasePrices.Where(x => ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId, ct);
        var who = string.IsNullOrWhiteSpace(by) || by == NotSetBy || by.StartsWith(FromPurchasePrefix) ? "user" : Trim(by);
        foreach (var c in list)
        {
            var row = rows[c.ProductId];
            if ((row.UpdatedBy ?? "").StartsWith(FromPurchasePrefix)) continue;   // bought: the purchase decides
            row.PurchasePrice = Math.Round(c.PurchasePrice, 2);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = who;
        }
        await db.SaveChangesAsync(ct);
    }

    private static string Trim(string s) => s.Length > 150 ? s[..150] : s;
}
