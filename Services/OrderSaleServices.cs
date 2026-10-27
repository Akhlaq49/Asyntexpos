using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

// ── SaleService ──
public class SaleService : ISaleService
{
    private readonly AppDbContext _db;
    public SaleService(AppDbContext db) => _db = db;

    public async Task<List<SaleDto>> GetAllAsync(string? source)
    {
        var query = _db.Sales.AsQueryable();
        if (!string.IsNullOrEmpty(source)) query = query.Where(s => s.Source == source);

        var list = await query.Include(s => s.Items).Include(s => s.Payments)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => MapToDto(s)).ToListAsync();
        await AttachQuotationReferencesAsync(list);
        return list;
    }

    /// <summary>Marks sales that came from a converted quotation</summary>
    private async Task AttachQuotationReferencesAsync(List<SaleDto> sales)
    {
        if (sales.Count == 0) return;
        var ids = sales.Select(s => s.Id).ToList();
        var links = await _db.Quotations.Where(q => q.SaleId != null && ids.Contains(q.SaleId.Value))
            .Select(q => new { SaleId = q.SaleId!.Value, q.Id, q.Reference, q.TermsContent }).ToListAsync();
        foreach (var link in links)
        {
            var sale = sales.FirstOrDefault(s => s.Id == link.SaleId);
            if (sale == null) continue;
            sale.QuotationReference = string.IsNullOrWhiteSpace(link.Reference) ? $"QT-{link.Id}" : link.Reference;
            sale.QuotationTerms = link.TermsContent;
        }
    }

    public async Task<PagedResult<SaleDto>> GetAllPagedAsync(string? source, PaginationQuery query, string? paymentStatus = null)
    {
        var q = _db.Sales.AsQueryable();
        if (!string.IsNullOrEmpty(source)) q = q.Where(s => s.Source == source);

        if (!string.IsNullOrEmpty(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(x => x.Reference.ToLower().Contains(s) || x.CustomerName.ToLower().Contains(s));
        }

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(x => x.Status == query.Status);

        if (!string.IsNullOrEmpty(paymentStatus))
            q = q.Where(x => x.PaymentStatus == paymentStatus);

        q = q.OrderByDescending(s => s.CreatedAt);

        var totalCount = await q.CountAsync();
        var entities = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .ToListAsync();

        var items = entities.Select(MapToDto).ToList();
        await AttachQuotationReferencesAsync(items);
        return new PagedResult<SaleDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<SaleDto?> GetByIdAsync(int id)
    {
        var s = await _db.Sales.Include(s => s.Items).Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == id);
        if (s == null) return null;
        var dto = MapToDto(s);
        await AttachQuotationReferencesAsync(new List<SaleDto> { dto });
        return dto;
    }

    public async Task<object> CreateAsync(CreateSaleDto dto)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var lastRef = await _db.Sales.OrderByDescending(s => s.Id).Select(s => s.Reference).FirstOrDefaultAsync();
            int nextNum = 1;
            if (!string.IsNullOrEmpty(lastRef) && lastRef.StartsWith("SL"))
            { int.TryParse(lastRef.Substring(2), out nextNum); nextNum++; }
            var reference = $"SL{nextNum:D3}";

            var sale = new Sale
            {
                Reference = reference, OrderNumber = dto.OrderNumber,
                CustomerId = dto.CustomerId, CustomerName = dto.CustomerName,
                CustomerImage = dto.CustomerImage, Biller = dto.Biller, Source = dto.Source,
                GrandTotal = dto.GrandTotal, Paid = 0, Due = dto.GrandTotal,
                OrderTax = dto.OrderTax, Discount = dto.Discount, Shipping = dto.Shipping,
                Status = dto.Status, PaymentStatus = !string.IsNullOrEmpty(dto.ExpectedDate) ? "Pending" : "Unpaid", Notes = dto.Notes,
                ExpectedDate = !string.IsNullOrEmpty(dto.ExpectedDate) ? DateTime.Parse(dto.ExpectedDate) : null,
                SaleDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
                Items = dto.Items.Select(i => new SaleItem
                {
                    ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity,
                    PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
                    TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost
                }).ToList()
            };
            _db.Sales.Add(sale);

            // ── Deduct product quantities from inventory ──
            foreach (var item in sale.Items)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null)
                    product.Quantity -= item.Quantity;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new { sale.Id, sale.Reference };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<object?> UpdateAsync(int id, CreateSaleDto dto)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            sale.CustomerId = dto.CustomerId; sale.CustomerName = dto.CustomerName;
            sale.CustomerImage = dto.CustomerImage; sale.Biller = dto.Biller;
            sale.GrandTotal = dto.GrandTotal; sale.OrderTax = dto.OrderTax;
            sale.Discount = dto.Discount; sale.Shipping = dto.Shipping;
            var queued = SaleStatus.IsQueued(sale.Status);   // waiting in the sale queue: no stock was taken out
            sale.Status = queued ? SaleStatus.Queued : dto.Status; sale.Notes = dto.Notes;
            sale.ExpectedDate = !string.IsNullOrEmpty(dto.ExpectedDate) ? DateTime.Parse(dto.ExpectedDate) : null;
            RecalculateSalePaymentState(sale);

            // ── Restore old item quantities to inventory ──
            foreach (var oldItem in sale.Items.Where(_ => !queued))
            {
                var product = await _db.Products.FindAsync(oldItem.ProductId);
                if (product != null)
                    product.Quantity += oldItem.Quantity;
            }

            _db.SaleItems.RemoveRange(sale.Items);
            sale.Items = dto.Items.Select(i => new SaleItem
            {
                SaleId = id, ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity,
                PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
                TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost
            }).ToList();

            // ── Deduct new item quantities from inventory ──
            foreach (var newItem in sale.Items.Where(_ => !queued))
            {
                var product = await _db.Products.FindAsync(newItem.ProductId);
                if (product != null)
                    product.Quantity -= newItem.Quantity;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new { sale.Id };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var sale = await _db.Sales.Include(s => s.Items).Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return false;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // ── Restore product quantities to inventory (a queued order never took any out) ──
            foreach (var item in sale.Items.Where(_ => !SaleStatus.IsQueued(sale.Status)))
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null)
                    product.Quantity += item.Quantity;
            }

            // a sale made from a quotation: the quotation is free again (it can be converted once more)
            var quotations = await _db.Quotations.Where(q => q.SaleId == sale.Id).ToListAsync();
            foreach (var q in quotations)
            {
                q.SaleId = null;
                q.SaleReference = null;
                if (q.Status == "Converted") q.Status = "Pending";
            }

            _db.Sales.Remove(sale);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<SalePaymentDto>?> GetPaymentsAsync(int saleId)
    {
        var sale = await _db.Sales.Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == saleId);
        if (sale == null) return null;
        return sale.Payments.Select(p => new SalePaymentDto
        {
            Id = p.Id, Reference = p.Reference, ReceivedAmount = p.ReceivedAmount,
            PayingAmount = p.PayingAmount, PaymentType = p.PaymentType,
            Description = p.Description, PaymentDate = p.PaymentDate.ToString("dd MMM yyyy")
        }).ToList();
    }

    /// <summary>
    /// Quotation -> sale in one transaction: creates the sale from the quotation lines,
    /// deducts the products from stock, records the payment received and marks the quotation Converted.
    /// Throws InvalidOperationException with a user-facing message when it can't be converted.
    /// </summary>
    public async Task<SaleDto?> ConvertQuotationAsync(int quotationId, ConvertQuotationToSaleDto dto, string biller)
    {
        var quotation = await _db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == quotationId);
        if (quotation == null) return null;
        if (quotation.SaleId != null)
            throw new InvalidOperationException($"This quotation was already converted to sale {quotation.SaleReference}.");
        // any version (original, R1, R2 ...) can be converted - whichever the customer accepted - but only one per quotation
        var familyRoot = quotation.RootQuotationId ?? quotation.Id;
        var convertedSibling = await _db.Quotations
            .Where(q => (q.Id == familyRoot || q.RootQuotationId == familyRoot) && q.Id != quotation.Id && q.SaleId != null)
            .Select(q => new { q.Reference, q.SaleReference }).FirstOrDefaultAsync();
        if (convertedSibling != null)
            throw new InvalidOperationException($"{convertedSibling.Reference} of this quotation was already converted to sale {convertedSibling.SaleReference}. Only one version can be converted.");
        if (quotation.Items.Count == 0)
            throw new InvalidOperationException("This quotation has no products to sell.");
        if (dto.PaidAmount < 0)
            throw new InvalidOperationException("The amount received can't be negative.");

        // every product must still exist; stock is NOT needed - the order waits in the sale queue
        // until the goods arrive and it is marked as sold
        var productIds = quotation.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var problems = new List<string>();
        foreach (var g in quotation.Items.GroupBy(i => i.ProductId))
        {
            var needed = g.Sum(i => i.Quantity);
            if (!products.ContainsKey(g.Key)) problems.Add($"{g.First().ProductName}: product no longer exists");
        }
        if (problems.Count > 0)
            throw new InvalidOperationException("Some products can't be found - " + string.Join("; ", problems) + ".");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var lastRef = await _db.Sales.OrderByDescending(s => s.Id).Select(s => s.Reference).FirstOrDefaultAsync();
            int nextNum = 1;
            if (!string.IsNullOrEmpty(lastRef) && lastRef.StartsWith("SL"))
            { int.TryParse(lastRef.Substring(2), out nextNum); nextNum++; }

            var paid = Math.Min(dto.PaidAmount, quotation.GrandTotal);
            var sale = new Sale
            {
                Reference = $"SL{nextNum:D3}",
                OrderNumber = string.IsNullOrWhiteSpace(quotation.Reference) ? $"QT-{quotation.Id}" : quotation.Reference,
                CustomerId = quotation.CustomerId, CustomerName = quotation.CustomerName, CustomerImage = quotation.CustomerImage,
                Biller = string.IsNullOrWhiteSpace(biller) ? "Admin" : biller,
                Source = "pos", // listed under Sales > POS Orders
                GrandTotal = quotation.GrandTotal, Paid = 0, Due = quotation.GrandTotal,
                OrderTax = quotation.OrderTax, Discount = quotation.Discount, Shipping = quotation.Shipping,
                InstallationCharges = quotation.InstallationCharges,
                TaxesJson = quotation.TaxesJson,
                Status = SaleStatus.Queued, PaymentStatus = "Unpaid",
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? $"Sale queue - from quotation {quotation.Reference}" : dto.Notes,
                SaleDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
                Items = quotation.Items.Select(i => new SaleItem
                {
                    ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity,
                    PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
                    TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost
                }).ToList()
            };
            _db.Sales.Add(sale);
            // no stock out here: that happens when the queued order is marked as sold

            if (paid > 0)                                  // payment received now
            {
                sale.Payments.Add(new SalePayment
                {
                    Reference = $"{sale.Reference}-P1", ReceivedAmount = dto.PaidAmount, PayingAmount = paid,
                    PaymentType = string.IsNullOrWhiteSpace(dto.PaymentType) ? "Cash" : dto.PaymentType,
                    Description = $"Advance for quotation {quotation.Reference} (sale queue)",
                    PaymentDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
                });
                sale.Paid = paid;
            }
            RecalculateSalePaymentState(sale);

            await _db.SaveChangesAsync();
            quotation.Status = "Converted";
            quotation.SaleId = sale.Id;
            quotation.SaleReference = sale.Reference;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetByIdAsync(sale.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>Sale queue -> sold: the goods have arrived; takes them out of stock and counts the sale from today</summary>
    public async Task<SaleDto?> MarkQueuedAsSoldAsync(int saleId)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId);
        if (sale == null) return null;
        if (!SaleStatus.IsQueued(sale.Status))
            throw new InvalidOperationException($"{sale.Reference} is already sold.");
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var problems = new List<string>();
        foreach (var g in sale.Items.GroupBy(i => i.ProductId))
        {
            var needed = g.Sum(i => i.Quantity);
            if (!products.TryGetValue(g.Key, out var p)) problems.Add($"{g.First().ProductName}: product no longer exists");
            else if (p.Quantity < needed) problems.Add($"{p.ProductName}: {p.Quantity} in stock, {needed} needed");
        }
        if (problems.Count > 0)
            throw new InvalidOperationException("The goods have not all arrived yet - " + string.Join("; ", problems) + ". Receive them on the purchase order first.");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var item in sale.Items)              // stock out
                products[item.ProductId].Quantity -= item.Quantity;
            sale.Status = "Pending";
            sale.SaleDate = DateTime.UtcNow;             // counted as a sale from today (dashboard, reports)
            RecalculateSalePaymentState(sale);            // fully paid -> Completed
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        return await GetByIdAsync(sale.Id);
    }

    public async Task<object?> CreatePaymentAsync(int saleId, CreateSalePaymentDto dto)
    {
        var sale = await _db.Sales.FindAsync(saleId);
        if (sale == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var payment = new SalePayment
            {
                SaleId = saleId, Reference = dto.Reference, ReceivedAmount = dto.ReceivedAmount,
                PayingAmount = dto.PayingAmount, PaymentType = dto.PaymentType,
                Description = dto.Description, PaymentDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
            };
            _db.SalePayments.Add(payment);

            sale.Paid += dto.PayingAmount;
            RecalculateSalePaymentState(sale);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new { payment.Id };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<object?> UpdatePaymentAsync(int saleId, int paymentId, CreateSalePaymentDto dto)
    {
        var sale = await _db.Sales.FindAsync(saleId);
        if (sale == null) return null;
        var payment = await _db.SalePayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.SaleId == saleId);
        if (payment == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            sale.Paid -= payment.PayingAmount;
            payment.Reference = dto.Reference; payment.ReceivedAmount = dto.ReceivedAmount;
            payment.PayingAmount = dto.PayingAmount; payment.PaymentType = dto.PaymentType;
            payment.Description = dto.Description;

            sale.Paid += dto.PayingAmount;
            RecalculateSalePaymentState(sale);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new { payment.Id };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeletePaymentAsync(int saleId, int paymentId)
    {
        var sale = await _db.Sales.FindAsync(saleId);
        if (sale == null) return false;
        var payment = await _db.SalePayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.SaleId == saleId);
        if (payment == null) return false;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            sale.Paid -= payment.PayingAmount;
            RecalculateSalePaymentState(sale);

            _db.SalePayments.Remove(payment);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<(CustomerFifoPaymentResponseDto? Result, string? Error)> ApplyCustomerFifoPaymentAsync(CustomerFifoPaymentRequestDto dto)
    {
        if (dto.Amount <= 0)
            return (null, "Amount must be greater than zero.");

        var source = string.IsNullOrWhiteSpace(dto.Source) ? "pos" : dto.Source.Trim();

        IQueryable<Sale> q = _db.Sales.Where(s => s.Source == source && s.Due > 0);

        if (dto.CustomerId.HasValue)
            q = q.Where(s => s.CustomerId == dto.CustomerId.Value);
        else
        {
            var name = (dto.CustomerName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
                return (null, "CustomerName is required when CustomerId is not set.");
            var nameLower = name.ToLowerInvariant();
            q = q.Where(s => s.CustomerId == null && s.CustomerName.ToLower() == nameLower);
        }

        var sales = await q.OrderBy(s => s.SaleDate).ThenBy(s => s.Id).ToListAsync();

        var totalDue = sales.Sum(s => s.Due);
        if (totalDue <= 0)
            return (null, "No outstanding balance for this customer.");

        if (dto.Amount > totalDue + 0.01m)
            return (null, "Amount exceeds total due for this customer.");

        var response = new CustomerFifoPaymentResponseDto();
        decimal remaining = dto.Amount;
        var baseRef = string.IsNullOrWhiteSpace(dto.Reference)
            ? $"FIFO-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : dto.Reference.Trim();

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var sale in sales)
            {
                if (remaining <= 0) break;
                if (sale.Due <= 0) continue;

                var pay = decimal.Round(Math.Min(remaining, sale.Due), 2, MidpointRounding.AwayFromZero);
                if (pay <= 0) continue;

                var payRef = $"{baseRef}-SL{sale.Id}";
                var payment = new SalePayment
                {
                    SaleId = sale.Id,
                    Reference = payRef,
                    ReceivedAmount = pay,
                    PayingAmount = pay,
                    PaymentType = dto.PaymentType,
                    Description = dto.Description ?? $"Bulk payment (FIFO) — {sale.CustomerName}",
                    PaymentDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SalePayments.Add(payment);

                sale.Paid += pay;
                RecalculateSalePaymentState(sale);

                remaining = decimal.Round(remaining - pay, 2, MidpointRounding.AwayFromZero);
                response.Allocations.Add(new FifoAllocationDto
                {
                    SaleId = sale.Id,
                    SaleReference = sale.Reference,
                    AmountApplied = pay,
                    PaymentReference = payRef
                });
            }

            response.TotalApplied = dto.Amount - remaining;
            if (response.TotalApplied <= 0)
            {
                await transaction.RollbackAsync();
                return (null, "No payment could be applied.");
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return (response, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (null, ex.Message);
        }
    }

    private const decimal SaleMoneyEpsilon = 0.01m;

    /// <summary>Sets Due and PaymentStatus from GrandTotal/Paid. When fully paid, promotes order Status from Pending to Completed.</summary>
    private static void RecalculateSalePaymentState(Sale sale)
    {
        sale.Due = sale.GrandTotal - sale.Paid;
        if (sale.Due <= SaleMoneyEpsilon)
        {
            sale.Due = 0;
            sale.PaymentStatus = "Paid";
            var st = (sale.Status ?? string.Empty).Trim();
            if (string.Equals(st, "Pending", StringComparison.OrdinalIgnoreCase))
                sale.Status = "Completed";
        }
        else if (sale.Paid > 0)
            sale.PaymentStatus = "Partial";   // part paid, the rest still due (older rows say "Overdue")
        else
            sale.PaymentStatus = "Unpaid";
    }

    /// <summary>
    /// API display: fully settled POS-style bills should show order status Completed, even if older rows were never backfilled after payment.
    /// </summary>
    private static string NormalizeOrderStatusForDto(Sale s)
    {
        var st = (s.Status ?? string.Empty).Trim();
        if (!string.Equals(st, "Pending", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrEmpty(st) ? "Pending" : st;

        var dueClear = s.Due <= SaleMoneyEpsilon;
        var paidInFull = s.GrandTotal > 0 && s.Paid >= s.GrandTotal - SaleMoneyEpsilon;
        var paymentPaid = string.Equals((s.PaymentStatus ?? string.Empty).Trim(), "Paid", StringComparison.OrdinalIgnoreCase);

        if (dueClear && (paidInFull || paymentPaid))
            return "Completed";

        return st;
    }

    private static SaleDto MapToDto(Sale s) => new()
    {
        Id = s.Id, Reference = s.Reference, OrderNumber = s.OrderNumber,
        CustomerId = s.CustomerId,
        CustomerName = s.CustomerName, CustomerImage = s.CustomerImage, Biller = s.Biller,
        GrandTotal = s.GrandTotal, Paid = s.Paid, Due = s.Due,
        OrderTax = s.OrderTax, Discount = s.Discount, Shipping = s.Shipping, InstallationCharges = s.InstallationCharges,
        Taxes = AppliedTaxes.Parse(s.TaxesJson),
        Status = NormalizeOrderStatusForDto(s), PaymentStatus = s.PaymentStatus, Notes = s.Notes, Source = s.Source,
        ExpectedDate = s.ExpectedDate?.ToString("yyyy-MM-dd"),
        SaleDate = s.SaleDate.ToString("dd MMM yyyy"),
        Items = s.Items.Select(i => new SaleItemDto
        {
            Id = i.Id, ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity,
            PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
            TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost
        }).ToList(),
        Payments = s.Payments.Select(p => new SalePaymentDto
        {
            Id = p.Id, Reference = p.Reference, ReceivedAmount = p.ReceivedAmount,
            PayingAmount = p.PayingAmount, PaymentType = p.PaymentType,
            Description = p.Description, PaymentDate = p.PaymentDate.ToString("dd MMM yyyy")
        }).ToList()
    };
}

// ── InvoiceService ──
public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _db;
    public InvoiceService(AppDbContext db) => _db = db;

    public async Task<List<InvoiceDto>> GetAllAsync()
    {
        return await _db.Invoices.Include(i => i.Items).OrderByDescending(i => i.CreatedAt)
            .Select(i => MapToDto(i)).ToListAsync();
    }

    public async Task<InvoiceDto?> GetByIdAsync(int id)
    {
        var i = await _db.Invoices.Include(inv => inv.Items).FirstOrDefaultAsync(inv => inv.Id == id);
        return i == null ? null : MapToDto(i);
    }

    public async Task<object> CreateAsync(CreateInvoiceDto dto)
    {
        var lastNo = await _db.Invoices.OrderByDescending(i => i.Id).Select(i => i.InvoiceNo).FirstOrDefaultAsync();
        int next = 1;
        if (lastNo != null && lastNo.StartsWith("INV") && int.TryParse(lastNo[3..], out var n)) next = n + 1;
        var invoiceNo = $"INV{next:D4}";

        var invoice = new Invoice
        {
            InvoiceNo = invoiceNo, CustomerId = dto.CustomerId, CustomerName = dto.CustomerName,
            CustomerImage = dto.CustomerImage, CustomerAddress = dto.CustomerAddress,
            CustomerEmail = dto.CustomerEmail, CustomerPhone = dto.CustomerPhone,
            FromName = dto.FromName, FromAddress = dto.FromAddress,
            FromEmail = dto.FromEmail, FromPhone = dto.FromPhone,
            InvoiceFor = dto.InvoiceFor, SubTotal = dto.SubTotal,
            Discount = dto.Discount, DiscountPercent = dto.DiscountPercent,
            Tax = dto.Tax, TaxPercent = dto.TaxPercent, TotalAmount = dto.TotalAmount,
            Paid = dto.Paid, AmountDue = dto.AmountDue, Status = dto.Status,
            Notes = dto.Notes, Terms = dto.Terms,
            DueDate = dto.DueDate != null ? DateTime.Parse(dto.DueDate) : DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            Items = dto.Items.Select(it => new InvoiceItem
            {
                Description = it.Description, Quantity = it.Quantity,
                Cost = it.Cost, Discount = it.Discount, Total = it.Total
            }).ToList()
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return new { invoice.Id, invoice.InvoiceNo };
    }

    public async Task<object?> UpdateAsync(int id, CreateInvoiceDto dto)
    {
        var invoice = await _db.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            invoice.CustomerId = dto.CustomerId; invoice.CustomerName = dto.CustomerName;
            invoice.CustomerImage = dto.CustomerImage; invoice.CustomerAddress = dto.CustomerAddress;
            invoice.CustomerEmail = dto.CustomerEmail; invoice.CustomerPhone = dto.CustomerPhone;
            invoice.FromName = dto.FromName; invoice.FromAddress = dto.FromAddress;
            invoice.FromEmail = dto.FromEmail; invoice.FromPhone = dto.FromPhone;
            invoice.InvoiceFor = dto.InvoiceFor; invoice.SubTotal = dto.SubTotal;
            invoice.Discount = dto.Discount; invoice.DiscountPercent = dto.DiscountPercent;
            invoice.Tax = dto.Tax; invoice.TaxPercent = dto.TaxPercent;
            invoice.TotalAmount = dto.TotalAmount; invoice.Paid = dto.Paid;
            invoice.AmountDue = dto.AmountDue; invoice.Status = dto.Status;
            invoice.Notes = dto.Notes; invoice.Terms = dto.Terms;
            if (dto.DueDate != null) invoice.DueDate = DateTime.Parse(dto.DueDate);

            _db.InvoiceItems.RemoveRange(invoice.Items);
            invoice.Items = dto.Items.Select(it => new InvoiceItem
            {
                InvoiceId = id, Description = it.Description, Quantity = it.Quantity,
                Cost = it.Cost, Discount = it.Discount, Total = it.Total
            }).ToList();

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new { invoice.Id };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var invoice = await _db.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return false;
        _db.Invoices.Remove(invoice);
        await _db.SaveChangesAsync();
        return true;
    }

    private static InvoiceDto MapToDto(Invoice i) => new()
    {
        Id = i.Id, InvoiceNo = i.InvoiceNo, CustomerId = i.CustomerId,
        CustomerName = i.CustomerName, CustomerImage = i.CustomerImage,
        CustomerAddress = i.CustomerAddress, CustomerEmail = i.CustomerEmail,
        CustomerPhone = i.CustomerPhone, FromName = i.FromName,
        FromAddress = i.FromAddress, FromEmail = i.FromEmail, FromPhone = i.FromPhone,
        InvoiceFor = i.InvoiceFor, SubTotal = i.SubTotal,
        Discount = i.Discount, DiscountPercent = i.DiscountPercent,
        Tax = i.Tax, TaxPercent = i.TaxPercent, TotalAmount = i.TotalAmount,
        Paid = i.Paid, AmountDue = i.AmountDue, Status = i.Status,
        Notes = i.Notes, Terms = i.Terms,
        DueDate = i.DueDate.ToString("dd MMM yyyy"), CreatedAt = i.CreatedAt.ToString("dd MMM yyyy"),
        Items = i.Items.Select(it => new InvoiceItemDto
        {
            Id = it.Id, Description = it.Description, Quantity = it.Quantity,
            Cost = it.Cost, Discount = it.Discount, Total = it.Total
        }).ToList()
    };
}

// ── SalesReturnService ──
public class SalesReturnService : ISalesReturnService
{
    private readonly AppDbContext _db;
    public SalesReturnService(AppDbContext db) => _db = db;

    public async Task<PagedResult<SalesReturnDto>> GetAllPagedAsync(PaginationQuery query, string? paymentStatus = null)
    {
        var q = _db.SalesReturns.AsQueryable();

        if (!string.IsNullOrEmpty(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(r => r.CustomerName.ToLower().Contains(s) || r.Reference.ToLower().Contains(s));
        }

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(r => r.Status == query.Status);

        if (!string.IsNullOrEmpty(paymentStatus))
            q = q.Where(r => r.PaymentStatus == paymentStatus);

        q = q.OrderByDescending(r => r.Id);

        var totalCount = await q.CountAsync();
        var entities = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<SalesReturnDto>
        {
            Items = entities.Select(r => new SalesReturnDto
            {
                Id = r.Id, Reference = r.Reference, CustomerId = r.CustomerId,
                CustomerName = r.CustomerName, CustomerImage = r.CustomerImage,
                ProductId = r.ProductId, ProductName = r.ProductName, ProductImage = r.ProductImage,
                OrderTax = r.OrderTax, Discount = r.Discount, Shipping = r.Shipping,
                GrandTotal = r.GrandTotal, Paid = r.Paid, Due = r.Due,
                Status = r.Status, PaymentStatus = r.PaymentStatus,
                ReturnDate = r.ReturnDate.ToString("dd MMM yyyy"), SaleId = r.SaleId, SaleReference = r.SaleReference,
                CreatedAt = r.CreatedAt.ToString("dd MMM yyyy")
            }).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<SalesReturnDto>> GetAllAsync(string? customer, string? status, string? paymentStatus, string? sort)
    {
        var q = _db.SalesReturns.AsQueryable();
        if (!string.IsNullOrEmpty(customer)) q = q.Where(r => r.CustomerName.Contains(customer));
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        if (!string.IsNullOrEmpty(paymentStatus)) q = q.Where(r => r.PaymentStatus == paymentStatus);

        q = sort switch
        {
            "asc" => q.OrderBy(r => r.ReturnDate),
            "desc" => q.OrderByDescending(r => r.ReturnDate),
            _ => q.OrderByDescending(r => r.Id)
        };

        return await q.Select(r => new SalesReturnDto
        {
            Id = r.Id, Reference = r.Reference, CustomerId = r.CustomerId,
            CustomerName = r.CustomerName, CustomerImage = r.CustomerImage,
            ProductId = r.ProductId, ProductName = r.ProductName, ProductImage = r.ProductImage,
            OrderTax = r.OrderTax, Discount = r.Discount, Shipping = r.Shipping,
            GrandTotal = r.GrandTotal, Paid = r.Paid, Due = r.Due,
            Status = r.Status, PaymentStatus = r.PaymentStatus,
            ReturnDate = r.ReturnDate.ToString("dd MMM yyyy"), SaleId = r.SaleId, SaleReference = r.SaleReference,
            CreatedAt = r.CreatedAt.ToString("dd MMM yyyy")
        }).ToListAsync();
    }

    public async Task<SalesReturnDto?> GetByIdAsync(int id)
    {
        var r = await _db.SalesReturns.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return null;

        return new SalesReturnDto
        {
            Id = r.Id, Reference = r.Reference, CustomerId = r.CustomerId,
            CustomerName = r.CustomerName, CustomerImage = r.CustomerImage,
            ProductId = r.ProductId, ProductName = r.ProductName, ProductImage = r.ProductImage,
            OrderTax = r.OrderTax, Discount = r.Discount, Shipping = r.Shipping,
            GrandTotal = r.GrandTotal, Paid = r.Paid, Due = r.Due,
            Status = r.Status, PaymentStatus = r.PaymentStatus,
            ReturnDate = r.ReturnDate.ToString("dd MMM yyyy"), SaleId = r.SaleId, SaleReference = r.SaleReference,
            CreatedAt = r.CreatedAt.ToString("dd MMM yyyy"),
            Items = r.Items.Select(i => new SalesReturnItemDto
            {
                Id = i.Id, ProductName = i.ProductName, NetUnitPrice = i.NetUnitPrice,
                Stock = i.Stock, Quantity = i.Quantity, Discount = i.Discount,
                TaxPercent = i.TaxPercent, Subtotal = i.Subtotal
            }).ToList()
        };
    }

    public async Task<object> CreateAsync(CreateSalesReturnDto dto)
    {
        var entity = new SalesReturn
        {
            Reference = dto.Reference ?? string.Empty,
            CustomerId = dto.CustomerId, CustomerName = dto.CustomerName,
            CustomerImage = dto.CustomerImage, ProductId = dto.ProductId,
            ProductName = dto.ProductName, ProductImage = dto.ProductImage,
            OrderTax = dto.OrderTax, Discount = dto.Discount, Shipping = dto.Shipping,
            GrandTotal = dto.GrandTotal, Paid = dto.Paid, Due = dto.Due,
            Status = dto.Status, PaymentStatus = dto.PaymentStatus,
            ReturnDate = dto.ReturnDate != null ? DateTime.Parse(dto.ReturnDate) : DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Items = dto.Items.Select(i => new SalesReturnItem
            {
                ProductName = i.ProductName, NetUnitPrice = i.NetUnitPrice,
                Stock = i.Stock, Quantity = i.Quantity, Discount = i.Discount,
                TaxPercent = i.TaxPercent, Subtotal = i.Subtotal
            }).ToList()
        };
        _db.SalesReturns.Add(entity);
        await _db.SaveChangesAsync();
        return new { entity.Id };
    }

    public async Task<object?> UpdateAsync(int id, CreateSalesReturnDto dto)
    {
        var entity = await _db.SalesReturns.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return null;
        if (entity.SaleId != null)
            throw new InvalidOperationException($"{entity.Reference} is a return of sale {entity.SaleReference} and can't be edited. Delete it and return the sale again.");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            entity.CustomerId = dto.CustomerId; entity.CustomerName = dto.CustomerName;
            entity.CustomerImage = dto.CustomerImage; entity.ProductId = dto.ProductId;
            entity.ProductName = dto.ProductName; entity.ProductImage = dto.ProductImage;
            entity.OrderTax = dto.OrderTax; entity.Discount = dto.Discount;
            entity.Shipping = dto.Shipping; entity.GrandTotal = dto.GrandTotal;
            entity.Paid = dto.Paid; entity.Due = dto.Due;
            entity.Status = dto.Status; entity.PaymentStatus = dto.PaymentStatus;
            if (dto.ReturnDate != null) entity.ReturnDate = DateTime.Parse(dto.ReturnDate);

            _db.SalesReturnItems.RemoveRange(entity.Items);
            entity.Items = dto.Items.Select(i => new SalesReturnItem
            {
                SalesReturnId = id, ProductName = i.ProductName, NetUnitPrice = i.NetUnitPrice,
                Stock = i.Stock, Quantity = i.Quantity, Discount = i.Discount,
                TaxPercent = i.TaxPercent, Subtotal = i.Subtotal
            }).ToList();

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new { entity.Id };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.SalesReturns.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return false;
        // a return of a sale put the goods back into stock: deleting it takes them out again
        foreach (var item in entity.Items.Where(i => i.ProductId != null))
        {
            var product = await _db.Products.FindAsync(item.ProductId!.Value);
            if (product != null) product.Quantity = Math.Max(0, product.Quantity - item.Quantity);
        }
        _db.SalesReturns.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}

// ── QuotationService ──
public class QuotationService : IQuotationService
{
    private readonly AppDbContext _db;
    public QuotationService(AppDbContext db) => _db = db;

    public async Task<PagedResult<QuotationDto>> GetAllPagedAsync(PaginationQuery query)
    {
        var q = _db.Quotations.AsQueryable();

        if (!string.IsNullOrEmpty(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(x => x.CustomerName.ToLower().Contains(s) ||
                             x.ProductName.ToLower().Contains(s) ||
                             x.Reference.ToLower().Contains(s));
        }

        if (!string.IsNullOrEmpty(query.Status))
            q = q.Where(x => x.Status == query.Status);

        q = q.OrderByDescending(x => x.Id);

        var totalCount = await q.CountAsync();
        var entities = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(x => x.Items)
            .ToListAsync();

        return new PagedResult<QuotationDto>
        {
            Items = entities.Select(x => new QuotationDto
            {
                Id = x.Id, Reference = x.Reference, CustomerId = x.CustomerId,
                CustomerName = x.CustomerName, CustomerImage = x.CustomerImage,
                ProductId = x.ProductId, ProductName = x.ProductName, ProductImage = x.ProductImage,
                OrderTax = x.OrderTax, Discount = x.Discount, Shipping = x.Shipping, InstallationCharges = x.InstallationCharges,
                CreatedById = x.CreatedById, CreatedByName = x.CreatedByName, ApprovalStatus = x.ApprovalStatus, ApprovalSentTo = x.ApprovalSentTo, ApprovalBy = x.ApprovalBy, ApprovalAt = x.ApprovalAt, ApprovalNote = x.ApprovalNote,
                GrandTotal = x.GrandTotal, Status = x.Status, Description = x.Description, SaleId = x.SaleId, SaleReference = x.SaleReference, Taxes = AppliedTaxes.Parse(x.TaxesJson), RootQuotationId = x.RootQuotationId, RevisionNo = x.RevisionNo, SupersededById = x.SupersededById, ItemCount = x.Items.Count, ProductNames = x.Items.OrderBy(i => i.Id).Select(i => i.ProductName).ToList(),
                QuotationDate = x.QuotationDate.ToString("dd MMM yyyy"),
                CreatedAt = x.CreatedAt.ToString("dd MMM yyyy")
            }).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<List<QuotationDto>> GetAllAsync(string? product, string? customer, string? status, string? sort)
    {
        var q = _db.Quotations.AsQueryable();
        if (!string.IsNullOrEmpty(product)) q = q.Where(x => x.ProductName.Contains(product));
        if (!string.IsNullOrEmpty(customer)) q = q.Where(x => x.CustomerName.Contains(customer));
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);

        q = sort switch
        {
            "asc" => q.OrderBy(x => x.GrandTotal),
            "desc" => q.OrderByDescending(x => x.GrandTotal),
            _ => q.OrderByDescending(x => x.Id)
        };

        return await q.Select(x => new QuotationDto
        {
            Id = x.Id, Reference = x.Reference, CustomerId = x.CustomerId,
            CustomerName = x.CustomerName, CustomerImage = x.CustomerImage,
            ProductId = x.ProductId, ProductName = x.ProductName, ProductImage = x.ProductImage,
            OrderTax = x.OrderTax, Discount = x.Discount, Shipping = x.Shipping, InstallationCharges = x.InstallationCharges,
                CreatedById = x.CreatedById, CreatedByName = x.CreatedByName, ApprovalStatus = x.ApprovalStatus, ApprovalSentTo = x.ApprovalSentTo, ApprovalBy = x.ApprovalBy, ApprovalAt = x.ApprovalAt, ApprovalNote = x.ApprovalNote,
            GrandTotal = x.GrandTotal, Status = x.Status, Description = x.Description, SaleId = x.SaleId, SaleReference = x.SaleReference, Taxes = AppliedTaxes.Parse(x.TaxesJson), RootQuotationId = x.RootQuotationId, RevisionNo = x.RevisionNo, SupersededById = x.SupersededById, ItemCount = x.Items.Count, ProductNames = x.Items.OrderBy(i => i.Id).Select(i => i.ProductName).ToList(),
            QuotationDate = x.QuotationDate.ToString("dd MMM yyyy"),
            CreatedAt = x.CreatedAt.ToString("dd MMM yyyy")
        }).ToListAsync();
    }

    public async Task<QuotationDto?> GetByIdAsync(int id)
    {
        var x = await _db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
        if (x == null) return null;

        return new QuotationDto
        {
            Id = x.Id, Reference = x.Reference, CustomerId = x.CustomerId,
            CustomerName = x.CustomerName, CustomerImage = x.CustomerImage,
            ProductId = x.ProductId, ProductName = x.ProductName, ProductImage = x.ProductImage,
            OrderTax = x.OrderTax, Discount = x.Discount, Shipping = x.Shipping, InstallationCharges = x.InstallationCharges,
                CreatedById = x.CreatedById, CreatedByName = x.CreatedByName, ApprovalStatus = x.ApprovalStatus, ApprovalSentTo = x.ApprovalSentTo, ApprovalBy = x.ApprovalBy, ApprovalAt = x.ApprovalAt, ApprovalNote = x.ApprovalNote,
            GrandTotal = x.GrandTotal, Status = x.Status, Description = x.Description, SaleId = x.SaleId, SaleReference = x.SaleReference, Taxes = AppliedTaxes.Parse(x.TaxesJson), RootQuotationId = x.RootQuotationId, RevisionNo = x.RevisionNo, SupersededById = x.SupersededById, ItemCount = x.Items.Count, ProductNames = x.Items.OrderBy(i => i.Id).Select(i => i.ProductName).ToList(),
            QuotationDate = x.QuotationDate.ToString("dd MMM yyyy"),
            CreatedAt = x.CreatedAt.ToString("dd MMM yyyy"),
            TermsContent = x.TermsContent,
            Items = x.Items.Select(i => new QuotationItemDto
            {
                Id = i.Id, ProductId = i.ProductId, ProductName = i.ProductName,
                Quantity = i.Quantity, PurchasePrice = i.PurchasePrice, Discount = i.Discount,
                TaxPercent = i.TaxPercent, TaxAmount = i.TaxAmount,
                UnitCost = i.UnitCost, TotalCost = i.TotalCost
            }).ToList()
        };
    }

    public async Task<object> CreateAsync(CreateQuotationDto dto)
    {
        ApplyTaxes(dto);
        var entity = BuildQuotation(dto);
        // every quotation has a unique reference: the typed one (if not used yet) or the next QT-0001, QT-0002 ...
        var typed = dto.Reference?.Trim();
        if (string.IsNullOrEmpty(typed)) entity.Reference = await NextReferenceAsync();
        else if (await ReferenceTakenAsync(typed, null))
            throw new InvalidOperationException($"Reference {typed} is already used by another quotation. Leave it empty to get the next number automatically.");
        else entity.Reference = typed;
        _db.Quotations.Add(entity);
        await _db.SaveChangesAsync();
        return new { entity.Id };
    }

    /// <summary>
    /// New version of a quotation: QT-005 -> QT-005_R1 -> QT-005_R2 (numbered per original).
    /// Any version can be revised (e.g. R3 from the original); every version is kept unchanged and stays convertible.
    /// Not allowed once a version of the quotation has been converted to a sale.
    /// Terms: null keeps the previous version's terms, "" = standard terms, text = new terms.
    /// </summary>
    public async Task<QuotationDto?> ReviseAsync(int id, CreateQuotationDto dto)
    {
        var current = await _db.Quotations.FirstOrDefaultAsync(q => q.Id == id);
        if (current == null) return null;
        if (current.SaleId != null)
            throw new InvalidOperationException($"{current.Reference} was converted to sale {current.SaleReference} and can't be revised.");
        if (dto.Items.Count == 0)
            throw new InvalidOperationException("Add at least one product to the revision.");

        var rootId = current.RootQuotationId ?? current.Id;
        var root = rootId == current.Id ? current : await _db.Quotations.FirstAsync(q => q.Id == rootId);
        var converted = await _db.Quotations.Where(q => (q.Id == rootId || q.RootQuotationId == rootId) && q.SaleId != null)
            .Select(q => new { q.Reference, q.SaleReference }).FirstOrDefaultAsync();
        if (converted != null)
            throw new InvalidOperationException($"{converted.Reference} of this quotation was already converted to sale {converted.SaleReference}, so it can't be revised any more.");
        // the version that is currently the newest gets marked as replaced (it may not be the one being revised)
        var newest = await _db.Quotations.FirstOrDefaultAsync(q => (q.Id == rootId || q.RootQuotationId == rootId) && q.SupersededById == null);
        var revisionNo = await _db.Quotations.Where(q => q.Id == rootId || q.RootQuotationId == rootId).MaxAsync(q => q.RevisionNo) + 1;
        var suffix = $"_R{revisionNo}";
        var baseRef = string.IsNullOrWhiteSpace(root.Reference) ? $"QT-{root.Id}" : root.Reference.Trim();
        if (baseRef.Length + suffix.Length > 50) baseRef = baseRef[..(50 - suffix.Length)];

        ApplyTaxes(dto);
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var revision = BuildQuotation(dto);
            revision.Reference = baseRef + suffix;
            revision.RootQuotationId = rootId;
            revision.RevisionNo = revisionNo;
            if (dto.TermsContent == null) revision.TermsContent = current.TermsContent;   // keep the previous version's terms
            if (dto.Taxes == null) revision.TaxesJson = current.TaxesJson;
            if (current.CreatedById != null) { revision.CreatedById = current.CreatedById; revision.CreatedByName = current.CreatedByName; }
            _db.Quotations.Add(revision);
            await _db.SaveChangesAsync();

            if (newest != null) newest.SupersededById = revision.Id;   // older versions stay exactly as they were
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetByIdAsync(revision.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>Every version of the quotation family (original first)</summary>
    public async Task<List<QuotationDto>> GetRevisionsAsync(int id)
    {
        var q = await _db.Quotations.FirstOrDefaultAsync(x => x.Id == id);
        if (q == null) return new();
        var rootId = q.RootQuotationId ?? q.Id;
        return await _db.Quotations.Where(x => x.Id == rootId || x.RootQuotationId == rootId)
            .OrderBy(x => x.RevisionNo)
            .Select(x => new QuotationDto
            {
                Id = x.Id, Reference = x.Reference, CustomerId = x.CustomerId, CustomerName = x.CustomerName,
                OrderTax = x.OrderTax, Discount = x.Discount, Shipping = x.Shipping, InstallationCharges = x.InstallationCharges,
                CreatedById = x.CreatedById, CreatedByName = x.CreatedByName, ApprovalStatus = x.ApprovalStatus, ApprovalSentTo = x.ApprovalSentTo, ApprovalBy = x.ApprovalBy, ApprovalAt = x.ApprovalAt, ApprovalNote = x.ApprovalNote, GrandTotal = x.GrandTotal,
                Status = x.Status, Description = x.Description, SaleId = x.SaleId, SaleReference = x.SaleReference,
                RootQuotationId = x.RootQuotationId, RevisionNo = x.RevisionNo, SupersededById = x.SupersededById,
                QuotationDate = x.QuotationDate.ToString("dd MMM yyyy"),
                CreatedAt = x.CreatedAt.ToString("dd MMM yyyy, HH:mm")
            }).ToListAsync();
    }

    /// <summary>Next automatic reference: QT-0001, QT-0002 ... (one more than the highest QT-number used)</summary>
    private async Task<string> NextReferenceAsync()
    {
        var refs = await _db.Quotations.Where(q => q.Reference.StartsWith("QT-")).Select(q => q.Reference).ToListAsync();
        var max = refs.Select(r => System.Text.RegularExpressions.Regex.Match(r, @"^QT-(\d+)$"))
            .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        return $"QT-{max + 1:D4}";
    }

    private Task<bool> ReferenceTakenAsync(string reference, int? exceptId) =>
        _db.Quotations.AnyAsync(q => q.Reference == reference && (exceptId == null || q.Id != exceptId));

    private static Quotation BuildQuotation(CreateQuotationDto dto)
    {
        return new Quotation
        {
            TaxesJson = dto.Taxes != null ? AppliedTaxes.Serialize(dto.Taxes) : null,
            TermsContent = string.IsNullOrWhiteSpace(dto.TermsContent) ? null : dto.TermsContent.Trim(),
            Reference = dto.Reference ?? string.Empty,
            CustomerId = dto.CustomerId, CustomerName = dto.CustomerName,
            CustomerImage = dto.CustomerImage, ProductId = dto.ProductId,
            ProductName = dto.ProductName, ProductImage = dto.ProductImage,
            OrderTax = dto.OrderTax, Discount = dto.Discount, Shipping = dto.Shipping,
            InstallationCharges = Math.Max(0, dto.InstallationCharges),
            CreatedById = dto.CreatedById, CreatedByName = dto.CreatedByName,
            GrandTotal = dto.GrandTotal, Status = dto.Status, Description = dto.Description,
            QuotationDate = dto.QuotationDate != null ? DateTime.Parse(dto.QuotationDate) : DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Items = dto.Items.Select(i => new QuotationItem
            {
                ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity,
                PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
                TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost
            }).ToList()
        };
    }

    public async Task<object?> UpdateAsync(int id, CreateQuotationDto dto)
    {
        var entity = await _db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
        if (entity == null) return null;
        if (entity.SaleId != null)
            throw new InvalidOperationException($"This quotation was converted to sale {entity.SaleReference} and can't be edited.");
        if (entity.SupersededById != null)
            throw new InvalidOperationException($"{entity.Reference} is an older version and is kept unchanged. Use Revise to make a new version from it.");
        var editRoot = entity.RootQuotationId ?? entity.Id;
        var chosen = await _db.Quotations.Where(q => (q.Id == editRoot || q.RootQuotationId == editRoot) && q.SaleId != null)
            .Select(q => new { q.Reference, q.SaleReference }).FirstOrDefaultAsync();
        if (chosen != null)
            throw new InvalidOperationException($"{chosen.Reference} of this quotation was already converted to sale {chosen.SaleReference}; the other versions are kept unchanged.");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            ApplyTaxes(dto);
            if (dto.Taxes != null) entity.TaxesJson = AppliedTaxes.Serialize(dto.Taxes);
            if (dto.TermsContent != null) entity.TermsContent = string.IsNullOrWhiteSpace(dto.TermsContent) ? null : dto.TermsContent.Trim();
            var newRef = dto.Reference?.Trim();   // empty keeps the current reference
            if (!string.IsNullOrEmpty(newRef) && newRef != entity.Reference)
            {
                if (await ReferenceTakenAsync(newRef, entity.Id))
                    throw new InvalidOperationException($"Reference {newRef} is already used by another quotation.");
                entity.Reference = newRef;
            }
            entity.CustomerId = dto.CustomerId; entity.CustomerName = dto.CustomerName;
            entity.CustomerImage = dto.CustomerImage; entity.ProductId = dto.ProductId;
            entity.ProductName = dto.ProductName; entity.ProductImage = dto.ProductImage;
            entity.OrderTax = dto.OrderTax; entity.Discount = dto.Discount;
            entity.Shipping = dto.Shipping; entity.GrandTotal = dto.GrandTotal;
            entity.InstallationCharges = Math.Max(0, dto.InstallationCharges);
            entity.Status = dto.Status; entity.Description = dto.Description;
            // the quotation changed: an earlier approval / pending request no longer applies - it has to be sent again
            if (entity.ApprovalStatus != null)
            {
                entity.ApprovalStatus = null; entity.ApprovalSentTo = null; entity.ApprovalBy = null; entity.ApprovalAt = null; entity.ApprovalNote = null;
                foreach (var a in await _db.QuotationApprovals.Where(a => a.QuotationId == entity.Id && a.Status == QuotationApprovalStatus.Pending).ToListAsync())
                { a.Status = QuotationApprovalStatus.Cancelled; a.DecidedAt = DateTime.UtcNow; a.DecisionNote = "The quotation was edited after it was sent."; }
            }
            if (dto.QuotationDate != null) entity.QuotationDate = DateTime.Parse(dto.QuotationDate);

            _db.QuotationItems.RemoveRange(entity.Items);
            entity.Items = dto.Items.Select(i => new QuotationItem
            {
                QuotationId = id, ProductId = i.ProductId, ProductName = i.ProductName,
                Quantity = i.Quantity, PurchasePrice = i.PurchasePrice, Discount = i.Discount,
                TaxPercent = i.TaxPercent, TaxAmount = i.TaxAmount,
                UnitCost = i.UnitCost, TotalCost = i.TotalCost
            }).ToList();

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new { entity.Id };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>Taxes sent with the quotation: amounts on (items total - discount); OrderTax = their total, GrandTotal recalculated</summary>
    private static void ApplyTaxes(CreateQuotationDto dto)
    {
        if (dto.Taxes == null) return;
        var itemsTotal = dto.Items.Sum(i => i.TotalCost);
        dto.Taxes = AppliedTaxes.Calculate(dto.Taxes, itemsTotal - dto.Discount);
        dto.OrderTax = dto.Taxes.Sum(t => t.Amount);
        dto.GrandTotal = Math.Max(0, Math.Round(itemsTotal - dto.Discount + dto.OrderTax + dto.Shipping + Math.Max(0, dto.InstallationCharges), 2));
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
        if (entity == null) return false;
        if (entity.SupersededById != null)
            throw new InvalidOperationException($"{entity.Reference} is an older version kept as history and can't be deleted.");
        // deleting the latest revision makes the previous version the current one again
        var previous = await _db.Quotations.FirstOrDefaultAsync(q => q.SupersededById == entity.Id);
        if (previous != null) previous.SupersededById = null;
        foreach (var a in await _db.QuotationApprovals.Where(a => a.QuotationId == entity.Id && a.Status == QuotationApprovalStatus.Pending).ToListAsync())
        { a.Status = QuotationApprovalStatus.Cancelled; a.DecidedAt = DateTime.UtcNow; a.DecisionNote = "The quotation was deleted."; }
        _db.Quotations.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}

