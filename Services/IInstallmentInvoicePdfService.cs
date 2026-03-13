using ReactPosApi.Models;

namespace ReactPosApi.Services;

public interface IInstallmentInvoicePdfService
{
    /// <summary>
    /// Generate a PDF invoice for an installment plan and save it to wwwroot/uploads/invoices.
    /// Returns the relative URL path (e.g. /uploads/invoices/{filename}.pdf).
    /// </summary>
    string GeneratePlanCreationInvoice(InstallmentPlan plan);

    /// <summary>
    /// Generate a PDF receipt for an installment payment and save it to wwwroot/uploads/invoices.
    /// Returns the relative URL path.
    /// </summary>
    string GeneratePaymentReceipt(InstallmentPlan plan, RepaymentEntry entry, decimal paidAmount);
}
