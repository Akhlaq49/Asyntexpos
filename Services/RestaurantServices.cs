using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public interface IRestaurantService
{
    /// <summary>Deducts recipe ingredients for dishes on the bill and sends kitchen tickets.</summary>
    Task OnSaleCreatedAsync(Sale sale);
    /// <summary>Puts ingredients back and removes the bill's kitchen tickets.</summary>
    Task OnSaleDeletedAsync(int saleId);
    /// <summary>Ingredient cost of one portion.</summary>
    Task<decimal> PlateCostAsync(int productId);
}

public record ChosenModifier(int ModifierId, string? Name, decimal Price);

public class RestaurantService : IRestaurantService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;

    public RestaurantService(AppDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    private static List<ChosenModifier> ParseModifiers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<ChosenModifier>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); }
        catch (JsonException) { return new(); }
    }

    public async Task<decimal> PlateCostAsync(int productId)
    {
        var recipe = await _db.Recipes.Include(r => r.Items).FirstOrDefaultAsync(r => r.ProductId == productId);
        if (recipe == null || recipe.Items.Count == 0) return 0;
        var ids = recipe.Items.Select(i => i.IngredientId).ToList();
        var costs = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PurchasePrice);
        var total = recipe.Items.Sum(i => i.Quantity * (1 + i.WastagePct / 100m) * costs.GetValueOrDefault(i.IngredientId));
        return recipe.YieldQty > 0 ? Math.Round(total / recipe.YieldQty, 4) : 0;
    }

    public async Task OnSaleCreatedAsync(Sale sale)
    {
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var dishes = await _db.Products.Where(p => productIds.Contains(p.Id) && p.IsMenuItem).ToDictionaryAsync(p => p.Id);
        if (dishes.Count == 0) return;

        var at = new StockRef("Consumption", sale.Reference, sale.Id, sale.LocationId, null, "Sale");
        var recipes = await _db.Recipes.Include(r => r.Items).Where(r => productIds.Contains(r.ProductId)).ToDictionaryAsync(r => r.ProductId);
        var modifierIds = sale.Items.SelectMany(i => ParseModifiers(i.ModifiersJson).Select(m => m.ModifierId)).Distinct().ToList();
        var modifiers = await _db.Modifiers.Where(m => modifierIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        // Ingredients needed for the whole bill, in base units.
        var need = new Dictionary<int, decimal>();
        var lineCost = new Dictionary<SaleItem, decimal>();
        foreach (var item in sale.Items.Where(i => dishes.ContainsKey(i.ProductId)))
        {
            var portions = (decimal)(item.UnitQuantity > 0 ? item.UnitQuantity : item.Quantity);
            if (recipes.TryGetValue(item.ProductId, out var r) && r.YieldQty > 0)
                foreach (var ri in r.Items)
                    need[ri.IngredientId] = need.GetValueOrDefault(ri.IngredientId) + ri.Quantity * (1 + ri.WastagePct / 100m) * portions / r.YieldQty;
            foreach (var m in ParseModifiers(item.ModifiersJson))
                if (modifiers.TryGetValue(m.ModifierId, out var mod) && mod.IngredientId.HasValue && mod.IngredientQty > 0)
                    need[mod.IngredientId.Value] = need.GetValueOrDefault(mod.IngredientId.Value) + mod.IngredientQty.Value * portions;
        }

        var ingredientCost = new Dictionary<int, decimal>();
        foreach (var (ingredientId, qty) in need)
        {
            var units = (int)Math.Ceiling(qty);
            if (units <= 0) continue;
            var ing = await _db.Products.FirstOrDefaultAsync(p => p.Id == ingredientId);
            if (ing == null) continue;
            // The kitchen does not stop a sale for missing stock; it is drawn from other locations or goes negative and shows on the stock reports.
            var taken = await _inventory.DeductFefoAsync(ing, units, at, spillToOtherLocations: true);
            ingredientCost[ingredientId] = _inventory.AverageCost(taken, ing.PurchasePrice);
        }

        foreach (var item in sale.Items.Where(i => dishes.ContainsKey(i.ProductId)))
        {
            var portions = (decimal)(item.UnitQuantity > 0 ? item.UnitQuantity : item.Quantity);
            decimal cost = 0;
            if (recipes.TryGetValue(item.ProductId, out var r) && r.YieldQty > 0)
                cost += r.Items.Sum(ri => ri.Quantity * (1 + ri.WastagePct / 100m) / r.YieldQty * ingredientCost.GetValueOrDefault(ri.IngredientId, 0));
            foreach (var m in ParseModifiers(item.ModifiersJson))
                if (modifiers.TryGetValue(m.ModifierId, out var mod) && mod.IngredientId.HasValue && mod.IngredientQty > 0)
                    cost += mod.IngredientQty.Value * ingredientCost.GetValueOrDefault(mod.IngredientId.Value, 0);
            item.CostPrice = Math.Round(cost, 4);
        }

        // Kitchen tickets, one per station.
        var stations = sale.Items.Where(i => dishes.TryGetValue(i.ProductId, out var d) && d.KitchenStationId != null)
            .GroupBy(i => dishes[i.ProductId].KitchenStationId!.Value);
        foreach (var g in stations)
        {
            var ticket = new KitchenTicket
            {
                SaleId = sale.Id, StationId = g.Key, TicketNo = $"{sale.Reference}-{g.Key}", Status = "New",
                Items = g.Select(i => new KitchenTicketItem
                {
                    SaleItemId = i.Id, ItemName = i.ProductName, Quantity = i.UnitQuantity > 0 ? i.UnitQuantity : i.Quantity,
                    Notes = string.Join(", ", ParseModifiers(i.ModifiersJson).Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
                }).ToList()
            };
            _db.KitchenTickets.Add(ticket);
        }

        if (sale.DiningTableId.HasValue)
        {
            var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.Id == sale.DiningTableId);
            if (table != null) table.Status = "Occupied";
        }
        await _db.SaveChangesAsync();
    }

    public async Task OnSaleDeletedAsync(int saleId)
    {
        var used = await _db.StockMovements.Where(m => m.SourceType == "Sale" && m.SourceId == saleId && m.MovementType == "Consumption").ToListAsync();
        var reference = await _db.Sales.Where(s => s.Id == saleId).Select(s => s.Reference).FirstOrDefaultAsync();
        foreach (var g in used.GroupBy(m => m.ProductId))
        {
            var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == g.Key);
            if (p == null) continue;
            var at = new StockRef("Consumption", $"{reference} deleted", saleId, null, null, "SaleReversal");
            await _inventory.RestoreAsync(p,
                g.Where(m => m.BatchId != null).Select(m => new BatchAllocation(m.BatchId!.Value, -m.Quantity, m.UnitCost, "", null)),
                g.Where(m => m.BatchId == null).Sum(m => -m.Quantity), at);
        }
        _db.KitchenTickets.RemoveRange(_db.KitchenTickets.Where(t => t.SaleId == saleId));
        await _db.SaveChangesAsync();
    }
}
