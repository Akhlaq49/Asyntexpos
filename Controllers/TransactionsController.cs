using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Every money movement in one list: payments received on sales (in), payments to suppliers (out),
/// refunds from suppliers (in) and expenses (out).
/// </summary>
[Authorize]
[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TransactionsController(AppDbContext db) => _db = db;

    /// <param name="from">yyyy-MM-dd, inclusive (optional)</param>
    /// <param name="to">yyyy-MM-dd, inclusive (optional)</param>
    [HttpGet]
    public async Task<ActionResult<TransactionsSummaryDto>> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = from?.Date ?? DateTime.MinValue;
        var end = to.HasValue ? to.Value.Date.AddDays(1) : DateTime.MaxValue;
        var list = new List<TransactionDto>();

        // sale payments (money in)
        var salePayments = await _db.SalePayments.Include(p => p.Sale)
            .Where(p => p.PaymentDate >= start && p.PaymentDate < end).ToListAsync();
        list.AddRange(salePayments.Select(p => new TransactionDto
        {
            Key = $"SP-{p.Id}", Date = p.PaymentDate, Kind = "Sale payment", Direction = "In", Amount = p.PayingAmount,
            Method = p.PaymentType, Reference = p.Reference, Document = p.Sale?.Reference, Party = p.Sale?.CustomerName,
            Notes = p.Description,
        }));

        // supplier payments (out) and refunds (in)
        // (return credits only lower what is owed - no money moves, so they are not transactions)
        var purchasePayments = await _db.PurchasePayments.Where(p => p.Date >= start && p.Date < end && p.Method != PurchasePaymentRules.ReturnCredit).ToListAsync();
        var purchaseIds = purchasePayments.Select(p => p.PurchaseId).Distinct().ToList();
        var purchases = await _db.Purchases.Where(p => purchaseIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Reference, p.SupplierName }).ToDictionaryAsync(p => p.Id);
        var poIds = purchasePayments.Where(p => p.PurchaseOrderId != null).Select(p => p.PurchaseOrderId!.Value).Distinct().ToList();
        var poNumbers = await _db.PurchaseOrders.Where(p => poIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PoNumber);
        list.AddRange(purchasePayments.Select(p =>
        {
            purchases.TryGetValue(p.PurchaseId, out var purchase);
            var refund = p.Type == PurchasePaymentType.Refund;
            return new TransactionDto
            {
                Key = $"PP-{p.Id}", Date = p.Date, Kind = refund ? "Supplier refund" : "Supplier payment", Direction = refund ? "In" : "Out",
                Amount = p.Amount, Method = p.Method, Reference = p.Reference, Document = purchase?.Reference, Party = purchase?.SupplierName,
                PurchaseOrderId = p.PurchaseOrderId,
                PurchaseOrderNo = p.PurchaseOrderId != null && poNumbers.TryGetValue(p.PurchaseOrderId.Value, out var no) ? no : null,
                Notes = p.Notes, CreatedBy = p.CreatedBy,
            };
        }));

        // money back from suppliers for goods returned (in)
        var prRefunds = await (from f in _db.PurchaseReturnRefunds
                               join r in _db.PurchaseReturns on f.PurchaseReturnId equals r.Id
                               where f.Date >= start && f.Date < end
                               select new { f, r.Reference, r.PurchaseReference, r.SupplierName, r.PurchaseOrderId }).ToListAsync();
        list.AddRange(prRefunds.Select(x => new TransactionDto
        {
            Key = $"PRR-{x.f.Id}", Date = x.f.Date, Kind = "Supplier refund", Direction = "In", Amount = x.f.Amount, Method = x.f.Method,
            Reference = x.f.Reference ?? x.Reference, Document = x.Reference, Party = x.SupplierName, PurchaseOrderId = x.PurchaseOrderId,
            Notes = string.IsNullOrWhiteSpace(x.f.Notes) ? $"Goods returned from {x.PurchaseReference}" : x.f.Notes, CreatedBy = x.f.CreatedBy,
        }));

        // refunds to customers on sales returns (out)
        var returns = await _db.SalesReturns.Where(r => r.Paid > 0 && r.ReturnDate >= start && r.ReturnDate < end).ToListAsync();
        list.AddRange(returns.Select(r => new TransactionDto
        {
            Key = $"SR-{r.Id}", Date = r.ReturnDate, Kind = "Customer refund", Direction = "Out", Amount = r.Paid,
            Reference = r.Reference, Document = r.SaleReference ?? r.Reference, Party = r.CustomerName,
            Notes = r.SaleReference != null ? $"Return of sale {r.SaleReference}" : "Sales return",
        }));

        // expenses (out)
        var expenses = await _db.Expenses.Include(e => e.ExpenseCategory)
            .Where(e => e.Date >= start && e.Date < end).ToListAsync();
        list.AddRange(expenses.Select(e => new TransactionDto
        {
            Key = $"EX-{e.Id}", Date = e.Date, Kind = "Expense", Direction = "Out", Amount = e.Amount,
            Reference = e.Reference, Document = e.Reference, Party = string.IsNullOrWhiteSpace(e.ExpenseName) ? e.ExpenseCategory?.Name : e.ExpenseName,
            Notes = e.Description,
        }));

        var ordered = list.OrderByDescending(t => t.Date).ThenByDescending(t => t.Key).ToList();
        var moneyIn = ordered.Where(t => t.Direction == "In").Sum(t => t.Amount);
        var moneyOut = ordered.Where(t => t.Direction == "Out").Sum(t => t.Amount);
        return new TransactionsSummaryDto { MoneyIn = moneyIn, MoneyOut = moneyOut, Net = moneyIn - moneyOut, Transactions = ordered };
    }
}
