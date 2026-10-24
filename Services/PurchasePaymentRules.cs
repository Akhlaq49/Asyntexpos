using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

/// <summary>How a purchase's Paid amount and payment status follow from its payment / refund records</summary>
public static class PurchasePaymentRules
{
    public static readonly string[] Methods = { "Cash", "Online", "Bank Transfer", "Cheque", "Card", "Other" };

    /// <summary>Payments - refunds</summary>
    public static decimal NetPaid(IEnumerable<PurchasePayment> records) =>
        records.Sum(r => r.Type == PurchasePaymentType.Refund ? -r.Amount : r.Amount);

    /// <summary>Unpaid / Partial / Paid / Overpaid (paid more than the receipt total, e.g. an extra charge or advance)</summary>
    public static string Status(decimal paid, decimal total)
    {
        if (paid <= 0) return "Unpaid";
        if (paid > total) return "Overpaid";
        return paid >= total ? "Paid" : "Partial";
    }

    public static PurchasePaymentDto ToDto(PurchasePayment p) => new()
    {
        Id = p.Id, PurchaseId = p.PurchaseId, PurchaseOrderId = p.PurchaseOrderId, Type = p.Type, Amount = p.Amount,
        Method = p.Method, Reference = p.Reference, Date = p.Date, Notes = p.Notes, CreatedBy = p.CreatedBy, CreatedAt = p.CreatedAt,
    };
}
