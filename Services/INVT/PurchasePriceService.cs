using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// Purchase prices of the inventory products (INVT_PPRC). Every product has one. Until someone enters it on
/// Inventory > Purchase Prices, it is the product's sale price (no margin) and follows the sale price when that
/// changes; once a person has saved it, it is theirs and is never changed by the system.
/// </summary>
public static class PurchasePriceService
{
    /// <summary>UpdatedBy of a purchase price nobody has entered yet (it follows the sale price)</summary>
    public const string NotSetBy = "system";

    /// <summary>A purchase price nobody has entered yet is the sale price (no margin)</summary>
    public static decimal StartingPrice(decimal salePrice) => Math.Max(0, salePrice);

    public static bool IsSet(ProductPurchasePrice row) => row.UpdatedBy != NotSetBy;

    /// <summary>Adds the starting purchase price of every product of the company that has none, and moves the ones
    /// nobody has entered yet to the current sale price; returns how many rows were added or changed</summary>
    public static async Task<int> EnsureAllAsync(AppDbContext db, CancellationToken ct = default)
    {
        var missing = await db.Products.Where(p => !db.ProductPurchasePrices.Any(x => x.ProductId == p.Id))
            .Select(p => new { p.Id, p.Price }).ToListAsync(ct);
        foreach (var p in missing)
            db.ProductPurchasePrices.Add(new ProductPurchasePrice { ProductId = p.Id, PurchasePrice = StartingPrice(p.Price), UpdatedBy = NotSetBy });
        var following = await db.ProductPurchasePrices.Include(x => x.Product)
            .Where(x => x.UpdatedBy == NotSetBy && x.Product != null && x.PurchasePrice != (x.Product.Price < 0 ? 0 : x.Product.Price))
            .ToListAsync(ct);
        foreach (var row in following)
        {
            row.PurchasePrice = StartingPrice(row.Product!.Price);
            row.UpdatedAt = DateTime.UtcNow;
        }
        var changed = missing.Count + following.Count;
        if (changed > 0) await db.SaveChangesAsync(ct);
        return changed;
    }

    public record Row(int ProductId, string ProductName, string? Sku, string? Model, string? Color, decimal EndUserPrice, decimal PurchasePrice,
        decimal Margin, DateTime UpdatedAt, string? UpdatedBy, bool IsSet);

    public static async Task<List<Row>> ListAsync(AppDbContext db, CancellationToken ct = default)
    {
        await EnsureAllAsync(db, ct);
        return (await db.Products.AsNoTracking().Include(p => p.PurchasePriceInfo).OrderBy(p => p.ProductName).ToListAsync(ct))
            .Select(p => new Row(p.Id, p.ProductName, p.SKU, p.Model, p.Color, p.Price, p.PurchasePriceInfo!.PurchasePrice,
                p.Price - p.PurchasePriceInfo.PurchasePrice, p.PurchasePriceInfo.UpdatedAt,
                IsSet(p.PurchasePriceInfo) ? p.PurchasePriceInfo.UpdatedBy : null, IsSet(p.PurchasePriceInfo))).ToList();
    }

    public record Change(int ProductId, decimal PurchasePrice);

    /// <summary>Saves purchase prices; a price below 0 or a product of another company is refused. A saved price
    /// counts as entered (it stops following the sale price), even when it is the same amount.</summary>
    public static async Task SaveAsync(AppDbContext db, IEnumerable<Change> changes, string? by, CancellationToken ct = default)
    {
        var list = changes.ToList();
        if (list.Any(c => c.PurchasePrice < 0)) throw new InvalidOperationException("A purchase price can't be below 0.");
        var ids = list.Select(c => c.ProductId).Distinct().ToList();
        var known = await db.Products.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        if (known.Count != ids.Count) throw new KeyNotFoundException("A product was not found.");
        await EnsureAllAsync(db, ct);
        var rows = await db.ProductPurchasePrices.Where(x => ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId, ct);
        var who = string.IsNullOrWhiteSpace(by) || by == NotSetBy ? "user" : by.Length > 150 ? by[..150] : by;
        foreach (var c in list)
        {
            var row = rows[c.ProductId];
            if (row.PurchasePrice == Math.Round(c.PurchasePrice, 2) && IsSet(row)) continue;
            row.PurchasePrice = Math.Round(c.PurchasePrice, 2);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = who;
        }
        await db.SaveChangesAsync(ct);
    }
}
