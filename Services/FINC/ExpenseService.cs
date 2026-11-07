using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _db;
    public ExpenseService(AppDbContext db) => _db = db;

    private static string GenerateReference() => $"EXP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";

    public async Task<List<ExpenseDto>> GetAllAsync()
    {
        return await _db.Expenses
            .Include(e => e.ExpenseCategory)
            .OrderByDescending(e => e.Date)
            .Select(e => new ExpenseDto
            {
                Id = e.Id,
                Reference = e.Reference,
                ExpenseName = e.ExpenseName,
                ExpenseCategoryId = e.ExpenseCategoryId,
                CategoryName = e.ExpenseCategory != null ? e.ExpenseCategory.Name : "",
                Description = e.Description,
                Date = e.Date.ToString("dd MMM yyyy"),
                Amount = e.Amount,
                Status = e.Status,
                CreatedOn = e.CreatedOn.ToString("dd MMM yyyy")
            })
            .ToListAsync();
    }

    public async Task<ExpenseDto?> GetByIdAsync(int id)
    {
        var e = await _db.Expenses.Include(x => x.ExpenseCategory).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return null;
        return new ExpenseDto
        {
            Id = e.Id, Reference = e.Reference, ExpenseName = e.ExpenseName,
            ExpenseCategoryId = e.ExpenseCategoryId,
            CategoryName = e.ExpenseCategory?.Name ?? "",
            Description = e.Description, Date = e.Date.ToString("dd MMM yyyy"),
            Amount = e.Amount, Status = e.Status, CreatedOn = e.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<ExpenseDto> CreateAsync(CreateExpenseDto dto)
    {
        var entity = new Expense
        {
            Reference = GenerateReference(),
            ExpenseName = dto.ExpenseName,
            ExpenseCategoryId = dto.ExpenseCategoryId,
            Description = dto.Description,
            Date = dto.Date,
            Amount = dto.Amount,
            Status = dto.Status
        };
        _db.Expenses.Add(entity);
        await _db.SaveChangesAsync();

        var cat = await _db.ExpenseCategories.FindAsync(entity.ExpenseCategoryId);
        return new ExpenseDto
        {
            Id = entity.Id, Reference = entity.Reference, ExpenseName = entity.ExpenseName,
            ExpenseCategoryId = entity.ExpenseCategoryId, CategoryName = cat?.Name ?? "",
            Description = entity.Description, Date = entity.Date.ToString("dd MMM yyyy"),
            Amount = entity.Amount, Status = entity.Status, CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<ExpenseDto?> UpdateAsync(int id, CreateExpenseDto dto)
    {
        var entity = await _db.Expenses.FindAsync(id);
        if (entity == null) return null;
        entity.ExpenseName = dto.ExpenseName;
        entity.ExpenseCategoryId = dto.ExpenseCategoryId;
        entity.Description = dto.Description;
        entity.Date = dto.Date;
        entity.Amount = dto.Amount;
        entity.Status = dto.Status;
        await _db.SaveChangesAsync();

        var cat = await _db.ExpenseCategories.FindAsync(entity.ExpenseCategoryId);
        return new ExpenseDto
        {
            Id = entity.Id, Reference = entity.Reference, ExpenseName = entity.ExpenseName,
            ExpenseCategoryId = entity.ExpenseCategoryId, CategoryName = cat?.Name ?? "",
            Description = entity.Description, Date = entity.Date.ToString("dd MMM yyyy"),
            Amount = entity.Amount, Status = entity.Status, CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.Expenses.FindAsync(id);
        if (entity == null) return (false, null);
        _db.Expenses.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
