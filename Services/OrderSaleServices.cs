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
            Status = dto.Status, PaymentStatus = "Unpaid", Notes = dto.Notes,
            SaleDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            Items = dto.Items.Select(i => new SaleItem
            {
                ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity,
                PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
                TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost
            }).ToList()
        };
        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();
        return new { sale.Id, sale.Reference };
    }

    public async Task<object?> UpdateAsync(int id, CreateSaleDto dto)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return null;

        sale.CustomerId = dto.CustomerId; sale.CustomerName = dto.CustomerName;
        sale.CustomerImage = dto.CustomerImage; sale.Biller = dto.Biller;
        sale.GrandTotal = dto.GrandTotal; sale.OrderTax = dto.OrderTax;
        sale.Discount = dto.Discount; sale.Shipping = dto.Shipping;
        sale.Status = dto.Status; sale.Notes = dto.Notes;
        sale.Due = dto.GrandTotal - sale.Paid;
        if (sale.Due <= 0) { sale.Due = 0; sale.PaymentStatus = "Paid"; }
        else if (sale.Paid > 0) { sale.PaymentStatus = "Overdue"; }
        else { sale.PaymentStatus = "Unpaid"; }

        _db.SaleItems.RemoveRange(sale.Items);
        sale.Items = dto.Items.Select(i => new SaleItem
        {
            SaleId = id, ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity,
            PurchasePrice = i.PurchasePrice, Discount = i.Discount, TaxPercent = i.TaxPercent,
            TaxAmount = i.TaxAmount, UnitCost = i.UnitCost, TotalCost = i.TotalCost
        }).ToList();

        await _db.SaveChangesAsync();
        return new { sale.Id };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var sale = await _db.Sales.Include(s => s.Items).Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return false;
        _db.Sales.Remove(sale);
        await _db.SaveChangesAsync();
        return true;
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

        var payment = new SalePayment
        {
            SaleId = saleId, Reference = dto.Reference, ReceivedAmount = dto.ReceivedAmount,
            PayingAmount = dto.PayingAmount, PaymentType = dto.PaymentType,
            Description = dto.Description, PaymentDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        };
        _db.SalePayments.Add(payment);

        sale.Paid += dto.PayingAmount;
        sale.Due = sale.GrandTotal - sale.Paid;
        if (sale.Due <= 0) { sale.Due = 0; sale.PaymentStatus = "Paid"; }
        else { sale.PaymentStatus = "Overdue"; }

        await _db.SaveChangesAsync();
        return new { payment.Id };
    }

    public async Task<object?> UpdatePaymentAsync(int saleId, int paymentId, CreateSalePaymentDto dto)
    {
        var sale = await _db.Sales.FindAsync(saleId);
        if (sale == null) return null;
        var payment = await _db.SalePayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.SaleId == saleId);
        if (payment == null) return null;

        sale.Paid -= payment.PayingAmount;
        payment.Reference = dto.Reference; payment.ReceivedAmount = dto.ReceivedAmount;
        payment.PayingAmount = dto.PayingAmount; payment.PaymentType = dto.PaymentType;
        payment.Description = dto.Description;

        sale.Paid += dto.PayingAmount;
        sale.Due = sale.GrandTotal - sale.Paid;
        if (sale.Due <= 0) { sale.Due = 0; sale.PaymentStatus = "Paid"; }
        else if (sale.Paid > 0) { sale.PaymentStatus = "Overdue"; }
        else { sale.PaymentStatus = "Unpaid"; }

        await _db.SaveChangesAsync();
        return new { payment.Id };
    }

    public async Task<bool> DeletePaymentAsync(int saleId, int paymentId)
    {
        var sale = await _db.Sales.FindAsync(saleId);
        if (sale == null) return false;
        var payment = await _db.SalePayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.SaleId == saleId);
        if (payment == null) return false;

        sale.Paid -= payment.PayingAmount;
        sale.Due = sale.GrandTotal - sale.Paid;
        if (sale.Due <= 0) { sale.Due = 0; sale.PaymentStatus = "Paid"; }
        else if (sale.Paid > 0) { sale.PaymentStatus = "Overdue"; }
        else { sale.PaymentStatus = "Unpaid"; }

        _db.SalePayments.Remove(payment);
        await _db.SaveChangesAsync();
        return true;
    }

    private static SaleDto MapToDto(Sale s) => new()
    {
        Id = s.Id, Reference = s.Reference, OrderNumber = s.OrderNumber,
        CustomerId = s.CustomerId,
        CustomerName = s.CustomerName, CustomerImage = s.CustomerImage, Biller = s.Biller,
        GrandTotal = s.GrandTotal, Paid = s.Paid, Due = s.Due,
        OrderTax = s.OrderTax, Discount = s.Discount, Shipping = s.Shipping,
        Status = s.Status, PaymentStatus = s.PaymentStatus, Notes = s.Notes, Source = s.Source,
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
        return new { invoice.Id };
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
        return new { entity.Id };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.SalesReturns.Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) return false;
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
        return new { entity.Id };
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
