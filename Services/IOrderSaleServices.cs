using ReactPosApi.DTOs;

namespace ReactPosApi.Services;

public interface ISaleService
{
    Task<SaleDto?> ConvertQuotationAsync(int quotationId, ConvertQuotationToSaleDto dto, string biller);
    Task<SaleDto?> MarkQueuedAsSoldAsync(int saleId);
    Task<List<SaleDto>> GetAllAsync(string? source);
    Task<PagedResult<SaleDto>> GetAllPagedAsync(string? source, PaginationQuery query, string? paymentStatus = null);
    Task<SaleDto?> GetByIdAsync(int id);
    Task<object> CreateAsync(CreateSaleDto dto);
    Task<object?> UpdateAsync(int id, CreateSaleDto dto);
    Task<bool> DeleteAsync(int id);
    Task<List<SalePaymentDto>?> GetPaymentsAsync(int saleId);
    Task<object?> CreatePaymentAsync(int saleId, CreateSalePaymentDto dto);
    Task<object?> UpdatePaymentAsync(int saleId, int paymentId, CreateSalePaymentDto dto);
    Task<bool> DeletePaymentAsync(int saleId, int paymentId);
    /// <summary>Split payment across customer's outstanding sales (FIFO). Per-sale POST /payments remains available.</summary>
    Task<(CustomerFifoPaymentResponseDto? Result, string? Error)> ApplyCustomerFifoPaymentAsync(CustomerFifoPaymentRequestDto dto);
}

public interface IInvoiceService
{
    Task<List<InvoiceDto>> GetAllAsync();
    Task<InvoiceDto?> GetByIdAsync(int id);
    Task<object> CreateAsync(CreateInvoiceDto dto);
    Task<object?> UpdateAsync(int id, CreateInvoiceDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface ISalesReturnService
{
    Task<List<SalesReturnDto>> GetAllAsync(string? customer, string? status, string? paymentStatus, string? sort);
    Task<PagedResult<SalesReturnDto>> GetAllPagedAsync(PaginationQuery query, string? paymentStatus = null);
    Task<SalesReturnDto?> GetByIdAsync(int id);
    Task<object> CreateAsync(CreateSalesReturnDto dto);
    Task<object?> UpdateAsync(int id, CreateSalesReturnDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface IQuotationService
{
    Task<List<QuotationDto>> GetAllAsync(string? product, string? customer, string? status, string? sort);
    Task<PagedResult<QuotationDto>> GetAllPagedAsync(PaginationQuery query);
    Task<QuotationDto?> GetByIdAsync(int id);
    Task<object> CreateAsync(CreateQuotationDto dto);
    Task<object?> UpdateAsync(int id, CreateQuotationDto dto);
    Task<bool> DeleteAsync(int id);
    /// <summary>Saves a new version of the quotation (…_R1, _R2 …); the old version is kept read-only</summary>
    Task<QuotationDto?> ReviseAsync(int id, CreateQuotationDto dto);
    /// <summary>All versions of the quotation (original first)</summary>
    Task<List<QuotationDto>> GetRevisionsAsync(int id);
}

