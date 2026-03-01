using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class FinanceIncomeService : IFinanceIncomeService
{
    private readonly AppDbContext _db;
    public FinanceIncomeService(AppDbContext db) => _db = db;

    private static string GenerateReference() => $"INC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";

    public async Task<List<FinanceIncomeDto>> GetAllAsync()
    {
        return await _db.FinanceIncomes
            .Include(i => i.IncomeCategory)
            .OrderByDescending(i => i.Date)
            .Select(i => new FinanceIncomeDto
            {
                Id = i.Id,
                Date = i.Date.ToString("dd MMM yyyy"),
                Reference = i.Reference,
                Store = i.Store,
                IncomeCategoryId = i.IncomeCategoryId,
                CategoryName = i.IncomeCategory != null ? i.IncomeCategory.Name : "",
                Notes = i.Notes,
                Amount = i.Amount,
                Account = i.Account,
                CreatedOn = i.CreatedOn.ToString("dd MMM yyyy")
            })
            .ToListAsync();
    }

    public async Task<FinanceIncomeDto?> GetByIdAsync(int id)
    {
        var i = await _db.FinanceIncomes.Include(x => x.IncomeCategory).FirstOrDefaultAsync(x => x.Id == id);
        if (i == null) return null;
        return new FinanceIncomeDto
        {
            Id = i.Id, Date = i.Date.ToString("dd MMM yyyy"), Reference = i.Reference,
            Store = i.Store, IncomeCategoryId = i.IncomeCategoryId,
            CategoryName = i.IncomeCategory?.Name ?? "", Notes = i.Notes,
            Amount = i.Amount, Account = i.Account, CreatedOn = i.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<FinanceIncomeDto> CreateAsync(CreateFinanceIncomeDto dto)
    {
        var entity = new FinanceIncome
        {
            Date = dto.Date,
            Reference = GenerateReference(),
            Store = dto.Store,
            IncomeCategoryId = dto.IncomeCategoryId,
            Notes = dto.Notes,
            Amount = dto.Amount,
            Account = dto.Account
        };
        _db.FinanceIncomes.Add(entity);
        await _db.SaveChangesAsync();

        var cat = await _db.IncomeCategories.FindAsync(entity.IncomeCategoryId);
        return new FinanceIncomeDto
        {
            Id = entity.Id, Date = entity.Date.ToString("dd MMM yyyy"), Reference = entity.Reference,
            Store = entity.Store, IncomeCategoryId = entity.IncomeCategoryId,
            CategoryName = cat?.Name ?? "", Notes = entity.Notes,
            Amount = entity.Amount, Account = entity.Account,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<FinanceIncomeDto?> UpdateAsync(int id, CreateFinanceIncomeDto dto)
    {
        var entity = await _db.FinanceIncomes.FindAsync(id);
        if (entity == null) return null;
        entity.Date = dto.Date;
        entity.Store = dto.Store;
        entity.IncomeCategoryId = dto.IncomeCategoryId;
        entity.Notes = dto.Notes;
        entity.Amount = dto.Amount;
        entity.Account = dto.Account;
        await _db.SaveChangesAsync();

        var cat = await _db.IncomeCategories.FindAsync(entity.IncomeCategoryId);
        return new FinanceIncomeDto
        {
            Id = entity.Id, Date = entity.Date.ToString("dd MMM yyyy"), Reference = entity.Reference,
            Store = entity.Store, IncomeCategoryId = entity.IncomeCategoryId,
            CategoryName = cat?.Name ?? "", Notes = entity.Notes,
            Amount = entity.Amount, Account = entity.Account,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.FinanceIncomes.FindAsync(id);
        if (entity == null) return (false, null);
        _db.FinanceIncomes.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
