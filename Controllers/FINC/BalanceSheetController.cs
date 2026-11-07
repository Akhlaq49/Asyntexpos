using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.Models;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

/// <summary>
/// Balance sheet as on a date, built from the system's own records:
/// cash = every money movement (Transactions), receivables / payables = unpaid sales and purchases,
/// inventory = stock on hand at its purchase cost, plus capital, loans and fixed assets (Capital &amp; Loans).
/// The profit kept in the business is what is left after liabilities and the owner's capital,
/// so Assets = Liabilities + Equity always holds.
/// </summary>
[Authorize]
[ApiController]
[Route("api/reports/balance-sheet")]
public class BalanceSheetController : ControllerBase
{
    private readonly AppDbContext _db;
    public BalanceSheetController(AppDbContext db) => _db = db;

    public record Detail(string Reference, string Name, DateTime? Date, decimal Amount, string? Info);
    public record Line(string Key, string Label, decimal Amount, string Hint, List<Detail> Details);
    public record Section(string Title, List<Line> Lines, decimal Total);

    private static readonly string[] CashMethods = { "Cash", "Not recorded", "" };

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTime? asOf)
    {
        var today = DateTime.UtcNow.Date;
        var date = (asOf ?? today).Date;
        var end = date.AddDays(1);                    // everything up to the end of that day
        var isToday = date >= today;
        var notes = new List<string>();

        /* ---------- cash: every money movement up to the date ---------- */
        var moves = await TransactionsController.LoadAsync(_db, DateTime.MinValue, end);
        decimal Net(IEnumerable<DTOs.TransactionDto> m) => m.Sum(t => t.Direction == "In" ? t.Amount : -t.Amount);
        var cashMoves = moves.Where(t => CashMethods.Contains(t.Method ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
        var bankMoves = moves.Except(cashMoves).ToList();
        var cashInHand = Math.Round(Net(cashMoves), 2);
        var bank = Math.Round(Net(bankMoves), 2);
        var bankDetails = bankMoves.GroupBy(t => t.Method ?? "Other")
            .Select(g => new Detail(g.Key, $"{g.Count()} movement(s)", null, Math.Round(Net(g), 2), null)).OrderByDescending(d => d.Amount).ToList();
        var cashDetails = new List<Detail> { new("In", "Money received in cash", null, cashMoves.Where(t => t.Direction == "In").Sum(t => t.Amount), null),
                                             new("Out", "Money paid in cash", null, -cashMoves.Where(t => t.Direction == "Out").Sum(t => t.Amount), null) };
        if (cashInHand + bank < 0)
            notes.Add("Cash is below zero: more has been paid out than received. Record the owner's starting capital under Finance > Capital & Loans.");

        /* ---------- customers: unpaid sales, advances ---------- */
        var salePaid = await _db.SalePayments.Where(p => p.PaymentDate < end).GroupBy(p => p.SaleId)
            .Select(g => new { SaleId = g.Key, Paid = g.Sum(p => p.PayingAmount) }).ToDictionaryAsync(x => x.SaleId, x => x.Paid);
        var paidSaleIds = salePaid.Keys.ToList();
        // sold by the date, still in the sale queue, or paid for by the date (an advance on an order sold later)
        var sales = await _db.Sales.Where(s => s.SaleDate < end || s.Status == ReactPosApi.Models.SaleStatus.Queued || paidSaleIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Reference, s.CustomerName, s.SaleDate, s.GrandTotal, s.Status }).ToListAsync();
        var receivables = new List<Detail>(); var customerAdvances = new List<Detail>();
        foreach (var s in sales.Where(s => !string.Equals(s.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)))
        {
            // not sold yet on the date (in the sale queue, or converted to a sale later): money received for it is a customer advance
            var queued = s.Status == ReactPosApi.Models.SaleStatus.Queued || s.SaleDate >= end;
            var due = Math.Round((queued ? 0 : s.GrandTotal) - (salePaid.TryGetValue(s.Id, out var p) ? p : 0), 2);
            if (due > 0) receivables.Add(new Detail(s.Reference, s.CustomerName, s.SaleDate, due, $"of Rs {s.GrandTotal:N2}"));
            else if (due < 0) customerAdvances.Add(new Detail(s.Reference, s.CustomerName, s.SaleDate, -due, queued ? "advance - order in the sale queue" : "paid more than the sale"));
        }
        // refunds still owed to customers on sales returns
        var salesReturnsDue = (await _db.SalesReturns.Where(r => r.ReturnDate < end && r.GrandTotal - r.Paid > 0)
                .Select(r => new { r.Reference, r.CustomerName, r.ReturnDate, Due = r.GrandTotal - r.Paid, r.SaleReference }).ToListAsync())
            .Select(r => new Detail(r.Reference, r.CustomerName, r.ReturnDate, Math.Round(r.Due, 2), r.SaleReference != null ? $"return of {r.SaleReference}" : null)).ToList();

        /* ---------- suppliers: unpaid purchases, advances, returns ---------- */
        var purchases = await _db.Purchases.Where(p => p.Date < end && p.Status == "Received" && !TransactionsController.InternalSuppliers.Contains(p.SupplierName))
            .Select(p => new { p.Id, p.Reference, p.SupplierName, p.Date, p.Total }).ToListAsync();
        var purchasePaid = (await _db.PurchasePayments.Where(p => p.Date < end).ToListAsync())
            .GroupBy(p => p.PurchaseId).ToDictionary(g => g.Key, g => PurchasePaymentRules.NetPaid(g));
        var payables = new List<Detail>(); var supplierAdvances = new List<Detail>();
        foreach (var p in purchases)
        {
            var due = Math.Round(p.Total - (purchasePaid.TryGetValue(p.Id, out var paid) ? paid : 0), 2);
            if (due > 0) payables.Add(new Detail(p.Reference, p.SupplierName, p.Date, due, $"of Rs {p.Total:N2}"));
            else if (due < 0) supplierAdvances.Add(new Detail(p.Reference, p.SupplierName, p.Date, -due, "paid more than the purchase"));
        }
        var prRefunded = await _db.PurchaseReturnRefunds.Where(f => f.Date < end).GroupBy(f => f.PurchaseReturnId)
            .Select(g => new { Id = g.Key, Amount = g.Sum(f => f.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount);
        var supplierRefundsDue = (await _db.PurchaseReturns.Where(r => r.ReturnDate < end).ToListAsync())
            .Select(r => new Detail(r.Reference, r.SupplierName, r.ReturnDate, Math.Round(r.Total - r.Credited - (prRefunded.TryGetValue(r.Id, out var f) ? f : 0), 2), $"goods returned from {r.PurchaseReference}"))
            .Where(d => d.Amount > 0).ToList();

        /* ---------- inventory at cost ---------- */
        var products = await _db.Products.Select(p => new { p.Id, p.ProductName, p.SKU, p.Quantity, p.Price }).ToListAsync();
        var qty = products.ToDictionary(p => p.Id, p => (decimal)p.Quantity);
        if (!isToday)
        {
            // take back the movements that happened after the date
            void Undo(int? productId, decimal change) { if (productId != null && qty.ContainsKey(productId.Value)) qty[productId.Value] -= change; }
            foreach (var x in await (from i in _db.PurchaseItems join p in _db.Purchases on i.PurchaseId equals p.Id
                                     where p.Date >= end && p.Status == "Received" && i.ProductId != null select new { i.ProductId, i.Quantity }).ToListAsync()) Undo(x.ProductId, x.Quantity);
            foreach (var x in await _db.StockEntries.Where(e => e.Date >= end).Select(e => new { e.ProductId, e.Quantity }).ToListAsync()) Undo(x.ProductId, x.Quantity);
            foreach (var x in await (from i in _db.SaleItems join s in _db.Sales on i.SaleId equals s.Id where s.SaleDate >= end && s.Status != ReactPosApi.Models.SaleStatus.Queued select new { i.ProductId, i.Quantity }).ToListAsync()) Undo(x.ProductId, -x.Quantity);
            foreach (var x in await (from i in _db.SalesReturnItems join r in _db.SalesReturns on i.SalesReturnId equals r.Id where r.ReturnDate >= end && i.ProductId != null select new { i.ProductId, i.Quantity }).ToListAsync()) Undo(x.ProductId, x.Quantity);
            foreach (var x in await (from i in _db.PurchaseReturnItems join r in _db.PurchaseReturns on i.PurchaseReturnId equals r.Id where r.ReturnDate >= end && i.ProductId != null select new { i.ProductId, i.Quantity }).ToListAsync()) Undo(x.ProductId, -x.Quantity);
        }
        // average purchase cost per unit up to the date (opening stock included); else the product's price
        var costs = (await (from i in _db.PurchaseItems join p in _db.Purchases on i.PurchaseId equals p.Id
                            where p.Date < end && p.Status == "Received" && i.ProductId != null && i.Quantity > 0
                            select new { ProductId = i.ProductId!.Value, i.Quantity, i.TotalCost }).ToListAsync())
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalCost) / g.Sum(x => x.Quantity));
        var inventory = new List<Detail>(); var atPrice = 0;
        foreach (var p in products)
        {
            var q = qty[p.Id];
            if (q <= 0) continue;
            var hasCost = costs.TryGetValue(p.Id, out var unit);
            if (!hasCost) { unit = p.Price; atPrice++; }
            inventory.Add(new Detail(p.SKU ?? "", p.ProductName, null, Math.Round(q * unit, 2), $"{q:0.##} x Rs {unit:N2}{(hasCost ? "" : " (price)")}"));
        }
        if (atPrice > 0) notes.Add($"{atPrice} product(s) have no purchase cost recorded and are valued at their selling price.");

        /* ---------- capital, loans, fixed assets ---------- */
        var finance = await _db.FinanceEntries.Where(f => f.Date < end).ToListAsync();
        var fixedAssets = finance.Where(f => f.Type is FinanceEntryType.AssetPurchase or FinanceEntryType.Depreciation)
            .GroupBy(f => f.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var cost = g.Where(f => f.Type == FinanceEntryType.AssetPurchase).Sum(f => f.Amount);
                var dep = g.Where(f => f.Type == FinanceEntryType.Depreciation).Sum(f => f.Amount);
                return new Detail("", g.First().Name.Trim(), g.Min(f => f.Date), cost - dep, $"cost Rs {cost:N2}, depreciation Rs {dep:N2}");
            }).Where(d => d.Amount != 0).ToList();
        var loans = finance.Where(f => f.Type is FinanceEntryType.LoanReceived or FinanceEntryType.LoanRepayment)
            .GroupBy(f => f.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new Detail("", g.First().Name.Trim(), g.Min(f => f.Date), g.Sum(f => f.Type == FinanceEntryType.LoanReceived ? f.Amount : -f.Amount),
                $"borrowed Rs {g.Where(f => f.Type == FinanceEntryType.LoanReceived).Sum(f => f.Amount):N2}")).Where(d => d.Amount != 0).ToList();
        var invested = finance.Where(f => f.Type == FinanceEntryType.OwnerInvestment).ToList();
        var drawings = finance.Where(f => f.Type == FinanceEntryType.OwnerDrawing).ToList();
        var stockBroughtIn = await _db.Purchases.Where(p => p.Date < end && TransactionsController.InternalSuppliers.Contains(p.SupplierName))
            .Select(p => new { p.Reference, p.SupplierName, p.Date, p.Total, p.Notes }).ToListAsync();

        /* ---------- assemble ---------- */
        Line L(string key, string label, IEnumerable<Detail> details, string hint) { var d = details.ToList(); return new Line(key, label, Math.Round(d.Sum(x => x.Amount), 2), hint, d.OrderByDescending(x => x.Amount).ToList()); }
        var currentAssets = new List<Line>
        {
            new("cash", "Cash in hand", cashInHand, "Cash received minus cash paid out (sales, suppliers, expenses, capital, loans)", cashDetails),
            new("bank", "Bank & digital accounts", bank, "Money moved by bank transfer, online, cheque or card", bankDetails),
            L("receivables", "Customers owe you", receivables, "Unpaid part of sales"),
            L("inventory", "Stock on hand (at cost)", inventory, "Quantity in stock x average purchase cost"),
            L("supplierRefunds", "Suppliers owe you (returns)", supplierRefundsDue, "Goods returned to suppliers, not yet refunded or deducted"),
            L("supplierAdvances", "Paid to suppliers in advance", supplierAdvances, "Paid more than the purchase total"),
        };
        var nonCurrentAssets = new List<Line> { L("fixedAssets", "Business equipment (current value)", fixedAssets, "Furniture, equipment, vehicles ... from Capital & Loans") };
        var currentLiabilities = new List<Line>
        {
            L("payables", "You owe suppliers", payables, "Unpaid part of received purchases"),
            L("customerRefunds", "Refunds owed to customers", salesReturnsDue, "Sales returns not yet refunded"),
            L("customerAdvances", "Paid by customers in advance", customerAdvances, "Customers paid more than the sale total"),
        };
        var nonCurrentLiabilities = new List<Line> { L("loans", "Loans", loans, "Borrowed minus repaid, per lender") };

        var totalAssets = Math.Round(currentAssets.Sum(l => l.Amount) + nonCurrentAssets.Sum(l => l.Amount), 2);
        var totalLiabilities = Math.Round(currentLiabilities.Sum(l => l.Amount) + nonCurrentLiabilities.Sum(l => l.Amount), 2);
        var capital = new Line("capital", "Money the owner put in", invested.Sum(f => f.Amount), "Money the owner put into the business",
            invested.Select(f => new Detail(f.Reference ?? "", f.Name, f.Date, f.Amount, f.Notes)).ToList());
        var stockIn = new Line("stockBroughtIn", "Stock brought in by the owner", stockBroughtIn.Sum(p => p.Total), "Opening stock and stock added in Manage Stock (not bought from a supplier)",
            stockBroughtIn.Select(p => new Detail(p.Reference, p.SupplierName, p.Date, p.Total, p.Notes)).ToList());
        var drawn = new Line("drawings", "Less: money the owner took out", -drawings.Sum(f => f.Amount), "Money the owner took out",
            drawings.Select(f => new Detail(f.Reference ?? "", f.Name, f.Date, -f.Amount, f.Notes)).ToList());
        var retained = Math.Round(totalAssets - totalLiabilities - (capital.Amount + stockIn.Amount + drawn.Amount), 2);
        var profit = new Line("retained", "Profit kept in the business", retained,
            "Profit earned to date and left in the business (what remains after liabilities and the owner's capital)", new());
        var equityLines = new List<Line> { capital, stockIn, drawn, profit };
        var totalEquity = Math.Round(equityLines.Sum(l => l.Amount), 2);

        return Ok(new
        {
            asOf = date,
            assets = new { current = new Section("Cash, stock & money owed to you", currentAssets, Math.Round(currentAssets.Sum(l => l.Amount), 2)),
                           nonCurrent = new Section("Equipment (long-term)", nonCurrentAssets, Math.Round(nonCurrentAssets.Sum(l => l.Amount), 2)), total = totalAssets },
            liabilities = new { current = new Section("Bills to pay soon", currentLiabilities, Math.Round(currentLiabilities.Sum(l => l.Amount), 2)),
                                nonCurrent = new Section("Loans (long-term)", nonCurrentLiabilities, Math.Round(nonCurrentLiabilities.Sum(l => l.Amount), 2)), total = totalLiabilities },
            equity = new Section("Owner's share", equityLines, totalEquity),
            totalLiabilitiesAndEquity = Math.Round(totalLiabilities + totalEquity, 2),
            balanced = Math.Abs(totalAssets - (totalLiabilities + totalEquity)) < 0.01m,
            ratios = new
            {
                currentRatio = currentLiabilities.Sum(l => l.Amount) > 0 ? Math.Round(currentAssets.Sum(l => l.Amount) / currentLiabilities.Sum(l => l.Amount), 2) : (decimal?)null,
                quickRatio = currentLiabilities.Sum(l => l.Amount) > 0 ? Math.Round((currentAssets.Sum(l => l.Amount) - currentAssets.First(l => l.Key == "inventory").Amount) / currentLiabilities.Sum(l => l.Amount), 2) : (decimal?)null,
                debtToEquity = totalEquity > 0 ? Math.Round(totalLiabilities / totalEquity, 2) : (decimal?)null,
                workingCapital = Math.Round(currentAssets.Sum(l => l.Amount) - currentLiabilities.Sum(l => l.Amount), 2),
            },
            notes,
        });
    }
}
