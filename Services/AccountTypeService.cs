using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class AccountTypeService : IAccountTypeService
{
    private readonly AppDbContext _db;
    public AccountTypeService(AppDbContext db) => _db = db;

    public async Task<List<AccountTypeDto>> GetAllAsync()
    {
        return await _db.AccountTypes
            .OrderByDescending(t => t.CreatedOn)
            .Select(t => new AccountTypeDto
            {
                Id = t.Id,
                Name = t.Name,
                Status = t.Status,
                CreatedOn = t.CreatedOn.ToString("dd MMM yyyy")
            })
            .ToListAsync();
    }

    public async Task<AccountTypeDto?> GetByIdAsync(int id)
    {
        var t = await _db.AccountTypes.FindAsync(id);
        if (t == null) return null;
        return new AccountTypeDto
        {
            Id = t.Id, Name = t.Name, Status = t.Status,
            CreatedOn = t.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<AccountTypeDto> CreateAsync(CreateAccountTypeDto dto)
    {
        var entity = new AccountType { Name = dto.Name, Status = dto.Status };
        _db.AccountTypes.Add(entity);
        await _db.SaveChangesAsync();
        return new AccountTypeDto
        {
            Id = entity.Id, Name = entity.Name, Status = entity.Status,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<AccountTypeDto?> UpdateAsync(int id, CreateAccountTypeDto dto)
    {
        var entity = await _db.AccountTypes.FindAsync(id);
        if (entity == null) return null;
        entity.Name = dto.Name;
        entity.Status = dto.Status;
        await _db.SaveChangesAsync();
        return new AccountTypeDto
        {
            Id = entity.Id, Name = entity.Name, Status = entity.Status,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.AccountTypes.FindAsync(id);
        if (entity == null) return (false, null);
        var count = await _db.BankAccounts.CountAsync(b => b.AccountTypeId == id);
        if (count > 0) return (false, $"Cannot delete. {count} bank account(s) use this type.");
        _db.AccountTypes.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
