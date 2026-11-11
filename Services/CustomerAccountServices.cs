using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>Same paid/due rules the sale service uses, shared with receipts and credit notes.</summary>
public static class SalePaymentMath
{
    public const decimal Epsilon = 0.005m;

    public static void Recalculate(Sale sale)
    {
        sale.Due = sale.GrandTotal - sale.Paid;
        if (sale.Due <= Epsilon)
        {
            sale.Due = 0;
            sale.PaymentStatus = "Paid";
            if (string.Equals((sale.Status ?? "").Trim(), "Pending", StringComparison.OrdinalIgnoreCase)) sale.Status = "Completed";
        }
        else sale.PaymentStatus = sale.Paid > 0 ? "Overdue" : "Unpaid";
    }
}

public record CreditCheck(decimal Balance, decimal? Limit, int OverdueBills, decimal OverdueAmount, bool Blocked);

public interface ICustomerAccountService
{
    /// <summary>What the customer owes now (negative = advance held).</summary>
    Task<decimal> BalanceAsync(int customerId);
    Task<CreditCheck> CreditStatusAsync(int customerId);
    /// <summary>Throws when adding newDue breaks the customer's credit rules, unless a manager overrides.</summary>
    Task<string?> EnsureCreditAsync(int customerId, decimal newDue, bool overrideRequested);
    Task<object> CreateReceiptAsync(CustomerReceiptDto dto);
    Task DeleteReceiptAsync(int id);
    /// <summary>Turns a "Credit note" refund into account credit: settles the original bill first, then other open bills.</summary>
    Task ApplyCreditNoteAsync(SalesReturn ret);
    Task RemoveCreditNoteAsync(int salesReturnId);
    /// <summary>Uses unallocated receipts and credit notes against open bills, oldest first.</summary>
    Task ApplyAdvancesAsync(int customerId);
    Task<object> StatementAsync(int customerId, string? from, string? to);
    Task<object> BalancesAsync(string? search, bool dueOnly);
}

public class CustomerAccountService : ICustomerAccountService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IPharmacySettingsService _settings;

    public CustomerAccountService(AppDbContext db, IAuditService audit, IPharmacySettingsService settings)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
    }

    private async Task<decimal> UnallocatedAsync(int customerId)
    {
        var receipts = await _db.CustomerReceipts.Where(r => r.CustomerId == customerId).SumAsync(r => (decimal?)r.Amount) ?? 0;
        var allocated = await (from p in _db.SalePayments
                               join r in _db.CustomerReceipts on p.CustomerReceiptId equals r.Id
                               where r.CustomerId == customerId
                               select (decimal?)p.PayingAmount).SumAsync() ?? 0;
        return receipts - allocated;
    }

    public async Task<decimal> BalanceAsync(int customerId)
    {
        var party = await _db.Parties.Where(p => p.Id == customerId).Select(p => new { p.OpeningBalance }).FirstOrDefaultAsync();
        if (party == null) return 0;
        var dues = await _db.Sales.Where(s => s.CustomerId == customerId).SumAsync(s => (decimal?)s.Due) ?? 0;
        var adj = await _db.CustomerLedgerEntries.Where(e => e.CustomerId == customerId).SumAsync(e => (decimal?)(e.Debit - e.Credit)) ?? 0;
        return Math.Round(party.OpeningBalance + dues + adj - await UnallocatedAsync(customerId), 2);
    }

    public async Task<CreditCheck> CreditStatusAsync(int customerId)
    {
        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Id == customerId) ?? throw new KeyNotFoundException("Customer not found");
        var balance = await BalanceAsync(customerId);
        int overdueBills = 0;
        decimal overdueAmount = 0;
        if (party.CreditDays is > 0)
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(-party.CreditDays.Value);
            var overdue = await _db.Sales.Where(s => s.CustomerId == customerId && s.Due > 0 && s.SaleDate < cutoff).Select(s => s.Due).ToListAsync();
            overdueBills = overdue.Count;
            overdueAmount = overdue.Sum();
        }
        return new CreditCheck(balance, party.CreditLimit, overdueBills, overdueAmount, party.CreditBlocked);
    }

    public async Task<string?> EnsureCreditAsync(int customerId, decimal newDue, bool overrideRequested)
    {
        if (newDue <= 0) return null;
        var c = await CreditStatusAsync(customerId);
        var problems = new List<string>();
        if (c.Blocked) problems.Add("credit is on hold for this customer");
        if (c.Limit.HasValue && c.Balance + newDue > c.Limit.Value + SalePaymentMath.Epsilon)
            problems.Add($"balance would be Rs {c.Balance + newDue:N0} against a limit of Rs {c.Limit:N0}");
        if (c.OverdueBills > 0) problems.Add($"{c.OverdueBills} bill(s) overdue, Rs {c.OverdueAmount:N0}");
        if (problems.Count == 0) return null;

        var settings = await _settings.GetAsync();
        var message = "Credit not allowed: " + string.Join("; ", problems) + ".";
        if (overrideRequested)
        {
            if (!_audit.IsAdmin) throw new BusinessRuleException(message + " Only a manager can override.");
            _audit.Log("CreditOverride", "Party", customerId, new { newDue, problems });
            return _audit.CurrentUser;
        }
        if (settings.BlockOverCreditLimit) throw new BusinessRuleException(message + " A manager can override.");
        return null;
    }

    private async Task<string> NextReferenceAsync(string prefix)
    {
        var n = await _db.CustomerReceipts.CountAsync() + 1;
        string r;
        do { r = $"{prefix}{n++:D4}"; } while (await _db.CustomerReceipts.AnyAsync(x => x.Reference == r));
        return r;
    }

    /// <summary>Spreads up to `amount` of a receipt over open bills, the preferred bill first, then oldest first.</summary>
    private async Task<decimal> AllocateAsync(CustomerReceipt receipt, decimal amount, int? preferSaleId = null)
    {
        if (amount <= SalePaymentMath.Epsilon) return 0;
        var open = await _db.Sales.Where(s => s.CustomerId == receipt.CustomerId && s.Due > 0)
            .OrderBy(s => s.Id == preferSaleId ? 0 : 1).ThenBy(s => s.SaleDate).ThenBy(s => s.Id).ToListAsync();
        var left = amount;
        foreach (var sale in open)
        {
            if (left <= SalePaymentMath.Epsilon) break;
            var take = Math.Min(sale.Due, left);
            _db.SalePayments.Add(new SalePayment
            {
                SaleId = sale.Id, Reference = receipt.Reference, ReceivedAmount = take, PayingAmount = take,
                PaymentType = receipt.PaymentMethod, Description = receipt.SalesReturnId.HasValue ? "Credit note" : "On-account receipt",
                PaymentDate = receipt.ReceiptDate, CreatedAt = DateTime.UtcNow, CustomerReceiptId = receipt.Id
            });
            sale.Paid += take;
            SalePaymentMath.Recalculate(sale);
            left -= take;
        }
        return amount - left;
    }

    public async Task<object> CreateReceiptAsync(CustomerReceiptDto dto)
    {
        if (dto.Amount <= 0) throw new BusinessRuleException("Enter an amount above zero.");
        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Id == dto.CustomerId) ?? throw new KeyNotFoundException("Customer not found");
        var user = _audit.CurrentUser;
        var till = dto.PaymentMethod == "Cash"
            ? await _db.TillSessions.Where(t => t.Status == "Open" && t.UserName == user).Select(t => (int?)t.Id).FirstOrDefaultAsync()
            : null;

        using var tx = await _db.Database.BeginTransactionAsync();
        var receipt = new CustomerReceipt
        {
            Reference = await NextReferenceAsync("RC"), CustomerId = party.Id, LocationId = dto.LocationId, TillSessionId = till,
            Amount = Math.Round(dto.Amount, 2), PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod,
            ChequeNo = dto.ChequeNo, ChequeDate = DateTime.TryParse(dto.ChequeDate, out var cd) ? cd : null,
            Notes = dto.Notes, ReceivedBy = user,
            ReceiptDate = DateTime.TryParse(dto.ReceiptDate, out var rd) ? rd : DateTime.UtcNow
        };
        _db.CustomerReceipts.Add(receipt);
        await _db.SaveChangesAsync();
        var used = await AllocateAsync(receipt, receipt.Amount);
        _audit.Log("CustomerReceipt", "Party", party.Id, new { receipt.Reference, receipt.Amount, receipt.PaymentMethod, allocated = used });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return new { receipt.Id, receipt.Reference, receipt.Amount, allocated = used, advance = receipt.Amount - used, balance = await BalanceAsync(party.Id) };
    }

    private async Task UnallocateAsync(int receiptId)
    {
        var rows = await _db.SalePayments.Where(p => p.CustomerReceiptId == receiptId).ToListAsync();
        var saleIds = rows.Select(r => r.SaleId).Distinct().ToList();
        var sales = await _db.Sales.Where(s => saleIds.Contains(s.Id)).ToListAsync();
        foreach (var r in rows)
        {
            var sale = sales.First(s => s.Id == r.SaleId);
            sale.Paid -= r.PayingAmount;
            SalePaymentMath.Recalculate(sale);
        }
        _db.SalePayments.RemoveRange(rows);
    }

    public async Task DeleteReceiptAsync(int id)
    {
        var receipt = await _db.CustomerReceipts.FirstOrDefaultAsync(r => r.Id == id) ?? throw new KeyNotFoundException("Receipt not found");
        if (receipt.SalesReturnId.HasValue) throw new BusinessRuleException("This is the credit note of a return. Delete the return instead.");
        using var tx = await _db.Database.BeginTransactionAsync();
        await UnallocateAsync(receipt.Id);
        _db.CustomerReceipts.Remove(receipt);
        _audit.Log("Deleted", "CustomerReceipt", receipt.Reference, new { receipt.CustomerId, receipt.Amount });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        await ApplyAdvancesAsync(receipt.CustomerId);
    }

    public async Task ApplyCreditNoteAsync(SalesReturn ret)
    {
        var customerId = ret.CustomerId;
        if (customerId == null && ret.SaleId.HasValue)
            customerId = await _db.Sales.Where(s => s.Id == ret.SaleId).Select(s => s.CustomerId).FirstOrDefaultAsync();
        if (customerId == null) throw new BusinessRuleException("A credit note needs a registered customer.");
        if (ret.GrandTotal <= 0) return;

        var receipt = new CustomerReceipt
        {
            Reference = await NextReferenceAsync("CN"), CustomerId = customerId.Value, Amount = Math.Round(ret.GrandTotal, 2),
            PaymentMethod = "Credit note", SalesReturnId = ret.Id, Notes = $"Return {ret.Reference}",
            ReceivedBy = _audit.CurrentUser, ReceiptDate = ret.ReturnDate
        };
        _db.CustomerReceipts.Add(receipt);
        await _db.SaveChangesAsync();
        await AllocateAsync(receipt, receipt.Amount, ret.SaleId);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveCreditNoteAsync(int salesReturnId)
    {
        var notes = await _db.CustomerReceipts.Where(r => r.SalesReturnId == salesReturnId).ToListAsync();
        foreach (var n in notes) await UnallocateAsync(n.Id);
        _db.CustomerReceipts.RemoveRange(notes);
        await _db.SaveChangesAsync();
        foreach (var c in notes.Select(n => n.CustomerId).Distinct()) await ApplyAdvancesAsync(c);
    }

    public async Task ApplyAdvancesAsync(int customerId)
    {
        var receipts = await _db.CustomerReceipts.Where(r => r.CustomerId == customerId).OrderBy(r => r.ReceiptDate).ToListAsync();
        if (receipts.Count == 0) return;
        var ids = receipts.Select(r => r.Id).ToList();
        var used = await _db.SalePayments.Where(p => p.CustomerReceiptId != null && ids.Contains(p.CustomerReceiptId.Value))
            .GroupBy(p => p.CustomerReceiptId!.Value).Select(g => new { Id = g.Key, Amt = g.Sum(p => p.PayingAmount) }).ToListAsync();
        var any = false;
        foreach (var r in receipts)
        {
            var free = r.Amount - (used.FirstOrDefault(u => u.Id == r.Id)?.Amt ?? 0);
            if (free > SalePaymentMath.Epsilon && await AllocateAsync(r, free) > 0) any = true;
        }
        if (any) await _db.SaveChangesAsync();
    }

    public async Task<object> StatementAsync(int customerId, string? from, string? to)
    {
        var party = await _db.Parties.FirstOrDefaultAsync(p => p.Id == customerId) ?? throw new KeyNotFoundException("Customer not found");
        var start = DateTime.TryParse(from, out var f) ? f.Date : DateTime.MinValue;
        var end = DateTime.TryParse(to, out var t) ? t.Date.AddDays(1) : DateTime.MaxValue;

        var sales = await _db.Sales.Where(s => s.CustomerId == customerId)
            .Select(s => new { s.Id, s.Reference, s.SaleDate, s.GrandTotal, s.Paid, s.Due }).ToListAsync();
        var saleIds = sales.Select(s => s.Id).ToList();
        var fromReceipts = await _db.SalePayments.Where(p => saleIds.Contains(p.SaleId) && p.CustomerReceiptId != null)
            .GroupBy(p => p.SaleId).Select(g => new { SaleId = g.Key, Amt = g.Sum(p => p.PayingAmount) }).ToListAsync();
        var receipts = await _db.CustomerReceipts.Where(r => r.CustomerId == customerId).ToListAsync();
        var adjustments = await _db.CustomerLedgerEntries.Where(e => e.CustomerId == customerId).ToListAsync();

        var lines = new List<(DateTime date, int order, string type, string reference, string narration, decimal debit, decimal credit)>();
        foreach (var s in sales)
        {
            lines.Add((s.SaleDate, 1, "Sale", s.Reference, "Bill", s.GrandTotal, 0));
            var direct = s.Paid - (fromReceipts.FirstOrDefault(r => r.SaleId == s.Id)?.Amt ?? 0);
            if (direct > SalePaymentMath.Epsilon) lines.Add((s.SaleDate, 2, "Payment", s.Reference, "Paid at sale", 0, direct));
        }
        foreach (var r in receipts)
            lines.Add((r.ReceiptDate, 3, r.SalesReturnId.HasValue ? "CreditNote" : "Receipt", r.Reference,
                string.Join(" · ", new[] { r.PaymentMethod, r.ChequeNo, r.Notes }.Where(x => !string.IsNullOrWhiteSpace(x))), 0, r.Amount));
        foreach (var a in adjustments)
            lines.Add((a.EntryDate, 4, a.EntryType, a.Reference ?? "", a.Narration ?? "", a.Debit, a.Credit));

        var ordered = lines.OrderBy(l => l.date).ThenBy(l => l.order).ToList();
        var opening = party.OpeningBalance + ordered.Where(l => l.date < start).Sum(l => l.debit - l.credit);
        var running = opening;
        var rows = ordered.Where(l => l.date >= start && l.date < end).Select(l =>
        {
            running += l.debit - l.credit;
            return new { date = l.date.ToString("yyyy-MM-dd"), l.type, l.reference, l.narration, l.debit, l.credit, balance = Math.Round(running, 2) };
        }).ToList();
        var status = await CreditStatusAsync(customerId);
        return new
        {
            customer = new { party.Id, party.FullName, party.Phone, party.Address, party.CustomerType, party.CreditLimit, party.CreditDays, party.Ntn, party.Strn },
            from = start == DateTime.MinValue ? null : start.ToString("yyyy-MM-dd"),
            to = end == DateTime.MaxValue ? null : end.AddDays(-1).ToString("yyyy-MM-dd"),
            openingBalance = Math.Round(opening, 2), closingBalance = Math.Round(running, 2),
            currentBalance = status.Balance, overdueBills = status.OverdueBills, overdueAmount = status.OverdueAmount,
            totalDebit = rows.Sum(r => r.debit), totalCredit = rows.Sum(r => r.credit), rows
        };
    }

    public async Task<object> BalancesAsync(string? search, bool dueOnly)
    {
        var q = _db.Parties.Where(p => p.Role == "Customer");
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.FullName.Contains(search) || (p.Phone != null && p.Phone.Contains(search)));
        var parties = await q.Select(p => new { p.Id, p.FullName, p.Phone, p.CustomerType, p.CreditLimit, p.CreditDays, p.OpeningBalance, p.CreditBlocked }).ToListAsync();
        var ids = parties.Select(p => p.Id).ToList();
        var dues = await _db.Sales.Where(s => s.CustomerId != null && ids.Contains(s.CustomerId.Value))
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new { Id = g.Key, Due = g.Sum(s => s.Due), Oldest = g.Where(s => s.Due > 0).Min(s => (DateTime?)s.SaleDate) }).ToListAsync();
        var receipts = await _db.CustomerReceipts.Where(r => ids.Contains(r.CustomerId)).GroupBy(r => r.CustomerId)
            .Select(g => new { Id = g.Key, Amt = g.Sum(r => r.Amount) }).ToListAsync();
        var allocated = await (from p in _db.SalePayments join r in _db.CustomerReceipts on p.CustomerReceiptId equals r.Id
                               where ids.Contains(r.CustomerId)
                               group p by r.CustomerId into g select new { Id = g.Key, Amt = g.Sum(x => x.PayingAmount) }).ToListAsync();
        var adj = await _db.CustomerLedgerEntries.Where(e => ids.Contains(e.CustomerId)).GroupBy(e => e.CustomerId)
            .Select(g => new { Id = g.Key, Amt = g.Sum(e => e.Debit - e.Credit) }).ToListAsync();
        var today = DateTime.UtcNow.Date;
        var rows = parties.Select(p =>
        {
            var d = dues.FirstOrDefault(x => x.Id == p.Id);
            var advance = (receipts.FirstOrDefault(x => x.Id == p.Id)?.Amt ?? 0) - (allocated.FirstOrDefault(x => x.Id == p.Id)?.Amt ?? 0);
            var balance = p.OpeningBalance + (d?.Due ?? 0) + (adj.FirstOrDefault(x => x.Id == p.Id)?.Amt ?? 0) - advance;
            var oldestDays = d?.Oldest == null ? (int?)null : (int)(today - d.Oldest.Value.Date).TotalDays;
            return new
            {
                p.Id, p.FullName, p.Phone, p.CustomerType, p.CreditLimit, p.CreditDays, p.CreditBlocked,
                balance = Math.Round(balance, 2), advance = Math.Round(advance, 2), oldestDueDays = oldestDays,
                overLimit = p.CreditLimit.HasValue && balance > p.CreditLimit.Value,
                overdue = p.CreditDays is > 0 && oldestDays > p.CreditDays
            };
        }).Where(r => !dueOnly || r.balance > SalePaymentMath.Epsilon).OrderByDescending(r => r.balance).ToList();
        return new { totalReceivable = rows.Where(r => r.balance > 0).Sum(r => r.balance), rows };
    }
}
