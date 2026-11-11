using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public class StoreService : IStoreService
{
    private readonly AppDbContext _db;
    public StoreService(AppDbContext db) => _db = db;

    public async Task<List<DropdownOptionDto>> GetAllAsync()
    {
        // Branches come from Locations; old store rows are kept for names not yet migrated.
        var locations = await _db.Locations.Where(l => l.IsActive && l.Type != "Warehouse" && l.Type != "Factory")
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.Name)
            .Select(l => new DropdownOptionDto(l.Code, l.Name)).ToListAsync();
        var old = await _db.Stores.Select(s => new DropdownOptionDto(s.Value, s.Label)).ToListAsync();
        return locations.Concat(old.Where(o => !locations.Any(l => l.Value == o.Value || l.Label == o.Label))).ToList();
    }
}

public class WarehouseService : IWarehouseService
{
    private readonly AppDbContext _db;
    public WarehouseService(AppDbContext db) => _db = db;

    public async Task<List<DropdownOptionDto>> GetAllAsync()
    {
        // Every stock-holding location can send or receive stock.
        var locations = await _db.Locations.Where(l => l.IsActive)
            .OrderByDescending(l => l.Type == "Warehouse").ThenBy(l => l.Name)
            .Select(l => new DropdownOptionDto(l.Code, l.Name)).ToListAsync();
        var old = await _db.Warehouses.Select(w => new DropdownOptionDto(w.Value, w.Label)).ToListAsync();
        return locations.Concat(old.Where(o => !locations.Any(l => l.Value == o.Value || l.Label == o.Label))).ToList();
    }
}
