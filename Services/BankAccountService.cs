using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class BankAccountService : IBankAccountService
{
    private readonly AppDbContext _db;
    public BankAccountService(AppDbContext db) => _db = db;

    public async Task<List<BankAccountDto>> GetAllAsync()
    {
        return await _db.BankAccounts
            .Include(b => b.AccountType)
            .OrderByDescending(b => b.CreatedOn)
            .Select(b => new BankAccountDto
            {
                Id = b.Id,
                HolderName = b.HolderName,
                AccountNumber = b.AccountNumber,
                BankName = b.BankName,
                Branch = b.Branch,
                IFSC = b.IFSC,
                AccountTypeId = b.AccountTypeId,
                AccountTypeName = b.AccountType != null ? b.AccountType.Name : "",
                OpeningBalance = b.OpeningBalance,
                Notes = b.Notes,
                Status = b.Status,
                IsDefault = b.IsDefault,
                CreatedOn = b.CreatedOn.ToString("dd MMM yyyy")
            })
            .ToListAsync();
    }

    public async Task<BankAccountDto?> GetByIdAsync(int id)
    {
        var b = await _db.BankAccounts.Include(x => x.AccountType).FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return null;
        return new BankAccountDto
        {
            Id = b.Id, HolderName = b.HolderName, AccountNumber = b.AccountNumber,
            BankName = b.BankName, Branch = b.Branch, IFSC = b.IFSC,
            AccountTypeId = b.AccountTypeId, AccountTypeName = b.AccountType?.Name ?? "",
            OpeningBalance = b.OpeningBalance, Notes = b.Notes,
            Status = b.Status, IsDefault = b.IsDefault,
            CreatedOn = b.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<BankAccountDto> CreateAsync(CreateBankAccountDto dto)
    {
        // If making default, unset others
        if (dto.IsDefault)
            await _db.BankAccounts.Where(b => b.IsDefault).ForEachAsync(b => b.IsDefault = false);

        var entity = new BankAccount
        {
            HolderName = dto.HolderName,
            AccountNumber = dto.AccountNumber,
            BankName = dto.BankName,
            Branch = dto.Branch,
            IFSC = dto.IFSC,
            AccountTypeId = dto.AccountTypeId,
            OpeningBalance = dto.OpeningBalance,
            Notes = dto.Notes,
            Status = dto.Status,
            IsDefault = dto.IsDefault
        };
        _db.BankAccounts.Add(entity);
        await _db.SaveChangesAsync();

        var type = await _db.AccountTypes.FindAsync(entity.AccountTypeId);
        return new BankAccountDto
        {
            Id = entity.Id, HolderName = entity.HolderName, AccountNumber = entity.AccountNumber,
            BankName = entity.BankName, Branch = entity.Branch, IFSC = entity.IFSC,
            AccountTypeId = entity.AccountTypeId, AccountTypeName = type?.Name ?? "",
            OpeningBalance = entity.OpeningBalance, Notes = entity.Notes,
            Status = entity.Status, IsDefault = entity.IsDefault,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<BankAccountDto?> UpdateAsync(int id, CreateBankAccountDto dto)
    {
        var entity = await _db.BankAccounts.FindAsync(id);
        if (entity == null) return null;

        if (dto.IsDefault)
            await _db.BankAccounts.Where(b => b.IsDefault && b.Id != id).ForEachAsync(b => b.IsDefault = false);

        entity.HolderName = dto.HolderName;
        entity.AccountNumber = dto.AccountNumber;
        entity.BankName = dto.BankName;
        entity.Branch = dto.Branch;
        entity.IFSC = dto.IFSC;
        entity.AccountTypeId = dto.AccountTypeId;
        entity.OpeningBalance = dto.OpeningBalance;
        entity.Notes = dto.Notes;
        entity.Status = dto.Status;
        entity.IsDefault = dto.IsDefault;
        await _db.SaveChangesAsync();

        var type = await _db.AccountTypes.FindAsync(entity.AccountTypeId);
        return new BankAccountDto
        {
            Id = entity.Id, HolderName = entity.HolderName, AccountNumber = entity.AccountNumber,
            BankName = entity.BankName, Branch = entity.Branch, IFSC = entity.IFSC,
            AccountTypeId = entity.AccountTypeId, AccountTypeName = type?.Name ?? "",
            OpeningBalance = entity.OpeningBalance, Notes = entity.Notes,
            Status = entity.Status, IsDefault = entity.IsDefault,
            CreatedOn = entity.CreatedOn.ToString("dd MMM yyyy")
        };
    }

    public async Task<(bool success, string? error)> DeleteAsync(int id)
    {
        var entity = await _db.BankAccounts.FindAsync(id);
        if (entity == null) return (false, null);
        _db.BankAccounts.Remove(entity);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
