using System.Globalization;
using System.Text;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

public class InstallmentInvoicePdfService : IInstallmentInvoicePdfService
{
    private readonly IWebHostEnvironment _env;

    public InstallmentInvoicePdfService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public string GeneratePlanCreationInvoice(InstallmentPlan plan)
    {
        var fileName = $"plan-{plan.Id}-{Guid.NewGuid():N}.pdf";
        var dir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "invoices");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, fileName);

        var customerName = plan.Customer?.FullName ?? "N/A";
        var productName = plan.Product?.ProductName ?? "N/A";

        var pdf = new SimplePdfWriter();

        // Title
        pdf.SetFont("Helvetica-Bold", 18);
        pdf.SetColor(0.13, 0.35, 0.67); // dark blue
        pdf.AddText("INSTALLMENT PLAN INVOICE", 40, 780);

        pdf.SetFont("Helvetica", 12);
        pdf.SetColor(0, 0, 0);
        pdf.AddText($"Plan # {plan.Id}", 40, 755);

        pdf.SetFont("Helvetica", 10);
        pdf.SetColor(0.4, 0.4, 0.4);
        pdf.AddText($"Date: {plan.CreatedAt:yyyy-MM-dd}", 40, 738);

        // Divider
        pdf.AddLine(40, 728, 555, 728, 0.5, 0.8, 0.8, 0.8);

        // Customer Details (left column)
        float y = 708;
        pdf.SetColor(0, 0, 0);
        pdf.SetFont("Helvetica-Bold", 11);
        pdf.AddText("Customer Details", 40, y);
        pdf.SetFont("Helvetica", 10);
        y -= 16;
        pdf.AddText($"Name: {customerName}", 40, y); y -= 14;
        pdf.AddText($"Phone: {plan.Customer?.Phone ?? "N/A"}", 40, y); y -= 14;
        pdf.AddText($"CNIC: {plan.Customer?.Cnic ?? "N/A"}", 40, y); y -= 14;
        pdf.AddText($"Address: {plan.Customer?.Address ?? "N/A"}", 40, y);

        // Plan Summary (right column)
        y = 708;
        pdf.SetFont("Helvetica-Bold", 11);
        pdf.AddText("Plan Summary", 320, y);
        pdf.SetFont("Helvetica", 10);
        y -= 16;
        pdf.AddText($"Status: {plan.Status.ToUpper()}", 320, y); y -= 14;
        pdf.AddText($"Start Date: {plan.StartDate}", 320, y); y -= 14;
        pdf.AddText($"Tenure: {plan.Tenure} months", 320, y);

        // Divider
        pdf.AddLine(40, 640, 555, 640, 0.5, 0.8, 0.8, 0.8);

        // Financial Details Table
        y = 625;
        pdf.SetFont("Helvetica-Bold", 9);
        pdf.SetColor(1, 1, 1);
        pdf.AddRect(40, y - 4, 515, 16, 0.6, 0.6, 0.6);
        pdf.AddText("Description", 45, y);
        pdf.AddTextRight("Amount (Rs)", 550, y);

        pdf.SetColor(0, 0, 0);
        pdf.SetFont("Helvetica", 9);
        y -= 20;

        var salePrice = plan.FinanceAmount ?? plan.ProductPrice;
        var rows = new List<(string desc, string amount)>
        {
            ($"Product: {productName}", $"{salePrice:N0}")
        };

        rows.Add(("Down Payment", $"{plan.DownPayment:N0}"));
        rows.Add(("Financed Amount", $"{plan.FinancedAmount:N0}"));
        rows.Add(($"Interest Rate ({plan.InterestRate}% p.a.)", $"{plan.TotalInterest:N0}"));
        rows.Add(("Monthly EMI", $"{plan.EmiAmount:N0}"));

        foreach (var (desc, amount) in rows)
        {
            pdf.AddText(desc, 45, y);
            pdf.AddTextRight(amount, 550, y);
            pdf.AddLine(40, y - 5, 555, y - 5, 0.3, 0.9, 0.9, 0.9);
            y -= 16;
        }

        // Total row (highlighted)
        pdf.AddRect(40, y - 4, 515, 16, 0.85, 0.92, 1.0);
        pdf.SetFont("Helvetica-Bold", 10);
        pdf.AddText("Total Payable", 45, y);
        pdf.AddTextRight($"{plan.TotalPayable:N0}", 550, y);
        y -= 28;

        // Schedule Table
        pdf.SetFont("Helvetica-Bold", 11);
        pdf.SetColor(0, 0, 0);
        pdf.AddText("Repayment Schedule", 40, y);
        y -= 18;

        // Schedule header
        pdf.SetFont("Helvetica-Bold", 8);
        pdf.SetColor(1, 1, 1);
        pdf.AddRect(40, y - 4, 515, 15, 0.6, 0.6, 0.6);
        pdf.AddText("#", 45, y);
        pdf.AddText("Due Date", 70, y);
        pdf.AddTextRight("EMI", 230, y);
        pdf.AddTextRight("Principal", 310, y);
        pdf.AddTextRight("Interest", 390, y);
        pdf.AddTextRight("Balance", 470, y);
        pdf.AddText("Status", 490, y);
        y -= 16;

        pdf.SetFont("Helvetica", 8);
        pdf.SetColor(0, 0, 0);
        foreach (var s in plan.Schedule.OrderBy(s => s.InstallmentNo))
        {
            if (y < 50) break; // Don't overflow page

            pdf.AddText(s.InstallmentNo.ToString(), 48, y);
            pdf.AddText(s.DueDate, 70, y);
            pdf.AddTextRight($"{s.EmiAmount:N0}", 230, y);
            pdf.AddTextRight($"{s.Principal:N0}", 310, y);
            pdf.AddTextRight($"{s.Interest:N0}", 390, y);
            pdf.AddTextRight($"{s.Balance:N0}", 470, y);
            pdf.AddText(s.Status, 490, y);
            pdf.AddLine(40, y - 4, 555, y - 4, 0.2, 0.93, 0.93, 0.93);
            y -= 14;
        }

        // Footer
        pdf.SetFont("Helvetica", 8);
        pdf.SetColor(0.5, 0.5, 0.5);
        pdf.AddText($"Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} | Installment Plan Invoice", 170, 25);

        pdf.Save(filePath);
        return $"/uploads/invoices/{fileName}";
    }

    public string GeneratePaymentReceipt(InstallmentPlan plan, RepaymentEntry entry, decimal paidAmount)
    {
        var fileName = $"payment-{plan.Id}-{entry.InstallmentNo}-{Guid.NewGuid():N}.pdf";
        var dir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "invoices");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, fileName);

        var customerName = plan.Customer?.FullName ?? "N/A";
        var productName = plan.Product?.ProductName ?? "N/A";

        var totalPaidOnPlan = plan.Schedule
            .Where(s => s.Status == "paid" || s.Status == "partial")
            .Sum(s => (s.ActualPaidAmount ?? 0) + (s.MiscAdjustedAmount ?? 0));
        var remainingOnPlan = plan.TotalPayable - plan.DownPayment - totalPaidOnPlan;

        var pdf = new SimplePdfWriter();

        // Title
        pdf.SetFont("Helvetica-Bold", 18);
        pdf.SetColor(0.13, 0.55, 0.13); // dark green
        pdf.AddText("INSTALLMENT PAYMENT RECEIPT", 40, 780);

        pdf.SetFont("Helvetica", 12);
        pdf.SetColor(0, 0, 0);
        pdf.AddText($"Plan # {plan.Id} - Installment # {entry.InstallmentNo}", 40, 755);

        pdf.SetFont("Helvetica", 10);
        pdf.SetColor(0.4, 0.4, 0.4);
        pdf.AddText($"Date: {DateTime.UtcNow:yyyy-MM-dd}", 40, 738);

        pdf.AddLine(40, 728, 555, 728, 0.5, 0.8, 0.8, 0.8);

        // Customer (left)
        float y = 708;
        pdf.SetColor(0, 0, 0);
        pdf.SetFont("Helvetica-Bold", 11);
        pdf.AddText("Customer", 40, y);
        pdf.SetFont("Helvetica", 10);
        y -= 16;
        pdf.AddText($"Name: {customerName}", 40, y); y -= 14;
        pdf.AddText($"Phone: {plan.Customer?.Phone ?? "N/A"}", 40, y);

        // Product (right)
        y = 708;
        pdf.SetFont("Helvetica-Bold", 11);
        pdf.AddText("Product", 320, y);
        pdf.SetFont("Helvetica", 10);
        y -= 16;
        pdf.AddText($"Name: {productName}", 320, y); y -= 14;
        pdf.AddText($"Plan Status: {plan.Status.ToUpper()}", 320, y);

        pdf.AddLine(40, 660, 555, 660, 0.5, 0.8, 0.8, 0.8);

        // Payment table
        y = 645;
        pdf.SetFont("Helvetica-Bold", 9);
        pdf.SetColor(1, 1, 1);
        pdf.AddRect(40, y - 4, 515, 16, 0.6, 0.6, 0.6);
        pdf.AddText("Description", 45, y);
        pdf.AddTextRight("Amount (Rs)", 550, y);
        y -= 20;

        pdf.SetColor(0, 0, 0);
        pdf.SetFont("Helvetica", 9);

        var totalOnEntry = (entry.ActualPaidAmount ?? 0) + (entry.MiscAdjustedAmount ?? 0);

        var paymentRows = new List<(string desc, string amount)>
        {
            ($"Installment # {entry.InstallmentNo} - Due: {entry.DueDate}", $"{entry.EmiAmount:N0}"),
            ("Amount Paid Now", $"{paidAmount:N0}"),
            ("Total Paid on this Installment", $"{totalOnEntry:N0}"),
            ("Installment Status", entry.Status.ToUpper())
        };

        foreach (var (desc, amount) in paymentRows)
        {
            pdf.AddText(desc, 45, y);
            pdf.AddTextRight(amount, 550, y);
            pdf.AddLine(40, y - 5, 555, y - 5, 0.3, 0.9, 0.9, 0.9);
            y -= 16;
        }

        // Total row
        pdf.AddRect(40, y - 4, 515, 16, 0.85, 0.92, 1.0);
        pdf.SetFont("Helvetica-Bold", 10);
        pdf.AddText($"Paid Installments: {plan.PaidInstallments}/{plan.Tenure}", 45, y);
        pdf.AddTextRight($"Remaining: {remainingOnPlan:N0}", 550, y);

        // Footer
        pdf.SetFont("Helvetica", 8);
        pdf.SetColor(0.5, 0.5, 0.5);
        pdf.AddText($"Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} | Payment Receipt", 190, 25);

        pdf.Save(filePath);
        return $"/uploads/invoices/{fileName}";
    }
}

/// <summary>
/// Minimal pure-managed PDF writer. Produces valid single-page PDF documents
/// using only built-in Helvetica fonts. Zero native dependencies.
/// </summary>
internal sealed class SimplePdfWriter
{
    private readonly List<string> _stream = new();
    private string _currentFont = "/F1";
    private float _currentSize = 10;
    private string _fillColor = "0 0 0";
    private string _strokeColor = "0 0 0";

    // 14 PDF standard fonts — always available, no embedding needed
    private static readonly Dictionary<string, string> Fonts = new()
    {
        ["Helvetica"] = "Helvetica",
        ["Helvetica-Bold"] = "Helvetica-Bold",
    };

    private static string F(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public void SetFont(string name, float size)
    {
        _currentFont = name == "Helvetica-Bold" ? "/F2" : "/F1";
        _currentSize = size;
        _stream.Add($"BT {_currentFont} {F(size)} Tf ET");
    }

    public void SetColor(double r, double g, double b)
    {
        _fillColor = $"{F(r)} {F(g)} {F(b)}";
        _strokeColor = _fillColor;
    }

    public void AddText(string text, float x, float y)
    {
        var escaped = EscapePdf(text);
        _stream.Add($"BT {_fillColor} rg {_currentFont} {F(_currentSize)} Tf {F(x)} {F(y)} Td ({escaped}) Tj ET");
    }

    public void AddTextRight(string text, float rightX, float y)
    {
        // Approximate width: avg char width ≈ fontSize * 0.5
        var approxWidth = text.Length * _currentSize * 0.5f;
        AddText(text, rightX - approxWidth, y);
    }

    public void AddLine(float x1, float y1, float x2, float y2, double width, double r, double g, double b)
    {
        _stream.Add($"{F(r)} {F(g)} {F(b)} RG {F(width)} w {F(x1)} {F(y1)} m {F(x2)} {F(y2)} l S");
    }

    public void AddRect(float x, float y, float w, float h, double r, double g, double b)
    {
        _stream.Add($"{F(r)} {F(g)} {F(b)} rg {F(x)} {F(y)} {F(w)} {F(h)} re f");
    }

    public void Save(string filePath)
    {
        // Build content stream
        var contentStr = string.Join("\n", _stream);
        var contentBytes = Encoding.ASCII.GetBytes(contentStr);

        using var fs = new FileStream(filePath, FileMode.Create);
        using var w = new StreamWriter(fs, new UTF8Encoding(false));

        // Object offsets for xref
        var offsets = new List<long>();

        w.Write("%PDF-1.4\n");

        // Obj 1: Catalog
        w.Flush(); offsets.Add(fs.Position);
        w.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Obj 2: Pages
        w.Flush(); offsets.Add(fs.Position);
        w.Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // Obj 3: Page (A4 = 595.28 x 841.89)
        w.Flush(); offsets.Add(fs.Position);
        w.Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842]\n");
        w.Write("   /Contents 4 0 R\n");
        w.Write("   /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>\nendobj\n");

        // Obj 4: Content stream
        w.Flush(); offsets.Add(fs.Position);
        w.Write($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        w.Flush();
        fs.Write(contentBytes, 0, contentBytes.Length);
        w.Write("\nendstream\nendobj\n");

        // Obj 5: Font Helvetica
        w.Flush(); offsets.Add(fs.Position);
        w.Write("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        // Obj 6: Font Helvetica-Bold
        w.Flush(); offsets.Add(fs.Position);
        w.Write("6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        // xref
        w.Flush();
        var xrefOffset = fs.Position;
        w.Write($"xref\n0 {offsets.Count + 1}\n");
        w.Write("0000000000 65535 f \n");
        foreach (var off in offsets)
            w.Write($"{off:D10} 00000 n \n");

        // trailer
        w.Write($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\n");
        w.Write($"startxref\n{xrefOffset}\n%%EOF\n");
    }

    private static string EscapePdf(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", "")
            .Replace("\n", " ");
    }
}
