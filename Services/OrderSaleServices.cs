using Microsoft.EntityFrameworkCore;
using ReactPosApi.Data;
using ReactPosApi.DTOs;
using ReactPosApi.Models;

namespace ReactPosApi.Services;

// ── SaleService ──
public class SaleService : ISaleService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IPharmacySettingsService _settings;
    private readonly IAuditService _audit;
    private readonly IFbrService _fbr;
    private readonly ILocationService _locations;
    private readonly ICustomerAccountService _accounts;
    private readonly IServiceProvider _services;

    public SaleService(AppDbContext db, IInventoryService inventory, IPharmacySettingsService settings,
        IAuditService audit, IFbrService fbr, ILocationService locations, ICustomerAccountService accounts, IServiceProvider services)
    {
        _db = db;
        _inventory = inventory;
        _settings = settings;
        _audit = audit;
        _fbr = fbr;
        _locations = locations;
        _accounts = accounts;
        _services = services;
    }

    public async Task<List<SaleDto>> GetAllAsync(string? source)
    {
        var query = _db.Sales.AsQueryable();
        if (!string.IsNullOrEmpty(source)) query = query.Where(s => s.Source == source);

        return await query.Include(s => s.Items).Include(s => s.Payments)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => MapToDto(s)).ToListAsync();
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

        return new PagedResult<SaleDto>
        {
            Items = entities.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<SaleDto?> GetByIdAsync(int id)
    {
        var s = await _db.Sales.Include(s => s.Items).Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == id);
        if (s == null) return null;
        return MapToDto(s);
    }

    public async Task<object> CreateAsync(CreateSaleDto dto)
    {
        var isCounter = string.Equals(dto.Source, "pos", StringComparison.OrdinalIgnoreCase);
        if (dto.Items.Count == 0) throw new BusinessRuleException("The bill has no items.");

        var settings = await _settings.GetAsync();
        if (isCounter) await ValidateCounterRulesAsync(dto, settings);

        var locationId = await _locations.PickAsync(dto.LocationId, dto.Store);
        var location = await _db.Locations.FirstAsync(l => l.Id == locationId);
        decimal? previousBalance = null;
        string? creditOverrideBy = null;
        int? priceLevelId = dto.PriceLevelId;
        if (dto.CustomerId.HasValue)
        {
            previousBalance = await _accounts.BalanceAsync(dto.CustomerId.Value);
            priceLevelId ??= await _db.Parties.Where(p => p.Id == dto.CustomerId).Select(p => p.PriceLevelId).FirstOrDefaultAsync();
            if (string.Equals(dto.PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
                creditOverrideBy = await _accounts.EnsureCreditAsync(dto.CustomerId.Value, dto.GrandTotal, dto.CreditOverride);
        }

        Sale sale;
        using (var transaction = await _db.Database.BeginTransactionAsync())
        {
            try
            {
                var lastRef = await _db.Sales.OrderByDescending(s => s.Id).Select(s => s.Reference).FirstOrDefaultAsync();
                int nextNum = 1;
                if (!string.IsNullOrEmpty(lastRef) && lastRef.StartsWith("SL"))
                { int.TryParse(lastRef.Substring(2), out nextNum); nextNum++; }
                var reference = $"SL{nextNum:D3}";

                int? tillId = null;
                if (isCounter)
                {
                    var user = _audit.CurrentUser;
                    tillId = await _db.TillSessions.Where(t => t.Status == "Open" && t.UserName == user)
                        .OrderByDescending(t => t.OpenedAt).Select(t => (int?)t.Id).FirstOrDefaultAsync();
                }

                sale = new Sale
                {
                    Reference = reference, OrderNumber = dto.OrderNumber,
                    CustomerId = dto.CustomerId, CustomerName = dto.CustomerName,
                    CustomerImage = dto.CustomerImage, Biller = dto.Biller, Source = dto.Source,
                    GrandTotal = dto.GrandTotal, Paid = 0, Due = dto.GrandTotal,
                    OrderTax = dto.OrderTax, Discount = dto.Discount, Shipping = dto.Shipping,
                    Status = dto.Status, PaymentStatus = !string.IsNullOrEmpty(dto.ExpectedDate) ? "Pending" : "Unpaid", Notes = dto.Notes,
                    ExpectedDate = !string.IsNullOrEmpty(dto.ExpectedDate) ? DateTime.Parse(dto.ExpectedDate) : null,
                    SaleDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
                    Store = dto.Store, TillSessionId = tillId, CouponCode = string.IsNullOrWhiteSpace(dto.CouponCode) ? null : dto.CouponCode.Trim().ToUpperInvariant(),
                    PaymentMethod = dto.PaymentMethod, PatientName = dto.PatientName, DoctorName = dto.DoctorName,
                    PrescriptionImage = dto.PrescriptionImage,
                    FbrStatus = isCounter && settings.FbrEnabled ? "NotSent" : null,
                    LocationId = locationId, PriceLevelId = priceLevelId, PreviousBalance = previousBalance,
                    CreditOverrideBy = creditOverrideBy,
                    Channel = !string.IsNullOrWhiteSpace(dto.Channel) ? dto.Channel! : isCounter ? "POS" : "Web",
                    OrderType = dto.OrderType, DiningTableId = dto.DiningTableId, Covers = dto.Covers
                };
                if (string.IsNullOrWhiteSpace(sale.Store)) sale.Store = location.Name;

                var built = await BuildItemsAsync(dto.Items, strictStock: isCounter || sale.Channel != "POS",
                    new StockRef(sale.Channel == "POS" ? "Sale" : "OnlineOrder", reference, null, locationId, null, "Sale"));
                sale.Items = built.Select(b => b.item).ToList();
                _db.Sales.Add(sale);
                await _db.SaveChangesAsync();
                await SaveAllocationsAsync(built);

                if (dto.QuotationId.HasValue)
                {
                    var quotation = await _db.Quotations.FirstOrDefaultAsync(q => q.Id == dto.QuotationId);
                    if (quotation != null) quotation.Status = "Ordered";
                }

                await _db.SaveChangesAsync();
                var restaurant = _services.GetService<IRestaurantService>();
                if (restaurant != null) await restaurant.OnSaleCreatedAsync(sale);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        if (sale.CustomerId.HasValue && string.Equals(dto.PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
            await _accounts.ApplyAdvancesAsync(sale.CustomerId.Value);

        string? fbrInvoice = null, fbrError = null;
        if (isCounter && settings.FbrEnabled)
        {
            var (_, inv, err) = await _fbr.SubmitAsync(sale.Id);
            fbrInvoice = inv;
            fbrError = err;
        }
        return new { sale.Id, sale.Reference, fbrInvoiceNumber = fbrInvoice, fbrError };
    }

    public async Task<object?> UpdateAsync(int id, CreateSaleDto dto)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return null;
        await EnsureNoReturnsAsync(id);

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _audit.Log("Updated", "Sale", sale.Reference, new { from = sale.GrandTotal, to = dto.GrandTotal, items = dto.Items.Count });
            sale.CustomerId = dto.CustomerId; sale.CustomerName = dto.CustomerName;
            sale.CustomerImage = dto.CustomerImage; sale.Biller = dto.Biller;
            sale.GrandTotal = dto.GrandTotal; sale.OrderTax = dto.OrderTax;
            sale.Discount = dto.Discount; sale.Shipping = dto.Shipping;
            sale.Status = dto.Status; sale.Notes = dto.Notes;
            sale.ExpectedDate = !string.IsNullOrEmpty(dto.ExpectedDate) ? DateTime.Parse(dto.ExpectedDate) : null;
            if (dto.DoctorName != null) sale.DoctorName = dto.DoctorName;
            if (dto.PatientName != null) sale.PatientName = dto.PatientName;
            RecalculateSalePaymentState(sale);

            await RestoreItemsAsync(sale.Items, new StockRef("Sale", sale.Reference + " edit", sale.Id, sale.LocationId, null, "Sale"));
            _db.SaleItems.RemoveRange(sale.Items);

            var built = await BuildItemsAsync(dto.Items, strictStock: false,
                new StockRef("Sale", sale.Reference + " edit", sale.Id, sale.LocationId, null, "Sale"));
            foreach (var b in built) b.item.SaleId = id;
            sale.Items = built.Select(b => b.item).ToList();
            await _db.SaveChangesAsync();
            await SaveAllocationsAsync(built);

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
        await EnsureNoReturnsAsync(id);

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            await RestoreItemsAsync(sale.Items, new StockRef("Sale", sale.Reference + " deleted", sale.Id, sale.LocationId, null, "Sale"));
            var restaurant = _services.GetService<IRestaurantService>();
            if (restaurant != null) await restaurant.OnSaleDeletedAsync(sale.Id);
            _audit.Log("Deleted", "Sale", sale.Reference, new { sale.CustomerName, sale.GrandTotal, sale.Paid, items = sale.Items.Select(i => new { i.ProductName, i.Quantity }) });
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

    // ── stock & counter helpers ──

    private async Task EnsureNoReturnsAsync(int saleId)
    {
        if (await _db.SalesReturns.AnyAsync(r => r.SaleId == saleId))
            throw new BusinessRuleException("This bill has returns recorded against it. Delete those returns before changing or deleting the bill.");
    }

    /// <summary>Stock that can be sold now: unexpired batches plus stock that has no batch record.</summary>
    private async Task<int> SellableStockAsync(Product product)
    {
        var today = DateTime.UtcNow.Date;
        var batches = await _db.ProductBatches.Where(b => b.ProductId == product.Id).ToListAsync();
        var inBatches = batches.Sum(b => b.Quantity);
        var unexpired = batches.Where(b => b.Quantity > 0 && (b.ExpiryDate == null || b.ExpiryDate.Value.Date >= today)).Sum(b => b.Quantity);
        var noBatch = Math.Max(0, product.Quantity - inBatches);
        return unexpired + noBatch;
    }

    private async Task<List<(SaleItem item, List<BatchAllocation> alloc)>> BuildItemsAsync(List<CreateSaleItemDto> items, bool strictStock, StockRef at)
    {
        var result = new List<(SaleItem, List<BatchAllocation>)>();
        foreach (var i in items)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == i.ProductId);
            var unit = string.IsNullOrWhiteSpace(i.SaleUnit) ? "Unit" : i.SaleUnit;
            var unitQty = i.UnitQuantity > 0 ? i.UnitQuantity : i.Quantity;
            var baseQty = product != null && i.UnitQuantity > 0
                ? i.UnitQuantity * ReactPosApi.Controllers.PurchasesController.UnitMultiplier(product, unit)
                : i.Quantity;
            if (baseQty <= 0) throw new BusinessRuleException($"Quantity for {i.ProductName} must be above zero.");

            var item = new SaleItem
            {
                ProductId = i.ProductId, ProductName = string.IsNullOrWhiteSpace(i.ProductName) ? product?.ProductName ?? "" : i.ProductName,
                Quantity = baseQty, PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
                TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost,
                SaleUnit = unit, UnitQuantity = unitQty, VariantId = i.VariantId, ModifiersJson = i.ModifiersJson
            };

            var alloc = new List<BatchAllocation>();
            if (product != null)
            {
                var lineAt = at with { VariantId = i.VariantId };
                if (product.IsMenuItem)
                {
                    // Dishes hold no stock; the restaurant module deducts their ingredients.
                    result.Add((item, alloc));
                    continue;
                }
                if (strictStock)
                {
                    var sellable = await _inventory.AvailableAsync(product, at.LocationId, i.VariantId);
                    if (sellable < baseQty)
                        throw new BusinessRuleException(sellable <= 0
                            ? $"{product.ProductName} is out of stock (expired stock cannot be sold)."
                            : $"{product.ProductName}: only {sellable} unexpired in stock, {baseQty} needed.");
                }
                alloc = await _inventory.DeductFefoAsync(product, baseQty, lineAt, spillToOtherLocations: !strictStock);
                item.CostPrice = _inventory.AverageCost(alloc, product.PurchasePrice);
                item.BatchInfo = string.Join(", ", alloc
                    .Select(a => a.ExpiryDate.HasValue ? $"{a.BatchNo} ({a.ExpiryDate:MM/yyyy})" : a.BatchNo)
                    .Distinct());
                if (item.BatchInfo.Length > 500) item.BatchInfo = item.BatchInfo[..500];
            }
            result.Add((item, alloc));
        }
        return result;
    }

    private async Task SaveAllocationsAsync(List<(SaleItem item, List<BatchAllocation> alloc)> built)
    {
        foreach (var (item, alloc) in built)
            foreach (var a in alloc)
                _db.SaleItemBatches.Add(new SaleItemBatch { SaleItemId = item.Id, BatchId = a.BatchId, Quantity = a.Quantity, UnitCost = a.UnitCost });
        await Task.CompletedTask;
    }

    private async Task RestoreItemsAsync(IEnumerable<SaleItem> items, StockRef at)
    {
        var list = items.ToList();
        var ids = list.Select(i => i.Id).ToList();
        var rows = await _db.SaleItemBatches.Where(b => ids.Contains(b.SaleItemId)).ToListAsync();
        foreach (var item in list)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product == null) continue;
            var mine = rows.Where(r => r.SaleItemId == item.Id).ToList();
            var allocs = mine.Select(r => new BatchAllocation(r.BatchId, r.Quantity, r.UnitCost, "", null)).ToList();
            var noBatch = Math.Max(0, item.Quantity - mine.Sum(r => r.Quantity));
            if (product.IsMenuItem) continue;
            await _inventory.RestoreAsync(product, allocs, noBatch, at with { VariantId = item.VariantId });
        }
        _db.SaleItemBatches.RemoveRange(rows);
    }

    private async Task ValidateCounterRulesAsync(CreateSaleDto dto, PharmacySettings settings)
    {
        // Discount limit for staff who are not admins.
        var gross = dto.Items.Sum(i => i.PurchasePrice * (i.UnitQuantity > 0 ? i.UnitQuantity : i.Quantity));
        var discount = dto.Discount + dto.Items.Sum(i => i.Discount);
        if (gross > 0 && discount > 0 && !_audit.IsAdmin)
        {
            var pct = discount / gross * 100;
            if (pct > settings.MaxCashierDiscountPercent + 0.01m)
                throw new BusinessRuleException($"Discount is {pct:0.#}% of the bill. Your limit is {settings.MaxCashierDiscountPercent:0.#}%; ask a manager.");
        }

        // Prescription-only medicines need the doctor's name.
        if (settings.RequirePrescription && string.IsNullOrWhiteSpace(dto.DoctorName))
        {
            var ids = dto.Items.Select(i => i.ProductId).ToList();
            var rx = await _db.Products.Where(p => ids.Contains(p.Id) && p.IsPrescriptionOnly).Select(p => p.ProductName).ToListAsync();
            if (rx.Count > 0)
                throw new BusinessRuleException($"{string.Join(", ", rx)} need a prescription. Enter the doctor's name before completing the bill.");
        }

        // Credit only for registered customers.
        if (string.Equals(dto.PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase) && dto.CustomerId == null)
            throw new BusinessRuleException("Credit is only available for registered customers.");

        if (!string.IsNullOrWhiteSpace(dto.CouponCode))
        {
            var (ok, error, _) = await CouponRules.CheckAsync(_db, dto.CouponCode, dto.CustomerId, gross);
            if (!ok) throw new BusinessRuleException(error!);
        }
    }

    public async Task<(bool ok, string? invoiceNumber, string? error)> ResendToFbrAsync(int id) => await _fbr.SubmitAsync(id);

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
            sale.PaymentStatus = "Overdue";
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
        OrderTax = s.OrderTax, Discount = s.Discount, Shipping = s.Shipping,
        Status = NormalizeOrderStatusForDto(s), PaymentStatus = s.PaymentStatus, Notes = s.Notes, Source = s.Source,
        ExpectedDate = s.ExpectedDate?.ToString("yyyy-MM-dd"),
        SaleDate = s.SaleDate.ToString("dd MMM yyyy"),
        SaleDateTime = DateTime.SpecifyKind(s.SaleDate, DateTimeKind.Utc).ToString("o"),
        Store = s.Store, PaymentMethod = s.PaymentMethod, CouponCode = s.CouponCode,
        PatientName = s.PatientName, DoctorName = s.DoctorName, PrescriptionImage = s.PrescriptionImage,
        FbrInvoiceNumber = s.FbrInvoiceNumber, FbrStatus = s.FbrStatus, TillSessionId = s.TillSessionId,
        LocationId = s.LocationId, PriceLevelId = s.PriceLevelId, PreviousBalance = s.PreviousBalance, Channel = s.Channel,
        OrderType = s.OrderType, DiningTableId = s.DiningTableId, Covers = s.Covers,
        Items = s.Items.Select(i => new SaleItemDto
        {
            Id = i.Id, ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity,
            PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
            TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost,
            CostPrice = i.CostPrice, SaleUnit = i.SaleUnit, UnitQuantity = i.UnitQuantity > 0 ? i.UnitQuantity : i.Quantity,
            BatchInfo = i.BatchInfo, VariantId = i.VariantId, ModifiersJson = i.ModifiersJson
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
    private readonly IInventoryService _inventory;
    private readonly IAuditService _audit;
    private readonly ICustomerAccountService _accounts;

    public SalesReturnService(AppDbContext db, IInventoryService inventory, IAuditService audit, ICustomerAccountService accounts)
    {
        _db = db;
        _inventory = inventory;
        _audit = audit;
        _accounts = accounts;
    }

    private static bool IsCreditNote(string? method) => string.Equals(method, "Credit note", StringComparison.OrdinalIgnoreCase);

    private async Task<StockRef> RefFor(SalesReturn r, string suffix = "")
    {
        int? locationId = r.SaleId.HasValue ? await _db.Sales.Where(s => s.Id == r.SaleId).Select(s => s.LocationId).FirstOrDefaultAsync() : null;
        return new StockRef("SaleReturn", r.Reference + suffix, r.Id > 0 ? r.Id : null, locationId, null, "SalesReturn");
    }

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
                ReturnDate = r.ReturnDate.ToString("dd MMM yyyy"),
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
            ReturnDate = r.ReturnDate.ToString("dd MMM yyyy"),
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
            ReturnDate = r.ReturnDate.ToString("dd MMM yyyy"),
            CreatedAt = r.CreatedAt.ToString("dd MMM yyyy"),
            SaleId = r.SaleId, RefundMethod = r.RefundMethod,
            Items = r.Items.Select(i => new SalesReturnItemDto
            {
                Id = i.Id, ProductName = i.ProductName, NetUnitPrice = i.NetUnitPrice,
                Stock = i.Stock, Quantity = i.Quantity, Discount = i.Discount,
                TaxPercent = i.TaxPercent, Subtotal = i.Subtotal,
                ProductId = i.ProductId, SaleItemId = i.SaleItemId, Condition = i.Condition
            }).ToList()
        };
    }

    public async Task<object> CreateAsync(CreateSalesReturnDto dto)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var reference = string.IsNullOrWhiteSpace(dto.Reference)
                ? $"RT{await _db.SalesReturns.CountAsync() + 1:D4}"
                : dto.Reference;
            var entity = new SalesReturn
            {
                Reference = reference,
                CustomerId = dto.CustomerId, CustomerName = dto.CustomerName,
                CustomerImage = dto.CustomerImage, ProductId = dto.ProductId,
                ProductName = dto.ProductName, ProductImage = dto.ProductImage,
                OrderTax = dto.OrderTax, Discount = dto.Discount, Shipping = dto.Shipping,
                GrandTotal = dto.GrandTotal, Paid = dto.Paid, Due = dto.Due,
                Status = dto.Status, PaymentStatus = dto.PaymentStatus,
                ReturnDate = dto.ReturnDate != null ? DateTime.Parse(dto.ReturnDate) : DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                SaleId = dto.SaleId, RefundMethod = dto.RefundMethod,
                Items = await BuildItemsAsync(dto, null)
            };
            if (string.IsNullOrWhiteSpace(entity.ProductName) && entity.Items.Count > 0)
            {
                entity.ProductName = entity.Items[0].ProductName;
                entity.ProductId ??= entity.Items[0].ProductId;
            }

            if (IsCreditNote(entity.RefundMethod) && entity.CustomerId == null && entity.SaleId.HasValue)
                entity.CustomerId = await _db.Sales.Where(s => s.Id == entity.SaleId).Select(s => s.CustomerId).FirstOrDefaultAsync();
            if (IsCreditNote(entity.RefundMethod) && entity.CustomerId == null)
                throw new BusinessRuleException("A credit note needs a registered customer. Refund by cash or card instead.");

            _db.SalesReturns.Add(entity);
            await ApplyStockAsync(entity.Items, +1, await RefFor(entity));
            entity.StockApplied = true;
            _audit.Log("Refund", "SalesReturn", entity.Reference, new
            {
                entity.SaleId, entity.CustomerName, entity.GrandTotal, entity.RefundMethod,
                items = entity.Items.Select(i => new { i.ProductName, i.Quantity, i.Condition })
            });
            await _db.SaveChangesAsync();
            // A credit note reduces what the customer owes (original bill first).
            if (IsCreditNote(entity.RefundMethod)) await _accounts.ApplyCreditNoteAsync(entity);
            await transaction.CommitAsync();
            return new { entity.Id, entity.Reference };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<object?> UpdateAsync(int id, CreateSalesReturnDto dto)
    {
        var entity = await _db.SalesReturns.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            if (entity.StockApplied) await ApplyStockAsync(entity.Items, -1, await RefFor(entity, " edit"));
            await _accounts.RemoveCreditNoteAsync(entity.Id);

            entity.CustomerId = dto.CustomerId; entity.CustomerName = dto.CustomerName;
            entity.CustomerImage = dto.CustomerImage; entity.ProductId = dto.ProductId;
            entity.ProductName = dto.ProductName; entity.ProductImage = dto.ProductImage;
            entity.OrderTax = dto.OrderTax; entity.Discount = dto.Discount;
            entity.Shipping = dto.Shipping; entity.GrandTotal = dto.GrandTotal;
            entity.Paid = dto.Paid; entity.Due = dto.Due;
            entity.Status = dto.Status; entity.PaymentStatus = dto.PaymentStatus;
            entity.SaleId = dto.SaleId ?? entity.SaleId;
            entity.RefundMethod = dto.RefundMethod ?? entity.RefundMethod;
            if (dto.ReturnDate != null) entity.ReturnDate = DateTime.Parse(dto.ReturnDate);

            _db.SalesReturnItems.RemoveRange(entity.Items);
            entity.Items = await BuildItemsAsync(dto, id);
            foreach (var i in entity.Items) i.SalesReturnId = id;

            await ApplyStockAsync(entity.Items, +1, await RefFor(entity, " edit"));
            entity.StockApplied = true;
            _audit.Log("Updated", "SalesReturn", entity.Reference, new { entity.GrandTotal });
            await _db.SaveChangesAsync();
            if (IsCreditNote(entity.RefundMethod)) await _accounts.ApplyCreditNoteAsync(entity);
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
        using var transaction = await _db.Database.BeginTransactionAsync();
        if (entity.StockApplied) await ApplyStockAsync(entity.Items, -1, await RefFor(entity, " deleted"));
        await _accounts.RemoveCreditNoteAsync(entity.Id);
        _audit.Log("Deleted", "SalesReturn", entity.Reference, new { entity.CustomerName, entity.GrandTotal });
        _db.SalesReturns.Remove(entity);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    /// <summary>Lines of a bill with how much can still be returned.</summary>
    public async Task<object?> GetReturnableAsync(int saleId)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId);
        if (sale == null) return null;
        var itemIds = sale.Items.Select(i => i.Id).ToList();
        var returned = await _db.SalesReturnItems
            .Where(r => r.SaleItemId != null && itemIds.Contains(r.SaleItemId.Value))
            .GroupBy(r => r.SaleItemId!.Value)
            .Select(g => new { Id = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();
        return new
        {
            sale.Id, sale.Reference, sale.CustomerId, sale.CustomerName, sale.GrandTotal,
            saleDate = sale.SaleDate.ToString("yyyy-MM-dd"),
            items = sale.Items.Select(i =>
            {
                var done = returned.FirstOrDefault(r => r.Id == i.Id)?.Qty ?? 0;
                var unitPrice = i.Quantity > 0 ? Math.Round(i.TotalCost / i.Quantity, 4) : 0;
                return new
                {
                    saleItemId = i.Id, i.ProductId, i.ProductName, soldQuantity = i.Quantity,
                    returnedQuantity = done, returnableQuantity = Math.Max(0, i.Quantity - done),
                    unitPrice, i.SaleUnit, i.UnitQuantity, i.BatchInfo
                };
            })
        };
    }

    private async Task<List<SalesReturnItem>> BuildItemsAsync(CreateSalesReturnDto dto, int? editingReturnId)
    {
        var items = new List<SalesReturnItem>();
        foreach (var i in dto.Items)
        {
            if (i.Quantity <= 0) throw new BusinessRuleException($"Return quantity for {i.ProductName} must be above zero.");

            var productId = i.ProductId;
            if (productId == null && !string.IsNullOrWhiteSpace(i.ProductName))
                productId = await _db.Products.Where(p => p.ProductName == i.ProductName).Select(p => (int?)p.Id).FirstOrDefaultAsync();

            if (i.SaleItemId.HasValue)
            {
                var saleItem = await _db.SaleItems.FirstOrDefaultAsync(s => s.Id == i.SaleItemId);
                if (saleItem == null) throw new BusinessRuleException($"The bill line for {i.ProductName} was not found.");
                var already = await _db.SalesReturnItems
                    .Where(r => r.SaleItemId == i.SaleItemId && r.SalesReturnId != (editingReturnId ?? 0))
                    .SumAsync(r => (int?)r.Quantity) ?? 0;
                if (already + i.Quantity > saleItem.Quantity)
                    throw new BusinessRuleException($"{saleItem.ProductName}: {saleItem.Quantity} sold, {already} already returned; cannot return {i.Quantity} more.");
                productId ??= saleItem.ProductId;
            }

            var condition = i.Condition switch
            {
                "Damaged" or "damaged" => "Damaged",
                "Expired" or "expired" => "Expired",
                _ => "Saleable"
            };
            items.Add(new SalesReturnItem
            {
                ProductName = i.ProductName, NetUnitPrice = i.NetUnitPrice,
                Stock = i.Stock, Quantity = i.Quantity, Discount = i.Discount,
                TaxPercent = i.TaxPercent, Subtotal = i.Subtotal,
                ProductId = productId, SaleItemId = i.SaleItemId, Condition = condition
            });
        }
        return items;
    }

    /// <summary>direction +1 puts saleable stock back; -1 undoes that (return edited or deleted).</summary>
    private async Task ApplyStockAsync(IEnumerable<SalesReturnItem> items, int direction, StockRef at)
    {
        foreach (var i in items.Where(x => x.Condition == "Saleable" && x.ProductId != null))
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == i.ProductId);
            if (product == null) continue;

            // Return into the batches the bill took stock from, latest-expiry first.
            var allocs = new List<BatchAllocation>();
            if (i.SaleItemId.HasValue)
            {
                var remaining = i.Quantity;
                var rows = await _db.SaleItemBatches.Where(b => b.SaleItemId == i.SaleItemId).ToListAsync();
                var batchIds = rows.Select(r => r.BatchId).ToList();
                var batches = await _db.ProductBatches.Where(b => batchIds.Contains(b.Id)).ToListAsync();
                foreach (var row in rows.OrderByDescending(r => batches.FirstOrDefault(b => b.Id == r.BatchId)?.ExpiryDate ?? DateTime.MinValue))
                {
                    if (remaining <= 0) break;
                    var q = Math.Min(row.Quantity, remaining);
                    allocs.Add(new BatchAllocation(row.BatchId, q, row.UnitCost, "", null));
                    remaining -= q;
                }
            }
            var noBatch = i.Quantity - allocs.Sum(a => a.Quantity);

            var lineAt = at with { VariantId = i.VariantId };
            if (direction > 0)
            {
                if (allocs.Count > 0) await _inventory.RestoreAsync(product, allocs, 0, lineAt);
                if (noBatch > 0) await _inventory.AddStockAsync(product, noBatch, "RETURN", null, product.PurchasePrice, at: lineAt);
            }
            else
            {
                foreach (var a in allocs) await _inventory.DeductFromBatchAsync(product, a.BatchId, a.Quantity, lineAt);
                if (noBatch > 0) await _inventory.DeductFefoAsync(product, noBatch, lineAt, spillToOtherLocations: true);
            }
        }
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
            .ToListAsync();

        return new PagedResult<QuotationDto>
        {
            Items = entities.Select(x => new QuotationDto
            {
                Id = x.Id, Reference = x.Reference, CustomerId = x.CustomerId,
                CustomerName = x.CustomerName, CustomerImage = x.CustomerImage,
                ProductId = x.ProductId, ProductName = x.ProductName, ProductImage = x.ProductImage,
                OrderTax = x.OrderTax, Discount = x.Discount, Shipping = x.Shipping,
                GrandTotal = x.GrandTotal, Status = x.Status, Description = x.Description,
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
            OrderTax = x.OrderTax, Discount = x.Discount, Shipping = x.Shipping,
            GrandTotal = x.GrandTotal, Status = x.Status, Description = x.Description,
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
            OrderTax = x.OrderTax, Discount = x.Discount, Shipping = x.Shipping,
            GrandTotal = x.GrandTotal, Status = x.Status, Description = x.Description,
            QuotationDate = x.QuotationDate.ToString("dd MMM yyyy"),
            CreatedAt = x.CreatedAt.ToString("dd MMM yyyy"),
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
        var entity = new Quotation
        {
            Reference = dto.Reference ?? string.Empty,
            CustomerId = dto.CustomerId, CustomerName = dto.CustomerName,
            CustomerImage = dto.CustomerImage, ProductId = dto.ProductId,
            ProductName = dto.ProductName, ProductImage = dto.ProductImage,
            OrderTax = dto.OrderTax, Discount = dto.Discount, Shipping = dto.Shipping,
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
        _db.Quotations.Add(entity);
        await _db.SaveChangesAsync();
        return new { entity.Id };
    }

    public async Task<object?> UpdateAsync(int id, CreateQuotationDto dto)
    {
        var entity = await _db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
        if (entity == null) return null;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            entity.Reference = dto.Reference ?? entity.Reference;
            entity.CustomerId = dto.CustomerId; entity.CustomerName = dto.CustomerName;
            entity.CustomerImage = dto.CustomerImage; entity.ProductId = dto.ProductId;
            entity.ProductName = dto.ProductName; entity.ProductImage = dto.ProductImage;
            entity.OrderTax = dto.OrderTax; entity.Discount = dto.Discount;
            entity.Shipping = dto.Shipping; entity.GrandTotal = dto.GrandTotal;
            entity.Status = dto.Status; entity.Description = dto.Description;
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

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id);
        if (entity == null) return false;
        _db.Quotations.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}

// ── CouponService ──
public class CouponService : ICouponService
{
    private readonly AppDbContext _db;
    public CouponService(AppDbContext db) => _db = db;

    public async Task<List<CouponDto>> GetAllAsync(string? type, string? status, string? sort)
    {
        var q = _db.Coupons.AsQueryable();
        if (!string.IsNullOrEmpty(type)) q = q.Where(c => c.Type == type);
        if (!string.IsNullOrEmpty(status)) q = q.Where(c => c.Status == status);

        q = sort switch
        {
            "Ascending" => q.OrderBy(c => c.Name),
            "Descending" => q.OrderByDescending(c => c.Name),
            _ => q.OrderByDescending(c => c.CreatedAt)
        };

        return await q.Select(c => new CouponDto
        {
            Id = c.Id, Name = c.Name, Code = c.Code, Description = c.Description,
            Type = c.Type, Discount = c.Discount, Limit = c.Limit,
            StartDate = c.StartDate, EndDate = c.EndDate, OncePerCustomer = c.OncePerCustomer,
            ProductId = c.ProductId, ProductName = c.ProductName, Status = c.Status
        }).ToListAsync();
    }

    public async Task<CouponDto?> GetByIdAsync(int id)
    {
        var c = await _db.Coupons.FindAsync(id);
        if (c == null) return null;
        return new CouponDto
        {
            Id = c.Id, Name = c.Name, Code = c.Code, Description = c.Description,
            Type = c.Type, Discount = c.Discount, Limit = c.Limit,
            StartDate = c.StartDate, EndDate = c.EndDate, OncePerCustomer = c.OncePerCustomer,
            ProductId = c.ProductId, ProductName = c.ProductName, Status = c.Status
        };
    }

    public async Task<object> CreateAsync(CreateCouponDto dto)
    {
        var entity = new Coupon
        {
            Name = dto.Name, Code = dto.Code, Description = dto.Description,
            Type = dto.Type, Discount = dto.Discount, Limit = dto.Limit,
            StartDate = dto.StartDate, EndDate = dto.EndDate,
            OncePerCustomer = dto.OncePerCustomer, ProductId = dto.ProductId,
            ProductName = dto.ProductName, Status = dto.Status
        };
        _db.Coupons.Add(entity);
        await _db.SaveChangesAsync();
        return new { entity.Id };
    }

    public async Task<object?> UpdateAsync(int id, CreateCouponDto dto)
    {
        var entity = await _db.Coupons.FindAsync(id);
        if (entity == null) return null;

        entity.Name = dto.Name; entity.Code = dto.Code; entity.Description = dto.Description;
        entity.Type = dto.Type; entity.Discount = dto.Discount; entity.Limit = dto.Limit;
        entity.StartDate = dto.StartDate; entity.EndDate = dto.EndDate;
        entity.OncePerCustomer = dto.OncePerCustomer; entity.ProductId = dto.ProductId;
        entity.ProductName = dto.ProductName; entity.Status = dto.Status;

        await _db.SaveChangesAsync();
        return new { entity.Id };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.Coupons.FindAsync(id);
        if (entity == null) return false;
        _db.Coupons.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }
}


/// <summary>Coupon checks shared by the counter and the coupon-validate endpoint.</summary>
public static class CouponRules
{
    public static async Task<(bool ok, string? error, decimal discount)> CheckAsync(AppDbContext db, string code, int? customerId, decimal billAmount)
    {
        var c = code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.FirstOrDefaultAsync(x => x.Code.ToUpper() == c);
        if (coupon == null) return (false, $"Coupon {code} does not exist.", 0);
        if (!string.Equals(coupon.Status, "Active", StringComparison.OrdinalIgnoreCase)) return (false, $"Coupon {code} is not active.", 0);

        var today = DateTime.UtcNow.Date;
        if (DateTime.TryParse(coupon.StartDate, out var start) && today < start.Date) return (false, $"Coupon {code} starts on {start:dd MMM yyyy}.", 0);
        if (DateTime.TryParse(coupon.EndDate, out var end) && today > end.Date) return (false, $"Coupon {code} expired on {end:dd MMM yyyy}.", 0);

        if (coupon.Limit > 0)
        {
            var used = await db.Sales.CountAsync(s => s.CouponCode == c);
            if (used >= coupon.Limit) return (false, $"Coupon {code} has reached its limit of {coupon.Limit} uses.", 0);
        }
        if (coupon.OncePerCustomer)
        {
            if (customerId == null) return (false, $"Coupon {code} is once per customer; choose the customer first.", 0);
            if (await db.Sales.AnyAsync(s => s.CouponCode == c && s.CustomerId == customerId))
                return (false, $"This customer has already used coupon {code}.", 0);
        }

        var discount = string.Equals(coupon.Type, "Fixed", StringComparison.OrdinalIgnoreCase)
            ? Math.Min(coupon.Discount, billAmount)
            : Math.Round(billAmount * coupon.Discount / 100, 2);
        return (true, null, discount);
    }
}
