using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;

namespace ReactPosApi.Services;

/// <summary>A rule the user broke (not enough stock, discount too high, ...). Returned as HTTP 400 with the message.</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}

public class BusinessRuleExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is BusinessRuleException ex)
        {
            context.Result = new BadRequestObjectResult(new { message = ex.Message });
            context.ExceptionHandled = true;
        }
        else if (context.Exception is KeyNotFoundException nf)
        {
            context.Result = new NotFoundObjectResult(new { message = nf.Message });
            context.ExceptionHandled = true;
        }
    }
}

public interface IFbrService
{
    /// <summary>Reports a sale to FBR when FBR is switched on. Never throws; the result is stored on the sale.</summary>
    Task<(bool ok, string? invoiceNumber, string? error)> SubmitAsync(int saleId);
}

/// <summary>
/// FBR POS (PRAL) real-time invoice reporting.
/// Endpoints and payload follow FBR's POS integration guide; check them against the
/// version FBR issues with the client's POS ID before going live.
/// </summary>
public class FbrService : IFbrService
{
    private const string SandboxUrl = "https://esp.fbr.gov.pk:8244/FBR/v1/api/Live/PostData";
    private const string ProductionUrl = "https://gw.fbr.gov.pk/imsp/v1/api/Live/PostData";
    /// <summary>PCT heading for medicaments, used when a product has no code of its own.</summary>
    private const string DefaultPctCode = "30049099";

    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly IPharmacySettingsService _settings;
    private readonly ILogger<FbrService> _log;

    public FbrService(AppDbContext db, HttpClient http, IPharmacySettingsService settings, ILogger<FbrService> log)
    {
        _db = db;
        _http = http;
        _settings = settings;
        _log = log;
    }

    public async Task<(bool ok, string? invoiceNumber, string? error)> SubmitAsync(int saleId)
    {
        var settings = await _settings.GetAsync();
        if (!settings.FbrEnabled) return (false, null, "FBR reporting is switched off in Pharmacy Settings.");
        if (string.IsNullOrWhiteSpace(settings.FbrPosId) || string.IsNullOrWhiteSpace(settings.FbrToken))
            return (false, null, "Enter the FBR POS ID and token in Pharmacy Settings.");

        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId);
        if (sale == null) return (false, null, "Sale not found.");
        if (!string.IsNullOrEmpty(sale.FbrInvoiceNumber)) return (true, sale.FbrInvoiceNumber, null);

        var payload = new
        {
            InvoiceNumber = "",
            POSID = long.TryParse(settings.FbrPosId, out var posId) ? posId : 0,
            USIN = sale.Reference,
            DateTime = sale.SaleDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            BuyerName = sale.CustomerName,
            BuyerPNTN = "",
            BuyerCNIC = "",
            BuyerPhoneNumber = "",
            TotalBillAmount = sale.GrandTotal,
            TotalQuantity = sale.Items.Sum(i => i.Quantity),
            TotalSaleValue = sale.GrandTotal - sale.OrderTax,
            TotalTaxCharged = sale.OrderTax,
            Discount = sale.Discount + sale.Items.Sum(i => i.Discount),
            FurtherTax = 0m,
            PaymentMode = sale.PaymentMethod switch { "Card" => 2, "Credit" => 5, _ => 1 },
            RefUSIN = (string?)null,
            InvoiceType = 1,
            Items = sale.Items.Select(i => new
            {
                ItemCode = i.ProductId.ToString(),
                ItemName = i.ProductName,
                Quantity = i.Quantity,
                PCTCode = DefaultPctCode,
                TaxRate = i.TaxPercent,
                SaleValue = i.TotalCost - i.TaxAmount,
                TotalAmount = i.TotalCost,
                TaxCharged = i.TaxAmount,
                Discount = i.Discount,
                FurtherTax = 0m,
                InvoiceType = 1,
                RefUSIN = (string?)null
            })
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, settings.FbrEnvironment == "Production" ? ProductionUrl : SandboxUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.FbrToken);
            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            string? invoiceNo = null, code = null, message = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("InvoiceNumber", out var inv)) invoiceNo = inv.GetString();
                if (root.TryGetProperty("Code", out var c)) code = c.ToString();
                if (root.TryGetProperty("Response", out var r)) message = r.GetString();
            }
            catch (JsonException) { message = body; }

            if (res.IsSuccessStatusCode && code == "100" && !string.IsNullOrWhiteSpace(invoiceNo))
            {
                sale.FbrInvoiceNumber = invoiceNo;
                sale.FbrStatus = "Sent";
                await _db.SaveChangesAsync();
                return (true, invoiceNo, null);
            }

            sale.FbrStatus = "Failed";
            await _db.SaveChangesAsync();
            return (false, null, message ?? $"FBR returned {(int)res.StatusCode}.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FBR submission failed for sale {SaleId}", saleId);
            sale.FbrStatus = "Failed";
            await _db.SaveChangesAsync();
            return (false, null, "Could not reach FBR. The bill is saved; send it again from the sales list.");
        }
    }
}
