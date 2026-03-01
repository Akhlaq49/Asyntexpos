using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class MiscellaneousRegisterService : IMiscellaneousRegisterService
{
    private readonly AppDbContext _db;

    public MiscellaneousRegisterService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetBalanceAsync(int customerId)
    {
        var transactions = await _db.MiscellaneousRegisters
            .Where(m => m.CustomerId == customerId)
            .ToListAsync();

        var credits = transactions.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount);
        var debits = transactions.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount);
        return credits - debits;
    }

    public async Task<List<MiscellaneousRegisterDto>> GetByCustomerAsync(int customerId)
    {
        return await _db.MiscellaneousRegisters
            .Include(m => m.Customer)
            .Where(m => m.CustomerId == customerId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MiscellaneousRegisterDto
            {
                Id = m.Id.ToString(),
                CustomerId = m.CustomerId.ToString(),
                CustomerName = m.Customer!.FullName,
                TransactionType = m.TransactionType,
                Amount = m.Amount,
                Description = m.Description,
                ReferenceId = m.ReferenceId,
                ReferenceType = m.ReferenceType,
                CreatedAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                CreatedBy = m.CreatedBy
            })
            .ToListAsync();
    }

    public async Task<MiscellaneousRegisterDto> CreateAsync(CreateMiscTransactionDto dto)
    {
        var customer = await _db.Parties.FirstOrDefaultAsync(p => p.Id == dto.CustomerId && p.Role == "Customer")
            ?? throw new KeyNotFoundException("Customer not found");

        var transaction = new MiscellaneousRegister
        {
            CustomerId = dto.CustomerId,
            TransactionType = dto.TransactionType,
            Amount = dto.Amount,
            Description = dto.Description,
            ReferenceId = dto.ReferenceId,
            ReferenceType = dto.ReferenceType,
            CreatedBy = "Admin"
        };

        _db.MiscellaneousRegisters.Add(transaction);
        await _db.SaveChangesAsync();

        return new MiscellaneousRegisterDto
        {
            Id = transaction.Id.ToString(),
            CustomerId = transaction.CustomerId.ToString(),
            CustomerName = customer.FullName,
            TransactionType = transaction.TransactionType,
            Amount = transaction.Amount,
            Description = transaction.Description,
            ReferenceId = transaction.ReferenceId,
            ReferenceType = transaction.ReferenceType,
            CreatedAt = transaction.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            CreatedBy = transaction.CreatedBy
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var transaction = await _db.MiscellaneousRegisters.FindAsync(id);
        if (transaction == null) return false;

        _db.MiscellaneousRegisters.Remove(transaction);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<object> GetSummaryAsync()
    {
        return await _db.MiscellaneousRegisters
            .Include(m => m.Customer)
            .GroupBy(m => m.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                CustomerName = g.First().Customer!.FullName,
                TotalCredits = g.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount),
                TotalDebits = g.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount),
                Balance = g.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount) -
                         g.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount),
                TransactionCount = g.Count(),
                LastTransaction = g.Max(t => t.CreatedAt)
            })
            .Where(x => x.Balance != 0)
            .OrderByDescending(x => x.Balance)
            .ToListAsync();
    }
}
