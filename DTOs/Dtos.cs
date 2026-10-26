namespace ReactPosApi.DTOs;

// ============================
// Dropdown
// ============================
public record DropdownOptionDto(string Value, string Label);

// ============================
// Category
// ============================
public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CreatedOn { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public class CreateCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

// ============================
// Product
// ============================
public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string SellingType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string BarcodeSymbology { get; set; } = string.Empty;
    public string ItemBarcode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductType { get; set; } = "single";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string TaxType { get; set; } = string.Empty;
    public string Tax { get; set; } = string.Empty;
    public List<AppliedTaxDto> Taxes { get; set; } = new();
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public int QuantityAlert { get; set; }
    public string Warranty { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string ManufacturedDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string[] Images { get; set; } = Array.Empty<string>();
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

// ============================
// Customer
// ============================
public class CustomerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SO { get; set; }
    public string? Cnic { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public string Status { get; set; } = "active";
}

/// <summary>Only the name is required; every other field is optional</summary>
public class CreateCustomerDto
{
    public string Name { get; set; } = string.Empty;
    public string? SO { get; set; }
    public string? Cnic { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string Status { get; set; } = "active";
}

// ============================
// Warranty
// ============================
public class WarrantyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; }
    public string Period { get; set; } = "Month";
    public string DurationDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public class CreateWarrantyDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; }
    public string Period { get; set; } = "Month";
    public string Status { get; set; } = "active";
}

// ============================
// Variant Attribute
// ============================
public class VariantAttributeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Values { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateVariantAttributeDto
{
    public string Name { get; set; } = string.Empty;
    public string Values { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

// ============================
// Stock Entry
// ============================
public class StockEntryDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Person { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Date { get; set; } = string.Empty;
}

public class CreateStockEntryDto
{
    public int ProductId { get; set; }
    public string Person { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

// ============================
// Unit
// ============================
public class UnitDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public string Status { get; set; } = "active";
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateUnitDto
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

// ============================
// Brand
// ============================
public class BrandDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string Status { get; set; } = "active";
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateBrandDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

// ============================
// Low Stock Update
// ============================
public class UpdateLowStockDto
{
    public string? Sku { get; set; }
    public string? Category { get; set; }
    public string? ProductName { get; set; }
    public int? Quantity { get; set; }
    public int? QuantityAlert { get; set; }
}

// ============================
// Sale (Online Orders)
// ============================
public class SaleDto
{
    public int Id { get; set; }
    /// <summary>Set when this sale was created by converting a quotation (its terms & conditions go on the receipt)</summary>
    public string? QuotationReference { get; set; }
    /// <summary>The source quotation's own terms & conditions, when they were edited for it</summary>
    public string? QuotationTerms { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public string Biller { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal InstallationCharges { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? ExpectedDate { get; set; }
    public string SaleDate { get; set; } = string.Empty;
    public List<AppliedTaxDto> Taxes { get; set; } = new();
    public List<SaleItemDto> Items { get; set; } = new();
    public List<SalePaymentDto> Payments { get; set; } = new();
}

public class SaleItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class SalePaymentDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal ReceivedAmount { get; set; }
    public decimal PayingAmount { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PaymentDate { get; set; } = string.Empty;
}

public class CreateSaleDto
{
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public string Biller { get; set; } = "Admin";
    public decimal GrandTotal { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
    public string Source { get; set; } = "online";
    public string? OrderNumber { get; set; }
    public string? ExpectedDate { get; set; }
    public List<CreateSaleItemDto> Items { get; set; } = new();
}

public class CreateSaleItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>Convert a quotation into a sale: stock is deducted and the payment received now is recorded</summary>
public class ConvertQuotationToSaleDto
{
    /// <summary>Amount received from the customer now (0 = unpaid, the rest stays due)</summary>
    public decimal PaidAmount { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string? Notes { get; set; }
}

public class CreateSalePaymentDto
{
    public string Reference { get; set; } = string.Empty;
    public decimal ReceivedAmount { get; set; }
    public decimal PayingAmount { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string? Description { get; set; }
}

/// <summary>Apply one payment across all outstanding POS (or other source) sales for a customer, oldest first (FIFO).</summary>
public class CustomerFifoPaymentRequestDto
{
    public int? CustomerId { get; set; }
    /// <summary>Required when CustomerId is null (walk-in / name-only match).</summary>
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string Reference { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Defaults to "pos". Only sales with this Source are included.</summary>
    public string Source { get; set; } = "pos";
}

public class FifoAllocationDto
{
    public int SaleId { get; set; }
    public string SaleReference { get; set; } = string.Empty;
    public decimal AmountApplied { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
}

public class CustomerFifoPaymentResponseDto
{
    public decimal TotalApplied { get; set; }
    public List<FifoAllocationDto> Allocations { get; set; } = new();
}

// ============================
// Invoice
// ============================
public class InvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string? FromAddress { get; set; }
    public string? FromEmail { get; set; }
    public string? FromPhone { get; set; }
    public string? InvoiceFor { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal Tax { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Paid { get; set; }
    public decimal AmountDue { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public string DueDate { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public List<InvoiceItemDto> Items { get; set; } = new();
}

public class InvoiceItemDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}

public class CreateInvoiceDto
{
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string? FromAddress { get; set; }
    public string? FromEmail { get; set; }
    public string? FromPhone { get; set; }
    public string? InvoiceFor { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal Tax { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Paid { get; set; }
    public decimal AmountDue { get; set; }
    public string Status { get; set; } = "Unpaid";
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public string? DueDate { get; set; }
    public List<CreateInvoiceItemDto> Items { get; set; } = new();
}

public class CreateInvoiceItemDto
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Cost { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}

// ── Sales Return ──────────────────────────────────────────
public class SalesReturnDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string ReturnDate { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    /// <summary>The POS sale this return belongs to</summary>
    public int? SaleId { get; set; }
    public string? SaleReference { get; set; }
    public List<SalesReturnItemDto> Items { get; set; } = new();
}

public class SalesReturnItemDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal NetUnitPrice { get; set; }
    public int Stock { get; set; }
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal Subtotal { get; set; }
}

public class CreateSalesReturnDto
{
    public string? Reference { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public string Status { get; set; } = "Pending";
    public string PaymentStatus { get; set; } = "Unpaid";
    public string? ReturnDate { get; set; }
    public List<CreateSalesReturnItemDto> Items { get; set; } = new();
}

public class CreateSalesReturnItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public decimal NetUnitPrice { get; set; }
    public int Stock { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal Subtotal { get; set; }
}

// ── Quotation ──────────────────────────────────────────
public class QuotationDto
{
    public int Id { get; set; }
    public int? SaleId { get; set; }
    public string? SaleReference { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal InstallationCharges { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string QuotationDate { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public List<AppliedTaxDto> Taxes { get; set; } = new();
    /// <summary>Terms edited for this quotation (null = standard terms); only filled when loading one quotation</summary>
    public string? TermsContent { get; set; }
    /// <summary>Revisions: original id (null on the original), 0/1/2... and the newer version that replaced this one</summary>
    public int? RootQuotationId { get; set; }
    public int RevisionNo { get; set; }
    public int? SupersededById { get; set; }
    /// <summary>Number of product lines and their names (for the list, which has no line items)</summary>
    public int ItemCount { get; set; }
    public List<string> ProductNames { get; set; } = new();
    public List<QuotationItemDto> Items { get; set; } = new();
}

public class QuotationItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class CreateQuotationDto
{
    public string? Reference { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal OrderTax { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal InstallationCharges { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Description { get; set; }
    public string? QuotationDate { get; set; }
    /// <summary>When sent, OrderTax and GrandTotal are calculated from these (base = items total - discount)</summary>
    public List<AppliedTaxDto>? Taxes { get; set; }
    /// <summary>Terms edited for this quotation. null = keep as is (standard on a new quotation); "" = back to the standard terms</summary>
    public string? TermsContent { get; set; }
    public List<CreateQuotationItemDto> Items { get; set; } = new();
}

public class CreateQuotationItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PurchasePrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
