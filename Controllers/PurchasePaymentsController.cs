using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Payments to the supplier and refunds from the supplier against one purchase (e.g. a purchase order receipt).
/// Records are kept for good: a mistake is corrected with an entry in the other direction.
/// </summary>
[Authorize]
[ApiController]
[Route("api/purchases/{purchaseId:int}/payments")]
public class PurchasePaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PurchasePaymentsController(AppDbContext db) => _db = db;

    public record PurchasePaymentsResult(int PurchaseId, string Reference, decimal Total, decimal Paid, decimal Due, string PaymentStatus, List<PurchasePaymentDto> Payments);

    [HttpGet]
    public async Task<ActionResult<PurchasePaymentsResult>> Get(int purchaseId)
    {
        var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId);
        if (purchase == null) return NotFound();
        return await ResultAsync(purchase);
    }

    [HttpPost]
    public async Task<ActionResult<PurchasePaymentsResult>> Add(int purchaseId, [FromBody] SavePurchasePaymentDto dto)
    {
        var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId);
        if (purchase == null) return NotFound();

        var type = string.Equals(dto.Type, PurchasePaymentType.Refund, StringComparison.OrdinalIgnoreCase) ? PurchasePaymentType.Refund
                 : string.Equals(dto.Type, PurchasePaymentType.Payment, StringComparison.OrdinalIgnoreCase) ? PurchasePaymentType.Payment
                 : null;
        if (type == null) return BadRequest(new { message = "Choose Payment or Refund." });
        var amount = Math.Round(dto.Amount, 2);
        if (amount <= 0) return BadRequest(new { message = "Enter an amount greater than 0." });

        var records = await _db.PurchasePayments.Where(p => p.PurchaseId == purchaseId).ToListAsync();
        var netPaid = PurchasePaymentRules.NetPaid(records);
        if (type == PurchasePaymentType.Refund && amount > netPaid)
            return BadRequest(new { message = $"Only Rs {netPaid:N2} has been paid on {purchase.Reference}, so at most that much can come back." });

        var no = records.Count + 1;
        var record = new PurchasePayment
        {
            PurchaseId = purchase.Id,
            PurchaseOrderId = purchase.PurchaseOrderId,
            Type = type,
            Amount = amount,
            Method = string.IsNullOrWhiteSpace(dto.Method) ? "Cash" : dto.Method.Trim()[..Math.Min(30, dto.Method.Trim().Length)],
            Reference = string.IsNullOrWhiteSpace(dto.Reference)
                ? $"{purchase.Reference}-{(type == PurchasePaymentType.Refund ? "RF" : "P")}{no}"
                : dto.Reference.Trim()[..Math.Min(60, dto.Reference.Trim().Length)],
            Date = dto.Date ?? DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()[..Math.Min(500, dto.Notes.Trim().Length)],
            CreatedBy = User.FindFirstValue(ClaimTypes.Name),
            CreatedAt = DateTime.UtcNow,
        };
        _db.PurchasePayments.Add(record);
        records.Add(record);

        purchase.Paid = PurchasePaymentRules.NetPaid(records);
        purchase.PaymentStatus = PurchasePaymentRules.Status(purchase.Paid, purchase.Total);
        purchase.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await ResultAsync(purchase);
    }

    private async Task<PurchasePaymentsResult> ResultAsync(Purchase purchase)
    {
        var payments = await _db.PurchasePayments.Where(p => p.PurchaseId == purchase.Id)
            .OrderBy(p => p.Date).ThenBy(p => p.Id).ToListAsync();
        return new PurchasePaymentsResult(purchase.Id, purchase.Reference, purchase.Total, purchase.Paid,
            purchase.Total - purchase.Paid, purchase.PaymentStatus, payments.Select(PurchasePaymentRules.ToDto).ToList());
    }
}
