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
        return list;
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
            sale.Status = dto.Status; sale.Notes = dto.Notes;
            sale.ExpectedDate = !string.IsNullOrEmpty(dto.ExpectedDate) ? DateTime.Parse(dto.ExpectedDate) : null;
            RecalculateSalePaymentState(sale);

            // ── Restore old item quantities to inventory ──
            foreach (var oldItem in sale.Items)
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
            foreach (var newItem in sale.Items)
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
            // ── Restore product quantities to inventory ──
            foreach (var item in sale.Items)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null)
                    product.Quantity += item.Quantity;
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
            RecalculateSalePaymentState(sale, paymentReduced: true);

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
            RecalculateSalePaymentState(sale, paymentReduced: true);

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

    /// <summary>Sets Due and PaymentStatus from GrandTotal/Paid. When fully paid, promotes order Status from Pending to Completed.
    /// paymentReduced: a payment was removed or lowered - a Completed sale that now has money due goes back to Pending.</summary>
    private static void RecalculateSalePaymentState(Sale sale, bool paymentReduced = false)
    {
        sale.Due = sale.GrandTotal - sale.Paid;
        if (paymentReduced && sale.Due > SaleMoneyEpsilon && string.Equals((sale.Status ?? string.Empty).Trim(), "Completed", StringComparison.OrdinalIgnoreCase))
            sale.Status = "Pending";
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
        OrderTax = s.OrderTax, Discount = s.Discount, Shipping = s.Shipping,
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
