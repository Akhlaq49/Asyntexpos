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
// SubCategory
// ============================
public class SubCategoryDto
{
    public int Id { get; set; }
    public string? Image { get; set; }
    public string SubCategory { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
}

public class CreateSubCategoryDto
{
    public string SubCategory { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? CategoryCode { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    // Image uploaded as IFormFile in the controller
}

// ============================
// Product
// ============================
public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string SellingType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string BarcodeSymbology { get; set; } = string.Empty;
    public string ItemBarcode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductType { get; set; } = "single";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string TaxType { get; set; } = string.Empty;
    public string Tax { get; set; } = string.Empty;
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
    public decimal MiscBalance { get; set; } = 0.00m;
}

public class CreateCustomerDto
{
    public string Name { get; set; } = string.Empty;
    public string? SO { get; set; }
    public string? Cnic { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

// ============================
// Installment
// ============================
public class RepaymentEntryDto
{
    public int InstallmentNo { get; set; }
    public string DueDate { get; set; } = string.Empty;
    public decimal EmiAmount { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = "upcoming";
    public string? PaidDate { get; set; }
    public decimal? ActualPaidAmount { get; set; }
    public decimal? MiscAdjustedAmount { get; set; }
}

public class GuarantorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SO { get; set; }
    public string? Phone { get; set; }
    public string? Cnic { get; set; }
    public string? Address { get; set; }
    public string? Relationship { get; set; }
    public string? Picture { get; set; }
}

public class InstallmentPlanDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerSo { get; set; }
    public string? CustomerCnic { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public string? CustomerImage { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public decimal? FinanceAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal FinancedAmount { get; set; }
    public decimal InterestRate { get; set; }
    public int Tenure { get; set; }
    public decimal EmiAmount { get; set; }
    public decimal TotalPayable { get; set; }
    public decimal TotalInterest { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public int PaidInstallments { get; set; }
    public int RemainingInstallments { get; set; }
    public string NextDueDate { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public List<RepaymentEntryDto> Schedule { get; set; } = new();
    public List<GuarantorDto> Guarantors { get; set; } = new();
}

public class CreateInstallmentDto
{
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public decimal? FinanceAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal InterestRate { get; set; }
    public int Tenure { get; set; }
    public string StartDate { get; set; } = string.Empty;
}

public class PayInstallmentDto
{
    public decimal Amount { get; set; }
    public bool UseMiscBalance { get; set; } = false;
    public string? PaymentMethod { get; set; } = "Cash";
    public string? Notes { get; set; }
}

public class PartySearchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SO { get; set; }
    public string? Phone { get; set; }
    public string? Cnic { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? Picture { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class PreviewInstallmentDto
{
    public decimal ProductPrice { get; set; }
    public decimal? FinanceAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal InterestRate { get; set; }
    public int Tenure { get; set; }
    public string StartDate { get; set; } = string.Empty;
}

public class InstallmentPreviewDto
{
    public decimal ProductPrice { get; set; }
    public decimal FinanceAmount { get; set; }
    public decimal FinancedAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal InterestRate { get; set; }
    public int Tenure { get; set; }
    public decimal EmiAmount { get; set; }
    public decimal TotalPayable { get; set; }
    public decimal TotalInterest { get; set; }
    public List<RepaymentEntryDto> Schedule { get; set; } = new();
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
    public string Warehouse { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
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
    public string Warehouse { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string Person { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

// ============================
// Stock Adjustment
// ============================
public class StockAdjustmentDto
{
    public int Id { get; set; }
    public string Warehouse { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Person { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public string Date { get; set; } = string.Empty;
}

public class CreateStockAdjustmentDto
{
    public string Warehouse { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Person { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
}

// ============================
// Stock Transfer
// ============================
public class StockTransferDto
{
    public int Id { get; set; }
    public string WarehouseFrom { get; set; } = string.Empty;
    public string WarehouseTo { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int NoOfProducts { get; set; }
    public int QuantityTransferred { get; set; }
    public string Date { get; set; } = string.Empty;
    public List<StockTransferItemDto> Items { get; set; } = new();
}

public class StockTransferItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class CreateStockTransferDto
{
    public string WarehouseFrom { get; set; } = string.Empty;
    public string WarehouseTo { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<CreateStockTransferItemDto> Items { get; set; } = new();
}

public class CreateStockTransferItemDto
{
    public int ProductId { get; set; }
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
// Expired Product Update
// ============================
public class UpdateExpiredDto
{
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? ManufacturedDate { get; set; }
    public string? ExpiryDate { get; set; }
}

// ============================
// Low Stock Update
// ============================
public class UpdateLowStockDto
{
    public string? Warehouse { get; set; }
    public string? Store { get; set; }
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
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Source { get; set; } = string.Empty;
    public string SaleDate { get; set; } = string.Empty;
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

public class CreateSalePaymentDto
{
    public string Reference { get; set; } = string.Empty;
    public decimal ReceivedAmount { get; set; }
    public decimal PayingAmount { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string? Description { get; set; }
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
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string QuotationDate { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
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
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Description { get; set; }
    public string? QuotationDate { get; set; }
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

// ── Coupon DTOs ─────────────────────────────────────────

public class CouponDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Percentage";
    public decimal Discount { get; set; }
    public int Limit { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool OncePerCustomer { get; set; }
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string Status { get; set; } = "Active";
}

public class CreateCouponDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Percentage";
    public decimal Discount { get; set; }
    public int Limit { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool OncePerCustomer { get; set; }
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public string Status { get; set; } = "Active";
}

// ============================
// Miscellaneous Register
// ============================
public class MiscellaneousRegisterDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Balance { get; set; } = 0.00m;
    public string Description { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
}

public class CreateMiscTransactionDto
{
    public int CustomerId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // Credit, Debit
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public string ReferenceType { get; set; } = "ManualAdjustment";
}
