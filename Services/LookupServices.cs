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
        return await _db.Stores
            .Select(s => new DropdownOptionDto(s.Value, s.Label, s.Id))
            .ToListAsync();
    }
}

public class WarehouseService : IWarehouseService
{
    private readonly AppDbContext _db;
    public WarehouseService(AppDbContext db) => _db = db;

    public async Task<List<DropdownOptionDto>> GetAllAsync()
    {
        return await _db.Warehouses
            .Select(w => new DropdownOptionDto(w.Value, w.Label, (int?)null))
            .ToListAsync();
    }
}
