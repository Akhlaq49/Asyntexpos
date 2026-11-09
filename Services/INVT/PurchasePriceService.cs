using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>
/// Purchase prices of the inventory products (INVT_PPRC). Every product has one; a product without one gets the
/// starting price: its end user price less <see cref="StartingMargin"/> (never below 0). People change them on
/// Inventory > Purchase Prices.
/// </summary>
public static class PurchasePriceService
{
    /// <summary>For now a new purchase price is the end user price less this</summary>
    public const decimal StartingMargin = 10000m;

    public static decimal StartingPrice(decimal endUserPrice) => Math.Max(0, endUserPrice - StartingMargin);

    /// <summary>Adds the starting purchase price of every product of the company that has none; returns how many</summary>
    public static async Task<int> EnsureAllAsync(AppDbContext db, CancellationToken ct = default)
    {
        var missing = await db.Products.Where(p => !db.ProductPurchasePrices.Any(x => x.ProductId == p.Id))
            .Select(p => new { p.Id, p.Price }).ToListAsync(ct);
        foreach (var p in missing)
            db.ProductPurchasePrices.Add(new ProductPurchasePrice { ProductId = p.Id, PurchasePrice = StartingPrice(p.Price), UpdatedBy = "system" });
        if (missing.Count > 0) await db.SaveChangesAsync(ct);
        return missing.Count;
    }

    public record Row(int ProductId, string ProductName, string? Sku, string? Model, string? Color, decimal EndUserPrice, decimal PurchasePrice,
        decimal Margin, DateTime UpdatedAt, string? UpdatedBy);

    public static async Task<List<Row>> ListAsync(AppDbContext db, CancellationToken ct = default)
    {
        await EnsureAllAsync(db, ct);
        return (await db.Products.AsNoTracking().Include(p => p.PurchasePriceInfo).OrderBy(p => p.ProductName).ToListAsync(ct))
            .Select(p => new Row(p.Id, p.ProductName, p.SKU, p.Model, p.Color, p.Price, p.PurchasePriceInfo!.PurchasePrice,
                p.Price - p.PurchasePriceInfo.PurchasePrice, p.PurchasePriceInfo.UpdatedAt, p.PurchasePriceInfo.UpdatedBy)).ToList();
    }

    public record Change(int ProductId, decimal PurchasePrice);

    /// <summary>Saves purchase prices; a price below 0 or a product of another company is refused</summary>
    public static async Task SaveAsync(AppDbContext db, IEnumerable<Change> changes, string? by, CancellationToken ct = default)
    {
        var list = changes.ToList();
        if (list.Any(c => c.PurchasePrice < 0)) throw new InvalidOperationException("A purchase price can't be below 0.");
        var ids = list.Select(c => c.ProductId).Distinct().ToList();
        var known = await db.Products.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        if (known.Count != ids.Count) throw new KeyNotFoundException("A product was not found.");
        await EnsureAllAsync(db, ct);
        var rows = await db.ProductPurchasePrices.Where(x => ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId, ct);
        foreach (var c in list)
        {
            var row = rows[c.ProductId];
            if (row.PurchasePrice == Math.Round(c.PurchasePrice, 2)) continue;
            row.PurchasePrice = Math.Round(c.PurchasePrice, 2);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = by is { Length: > 150 } ? by[..150] : by;
        }
        await db.SaveChangesAsync(ct);
    }
}
